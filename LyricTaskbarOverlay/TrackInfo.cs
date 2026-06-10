namespace LyricTaskbarOverlay;

public sealed record TrackInfo(string Artist, string Name, TimeSpan Duration = default)
{
    public string DisplayName => $"{Artist} - {Name}";
}
