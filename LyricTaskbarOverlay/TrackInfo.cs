namespace LyricTaskbarOverlay;

public sealed record TrackInfo(string Artist, string Name)
{
    public string DisplayName => $"{Artist} - {Name}";
}
