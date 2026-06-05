using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LyricTaskbarOverlay;

public sealed class LyricsClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://lrclib.net/"),
        Timeout = TimeSpan.FromSeconds(8),
    };

    private static readonly HttpClient NeteaseHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    public async Task<LyricsDocument?> GetLyricsAsync(TrackInfo track)
    {
        // 1. Exact match
        var doc = await GetLyricsExactAsync(track.Artist, track.Name);
        if (doc != null) return doc;

        var cleanedName = CleanTrackName(track.Name);
        var cleanedArtist = CleanArtistName(track.Artist);

        // 2. Exact match with cleaned name/artist
        if (cleanedName != track.Name || cleanedArtist != track.Artist)
        {
            doc = await GetLyricsExactAsync(cleanedArtist, cleanedName);
            if (doc != null) return doc;
        }

        // 3. Search match
        doc = await SearchLyricsAsync(track.Artist, track.Name);
        if (doc != null) return doc;

        // 4. Search match with cleaned name
        if (cleanedName != track.Name || cleanedArtist != track.Artist)
        {
            doc = await SearchLyricsAsync(cleanedArtist, cleanedName);
            if (doc != null) return doc;
        }

        // 5. Query search
        doc = await SearchLyricsQueryAsync($"{cleanedArtist} {cleanedName}");
        if (doc != null) return doc;

        // 6. Fallback to Netease Cloud Music
        doc = await FetchNeteaseLyricsAsync(track.Artist, track.Name);
        if (doc != null) return doc;
        
        if (cleanedName != track.Name || cleanedArtist != track.Artist)
        {
            doc = await FetchNeteaseLyricsAsync(cleanedArtist, cleanedName);
        }

        return doc;
    }

    private async Task<LyricsDocument?> GetLyricsExactAsync(string artist, string trackName)
    {
        try
        {
            var path = "api/get?artist_name=" + Uri.EscapeDataString(artist) +
                       "&track_name=" + Uri.EscapeDataString(trackName);

            using var response = await Http.GetAsync(path);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<LrclibResponse>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ProcessResponse(result);
        }
        catch { return null; }
    }

    private async Task<LyricsDocument?> SearchLyricsAsync(string artist, string trackName)
    {
        try
        {
            var path = "api/search?artist_name=" + Uri.EscapeDataString(artist) +
                       "&track_name=" + Uri.EscapeDataString(trackName);

            using var response = await Http.GetAsync(path);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var results = await JsonSerializer.DeserializeAsync<LrclibResponse[]>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ProcessSearchResponses(results);
        }
        catch { return null; }
    }

    private async Task<LyricsDocument?> SearchLyricsQueryAsync(string query)
    {
        try
        {
            var path = "api/search?q=" + Uri.EscapeDataString(query);

            using var response = await Http.GetAsync(path);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var results = await JsonSerializer.DeserializeAsync<LrclibResponse[]>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ProcessSearchResponses(results);
        }
        catch { return null; }
    }

    private LyricsDocument? ProcessResponse(LrclibResponse? result)
    {
        if (!string.IsNullOrWhiteSpace(result?.SyncedLyrics))
            return LyricsDocument.FromSyncedLyrics(result.SyncedLyrics);
        if (!string.IsNullOrWhiteSpace(result?.PlainLyrics))
            return LyricsDocument.FromPlainLyrics(result.PlainLyrics);
        return null;
    }

    private LyricsDocument? ProcessSearchResponses(LrclibResponse[]? results)
    {
        if (results == null || results.Length == 0) return null;
        var bestMatch = results.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.SyncedLyrics))
                        ?? results.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.PlainLyrics));
        return ProcessResponse(bestMatch);
    }

    private string CleanTrackName(string name)
    {
        var index = name.IndexOf(" - ");
        if (index > 0) name = name.Substring(0, index);

        index = name.IndexOf(" (feat.");
        if (index > 0) name = name.Substring(0, index);

        index = name.IndexOf(" (with ");
        if (index > 0) name = name.Substring(0, index);

        return name.Trim();
    }

    private string CleanArtistName(string name)
    {
        var index = name.IndexOf(",");
        if (index > 0) name = name.Substring(0, index);

        index = name.IndexOf(" & ");
        if (index > 0) name = name.Substring(0, index);

        return name.Trim();
    }

    private async Task<LyricsDocument?> FetchNeteaseLyricsAsync(string artist, string trackName)
    {
        try
        {
            var query = $"{artist} {trackName}";
            var searchUrl = "http://music.163.com/api/search/get/web";
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("s", query),
                new KeyValuePair<string, string>("type", "1"),
                new KeyValuePair<string, string>("offset", "0"),
                new KeyValuePair<string, string>("limit", "1")
            });
            
            using var searchResponse = await NeteaseHttp.PostAsync(searchUrl, content);
            if (!searchResponse.IsSuccessStatusCode) return null;
            
            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            var searchDoc = JsonNode.Parse(searchJson);
            
            var songId = searchDoc?["result"]?["songs"]?[0]?["id"]?.GetValue<long>();
            if (songId == null) return null;

            var lyricUrl = $"https://music.163.com/api/song/lyric?id={songId}&lv=1&kv=1&tv=-1";
            using var lyricResponse = await NeteaseHttp.GetAsync(lyricUrl);
            if (!lyricResponse.IsSuccessStatusCode) return null;
            
            var lyricJson = await lyricResponse.Content.ReadAsStringAsync();
            var lyricDoc = JsonNode.Parse(lyricJson);
            
            var lyricStr = lyricDoc?["lrc"]?["lyric"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(lyricStr))
            {
                return LyricsDocument.FromSyncedLyrics(lyricStr);
            }
        }
        catch { }
        return null;
    }

    private sealed record LrclibResponse(string? SyncedLyrics, string? PlainLyrics);
}
