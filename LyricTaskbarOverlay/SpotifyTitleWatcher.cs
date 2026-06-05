using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace LyricTaskbarOverlay;

public sealed partial class SpotifyTitleWatcher
{
    public TrackInfo? GetCurrentTrack()
    {
        foreach (var process in Process.GetProcessesByName("Spotify"))
        {
            foreach (var title in GetWindowTitles(process.Id))
            {
                var track = ParseTitle(title);
                if (track is not null)
                {
                    return track;
                }
            }
        }

        return null;
    }

    private static TrackInfo? ParseTitle(string title)
    {
        title = title.Trim();
        if (string.IsNullOrWhiteSpace(title) ||
            title.Equals("Spotify", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Spotify Free", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Spotify Premium", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = SpotifyTitleRegex().Match(title);
        if (!match.Success)
        {
            return null;
        }

        var artist = match.Groups["artist"].Value.Trim();
        var name = match.Groups["name"].Value.Trim();
        if (artist.Length == 0 || name.Length == 0)
        {
            return null;
        }

        return new TrackInfo(artist, name);
    }

    private static IEnumerable<string> GetWindowTitles(int processId)
    {
        var titles = new List<string>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var windowProcessId);
            if (windowProcessId != processId)
            {
                return true;
            }

            var length = GetWindowTextLength(hwnd);
            if (length == 0)
            {
                return true;
            }

            var builder = new StringBuilder(length + 1);
            GetWindowText(hwnd, builder, builder.Capacity);
            titles.Add(builder.ToString());
            return true;
        }, IntPtr.Zero);

        return titles;
    }

    [GeneratedRegex(@"^(?<artist>.+?)\s+-\s+(?<name>.+)$")]
    private static partial Regex SpotifyTitleRegex();

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
}
