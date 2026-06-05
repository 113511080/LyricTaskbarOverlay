using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricTaskbarOverlay;

public sealed partial class LyricsDocument
{
    private readonly IReadOnlyList<LyricsLine> _lines;
    public bool IsSynced { get; }

    private LyricsDocument(IReadOnlyList<LyricsLine> lines, bool isSynced)
    {
        _lines = lines;
        IsSynced = isSynced;
    }

    public static LyricsDocument FromSyncedLyrics(string lrc)
    {
        var lines = new List<LyricsLine>();

        foreach (var rawLine in lrc.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = LrcLineRegex().Match(rawLine);
            if (!match.Success)
            {
                continue;
            }

            var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
            var seconds = int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
            var fraction = double.Parse("0." + match.Groups["f"].Value, CultureInfo.InvariantCulture);
            var timestamp = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds + fraction);
            var text = match.Groups["text"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var words = new List<LyricsWord>();
                var wordMatches = LrcWordRegex().Matches(text);
                
                string cleanText;
                if (wordMatches.Count > 0)
                {
                    foreach (Match wm in wordMatches)
                    {
                        var wmMinutes = int.Parse(wm.Groups["m"].Value, CultureInfo.InvariantCulture);
                        var wmSeconds = int.Parse(wm.Groups["s"].Value, CultureInfo.InvariantCulture);
                        var wmFraction = double.Parse("0." + wm.Groups["f"].Value, CultureInfo.InvariantCulture);
                        var wTime = TimeSpan.FromMinutes(wmMinutes) + TimeSpan.FromSeconds(wmSeconds + wmFraction);
                        var wText = wm.Groups["text"].Value;
                        words.Add(new LyricsWord(wTime, wText));
                    }
                    cleanText = LrcWordRegex().Replace(text, "${text}").Trim();
                }
                else
                {
                    cleanText = text;
                }
                
                lines.Add(new LyricsLine(timestamp, cleanText, words));
            }
        }

        return new LyricsDocument(lines.OrderBy(line => line.Timestamp).ToList(), isSynced: true);
    }

    public static LyricsDocument FromPlainLyrics(string plainLyrics)
    {
        var lines = plainLyrics
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((line, index) => new LyricsLine(TimeSpan.FromSeconds(index * 4), line, Array.Empty<LyricsWord>()))
            .ToList();

        return new LyricsDocument(lines, isSynced: false);
    }

    public LyricsLine? GetLine(TimeSpan elapsed)
    {
        if (_lines.Count == 0)
        {
            return null;
        }

        if (!IsSynced)
        {
            var plainIndex = Math.Min((int)(elapsed.TotalSeconds / 4), _lines.Count - 1);
            return _lines[plainIndex];
        }

        var selected = _lines[0];
        foreach (var line in _lines)
        {
            if (line.Timestamp > elapsed)
            {
                break;
            }

            selected = line;
        }

        return selected;
    }

    public LyricsLine? GetNextLine(TimeSpan elapsed)
    {
        if (_lines.Count == 0) return null;

        if (!IsSynced)
        {
            var currentIndex = (int)(elapsed.TotalSeconds / 4);
            if (currentIndex + 1 < _lines.Count) return _lines[currentIndex + 1];
            return null;
        }

        foreach (var line in _lines)
        {
            if (line.Timestamp > elapsed)
            {
                return line;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^\[(?<m>\d{2}):(?<s>\d{2})\.(?<f>\d{2,3})\]\s*(?<text>.*)$")]
    private static partial Regex LrcLineRegex();

    [GeneratedRegex(@"<(?<m>\d{2}):(?<s>\d{2})\.(?<f>\d{2,3})>\s*(?<text>[^<]*)")]
    private static partial Regex LrcWordRegex();
}

public sealed record LyricsWord(TimeSpan Timestamp, string Text);
public sealed record LyricsLine(TimeSpan Timestamp, string Text, IReadOnlyList<LyricsWord> Words);
