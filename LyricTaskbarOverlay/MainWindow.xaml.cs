using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace LyricTaskbarOverlay;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly SpotifyWebClient _spotify = new();
    private readonly SpotifyTitleWatcher _spotifyTitle = new();
    private readonly WindowsMediaWatcher _windowsMedia = new();
    private readonly LyricsClient _lyricsClient = new();
    private readonly AppConfig _config = AppConfig.Load();

    private TrackInfo? _track;
    private LyricsDocument? _lyrics;
    private string _lastDisplayed = "";
    private bool _isFetching;
    private bool _isTicking;

    private DateTimeOffset _lastFetchAttempt = DateTimeOffset.MinValue;
    private int _fetchFailCount = 0;

    private System.Windows.Controls.TextBlock _activeTextBlock;
    private System.Windows.Controls.TextBlock _inactiveTextBlock;
    private string _currentLineText = "";

    private NotifyIcon _notifyIcon;

    private DateTimeOffset _lastApiCheck = DateTimeOffset.MinValue;
    private SpotifyPlayerState? _playerState;
    private DateTimeOffset _trackDetectedAt = DateTimeOffset.MinValue;
    
    private bool _spotifyWasRunning = false;
    private DateTimeOffset _lastSpotifyProcessCheck = DateTimeOffset.MinValue;
    private bool _isSpotifyRunningCached = false;

    private IntPtr _hwnd = IntPtr.Zero;

    public MainWindow()
    {
        InitializeComponent();
        _activeTextBlock = LyricText1;
        _inactiveTextBlock = LyricText2;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        LocationChanged += OnLocationChanged;
        SourceInitialized += OnSourceInitialized;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        _timer.Tick += OnTick;

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Lyric Taskbar Overlay"
        };
        SetupTrayMenu();
        
        ApplyConfig();
        // Ensure startup registry is correct on launch
        SettingsWindow.SetRunOnStartup(_config.RunOnStartup);
    }

    public void ApplyConfig()
    {
        this.Opacity = 1.0;
        LyricText1.FontSize = _config.FontSize;
        LyricText2.FontSize = _config.FontSize;

        var alpha = (byte)(_config.Opacity * 255);
        Chrome.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 27, 27, 27));
        Chrome.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 51, 51, 51));
    }

    private void SetupTrayMenu()
    {
        var menu = new ContextMenuStrip();
        
        menu.Items.Add(new ToolStripMenuItem("Settings...", null, (s, e) => {
            var sw = new SettingsWindow(_config, this);
            sw.Show();
        }));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Close", null, (s, e) => Close()));

        _notifyIcon.ContextMenuStrip = menu;
    }

    protected override void OnClosed(EventArgs e)
    {
        _config.Left = this.Left;
        _config.Top = this.Top;
        _config.Save();
        _notifyIcon.Dispose();
        base.OnClosed(e);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Width = _config.Width;
        Height = _config.Height;
        
        if (_config.Left != double.MinValue && _config.Top != double.MinValue)
        {
            Left = _config.Left;
            Top = _config.Top;

            var rect = new Rect(Left, Top, Width, Height);
            var visible = System.Windows.Forms.Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(
                new Rectangle((int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height)));

            if (!visible)
            {
                Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
                Top = SystemParameters.WorkArea.Bottom - Height - 20;
            }
        }
        else
        {
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
            Top = SystemParameters.WorkArea.Bottom - Height - 20;
        }

        await _windowsMedia.InitializeAsync();
        _timer.Start();

        LaunchSpotifyIfNeeded();

    }

    private void LaunchSpotifyIfNeeded()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Spotify");
            bool hasMainWindow = false;
            foreach (var p in processes)
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    hasMainWindow = true;
                    break;
                }
            }

            if (!hasMainWindow)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "spotify:",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore if Spotify isn't installed or URI scheme is missing
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
        {
            _config.Width = e.NewSize.Width;
            _config.Height = e.NewSize.Height;
            _config.Save();
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (IsLoaded)
        {
            _config.Left = Left;
            _config.Top = Top;
            _config.Save();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(_hwnd, GwlExStyle);
        SetWindowLong(_hwnd, GwlExStyle, exStyle | WsExToolWindow | WsExNoActivate);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !_config.IsPinned)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_isTicking) return;
        _isTicking = true;

        try
        {
            if (DateTimeOffset.Now - _lastSpotifyProcessCheck > TimeSpan.FromSeconds(2))
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("Spotify");
                _isSpotifyRunningCached = false;
                foreach (var p in processes)
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        _isSpotifyRunningCached = true;
                        break;
                    }
                }
                _lastSpotifyProcessCheck = DateTimeOffset.Now;
            }

            if (_isSpotifyRunningCached)
            {
                _spotifyWasRunning = true;
                if (this.Visibility != Visibility.Visible)
                {
                    this.Visibility = Visibility.Visible;
                }
                
                if (_hwnd != IntPtr.Zero)
                {
                    SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            else
            {
                if (_spotifyWasRunning)
                {
                    Close();
                    return;
                }
                else
                {
                    this.Visibility = Visibility.Hidden;
                    return;
                }
            }

            if (DateTimeOffset.Now - _lastApiCheck > TimeSpan.FromSeconds(2))
            {
                var state = await _spotify.GetPlayerStateAsync();
                
                if (state == null)
                {
                    state = await _windowsMedia.GetCurrentMediaStateAsync();
                }

                if (state != null)
                {
                    _playerState = state;
                }
                else
                {
                    _playerState = null;
                }
                _lastApiCheck = DateTimeOffset.Now;
            }

            TrackInfo? current = _playerState?.Track;
            TimeSpan elapsed = TimeSpan.Zero;

            if (_playerState != null)
            {
                elapsed = _playerState.IsPlaying 
                    ? _playerState.Progress + (DateTimeOffset.Now - _playerState.LocalTimestamp)
                    : _playerState.Progress;
            }
            else
            {
                current = _spotifyTitle.GetCurrentTrack();
                if (current != null)
                {
                    if (!_track?.Equals(current) ?? true)
                    {
                        _trackDetectedAt = DateTimeOffset.Now;
                    }
                    elapsed = DateTimeOffset.Now - _trackDetectedAt;
                }
            }

            elapsed += TimeSpan.FromMilliseconds(_config.LyricOffsetMs); // Add offset to compensate for lyrics delay

            if (current is null)
            {
                SetStatus("Waiting for Spotify...", "");
                return;
            }

            if (!_track?.Equals(current) ?? true)
            {
                _track = current;
                _lyrics = null;
                _fetchFailCount = 0;
                _trackDetectedAt = DateTimeOffset.Now;
                SetStatus("Finding lyrics...", current.DisplayName);
                await FetchLyricsAsync(current);
                return;
            }

            if (_lyrics is null)
            {
                var timeSinceLastAttempt = DateTimeOffset.Now - _lastFetchAttempt;
                if (!_isFetching && timeSinceLastAttempt > TimeSpan.FromSeconds(Math.Min(30, 5 * (1 << _fetchFailCount))))
                {
                    SetStatus("Retrying lyrics...", current.DisplayName);
                    await FetchLyricsAsync(current);
                    return;
                }

                SetStatus("No lyrics found", current.DisplayName);
                return;
            }

            var line = _lyrics.GetLine(elapsed);
            SetLine(line, current.DisplayName, elapsed);
        }
        finally
        {
            _isTicking = false;
        }
    }

    private async Task FetchLyricsAsync(TrackInfo track)
    {
        if (_isFetching) return;
        _isFetching = true;
        _lastFetchAttempt = DateTimeOffset.Now;
        try
        {
            var lyrics = await _lyricsClient.GetLyricsAsync(track);
            if (_track?.Equals(track) == true)
            {
                _lyrics = lyrics;
                if (lyrics is null) 
                {
                    _fetchFailCount++;
                    SetStatus("No lyrics found", track.DisplayName);
                }
                else
                {
                    _fetchFailCount = 0;
                }
            }
        }
        catch
        {
            if (_track?.Equals(track) == true)
            {
                _fetchFailCount++;
                SetStatus("Lyrics lookup failed", track.DisplayName);
            }
        }
        finally
        {
            _isFetching = false;
        }
    }

    private void SetStatus(string status, string track)
    {
        var combined = $"{status}";
        if (combined == _lastDisplayed) return;
        _lastDisplayed = combined;
        
        var activeBrush = ParseColor(_config.ActiveLyricColor, "#F7F7F7");
        TransitionTo(status, activeBrush);
    }

    private System.Windows.Media.SolidColorBrush ParseColor(string hex, string defaultHex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new System.Windows.Media.SolidColorBrush(color);
        }
        catch
        {
            var fallback = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(defaultHex);
            return new System.Windows.Media.SolidColorBrush(fallback);
        }
    }

    private void TransitionTo(string text, System.Windows.Media.SolidColorBrush foregroundBrush)
    {
        _currentLineText = text;

        var oldBlock = _activeTextBlock;
        var newBlock = _inactiveTextBlock;

        _activeTextBlock = newBlock;
        _inactiveTextBlock = oldBlock;

        var oldFadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        var oldSlideUp = new System.Windows.Media.Animation.DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(300));
        oldBlock.BeginAnimation(UIElement.OpacityProperty, oldFadeOut);
        oldBlock.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, oldSlideUp);

        newBlock.Inlines.Clear();
        newBlock.Text = text;
        newBlock.Foreground = foregroundBrush;
        
        var newFadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        var newSlideUp = new System.Windows.Media.Animation.DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300));
        newBlock.BeginAnimation(UIElement.OpacityProperty, newFadeIn);
        newBlock.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, newSlideUp);
    }

    private void SetLine(LyricsLine? line, string track, TimeSpan elapsed)
    {
        if (line == null)
        {
            SetStatus("No lyrics found", track);
            return;
        }

        if (line.Text != _currentLineText)
        {
            var activeBrushLocal = ParseColor(_config.ActiveLyricColor, "#F7F7F7");
            TransitionTo(line.Text, activeBrushLocal);
        }
    }

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
