using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LyricTaskbarOverlay;

public sealed class SpotifyWebClient
{
    // Supply your own Spotify API Client ID and Secret if you want to use the Web API
    private static string ClientId => AppConfig.Load().SpotifyClientId;
    private static string ClientSecret => AppConfig.Load().SpotifyClientSecret;
    private const string RedirectUri = "http://127.0.0.1:5000/callback/";
    private static string TokenFile => Path.Combine(AppConfig.AppDataFolder, "spotify_token.json");
    private static string LogFile => Path.Combine(AppConfig.AppDataFolder, "debug.log");

    private readonly HttpClient _http = new();
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset _expiresAt;
    private bool _isAuthenticating;

    public SpotifyWebClient()
    {
        if (File.Exists(TokenFile))
        {
            try
            {
                var json = File.ReadAllText(TokenFile);
                var tokenData = JsonSerializer.Deserialize<TokenData>(json);
                if (tokenData != null)
                {
                    _refreshToken = tokenData.RefreshToken;
                }
            }
            catch { }
        }
    }

    private void Log(string msg)
    {
        File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
    }

    public async Task<SpotifyPlayerState?> GetPlayerStateAsync()
    {
        if (_isAuthenticating) return null;

        if (string.IsNullOrEmpty(_accessToken) || DateTimeOffset.Now >= _expiresAt)
        {
            Log("Need auth. Token empty or expired.");
            await EnsureAuthenticatedAsync();
        }

        if (string.IsNullOrEmpty(_accessToken))
        {
            Log("Token still empty after auth attempt.");
            return null;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/player/currently-playing");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            
            using var response = await _http.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                Log("API returned NoContent (204).");
                return null;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log("API returned Unauthorized (401). Forcing refresh.");
                _accessToken = null;
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log($"API returned {response.StatusCode}. Content: {errorContent}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            Log($"API response length: {content.Length}");
            
            var result = JsonSerializer.Deserialize<CurrentlyPlayingResponse>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Item != null)
            {
                var artist = string.Join(", ", result.Item.Artists.Select(a => a.Name));
                var track = new TrackInfo(artist, result.Item.Name, TimeSpan.FromMilliseconds(result.Item.Duration_Ms));
                return new SpotifyPlayerState(track, TimeSpan.FromMilliseconds(result.Progress_Ms), result.Is_Playing, DateTimeOffset.Now);
            }
            else
            {
                Log("Result or Item is null.");
            }
        }
        catch (Exception ex)
        {
            Log($"Exception in GetPlayerStateAsync: {ex}");
        }

        return null;
    }

    private async Task EnsureAuthenticatedAsync()
    {
        _isAuthenticating = true;
        try
        {
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                Log("Trying to refresh token...");
                var success = await RefreshTokenAsync();
                if (success)
                {
                    Log("Refresh successful.");
                    return;
                }
                Log("Refresh failed, starting browser auth.");
            }

            await AuthenticateUserAsync();
        }
        finally
        {
            _isAuthenticating = false;
        }
    }

    private async Task AuthenticateUserAsync()
    {
        try
        {
            Log("Starting HttpListener on " + RedirectUri);
            using var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();

            var authUrl = $"https://accounts.spotify.com/authorize?client_id={ClientId}&response_type=code&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope=user-read-currently-playing";
            
            Log("Opening browser to " + authUrl);
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            var context = await listener.GetContextAsync();
            var request = context.Request;
            var code = request.QueryString["code"];

            Log($"Got callback. Code length: {code?.Length ?? 0}");

            var response = context.Response;
            var responseString = "<html><body>You can close this tab now.</body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;
            await responseOutput.WriteAsync(buffer, 0, buffer.Length);
            responseOutput.Close();

            listener.Stop();

            if (!string.IsNullOrEmpty(code))
            {
                await ExchangeCodeAsync(code);
            }
        }
        catch (Exception ex)
        {
            Log($"Exception in AuthenticateUserAsync: {ex}");
        }
    }

    private async Task<bool> RefreshTokenAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", _refreshToken!)
        });

        return await ProcessTokenResponseAsync(request);
    }

    private async Task ExchangeCodeAsync(string code)
    {
        Log("Exchanging code for token...");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", RedirectUri)
        });

        await ProcessTokenResponseAsync(request);
    }

    private async Task<bool> ProcessTokenResponseAsync(HttpRequestMessage request)
    {
        try
        {
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log($"Token endpoint returned {response.StatusCode}");
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TokenResponse>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null && !string.IsNullOrEmpty(result.Access_Token))
            {
                Log("Successfully processed token response.");
                _accessToken = result.Access_Token;
                _expiresAt = DateTimeOffset.Now.AddSeconds(result.Expires_In - 60);

                if (!string.IsNullOrEmpty(result.Refresh_Token))
                {
                    _refreshToken = result.Refresh_Token;
                    var tokenData = new TokenData { RefreshToken = _refreshToken };
                    File.WriteAllText(TokenFile, JsonSerializer.Serialize(tokenData));
                }
                return true;
            }
            Log("Token response did not contain access_token.");
        }
        catch (Exception ex)
        {
            Log($"Exception in ProcessTokenResponseAsync: {ex}");
        }
        return false;
    }

    private sealed record TokenData
    {
        public string? RefreshToken { get; set; }
    }

    private sealed record TokenResponse(string Access_Token, string Token_Type, int Expires_In, string? Refresh_Token);

    private sealed record CurrentlyPlayingResponse(long Progress_Ms, bool Is_Playing, PlaybackItem Item);
    private sealed record PlaybackItem(string Name, PlaybackArtist[] Artists, long Duration_Ms);
    private sealed record PlaybackArtist(string Name);
}

public sealed record SpotifyPlayerState(TrackInfo Track, TimeSpan Progress, bool IsPlaying, DateTimeOffset LocalTimestamp);
