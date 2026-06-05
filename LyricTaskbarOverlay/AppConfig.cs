using System.IO;
using System.Text.Json;

namespace LyricTaskbarOverlay;

public sealed class AppConfig
{
    public static string AppDataFolder
    {
        get
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LyricTaskbarOverlay");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }
    }

    private static string ConfigPath => Path.Combine(AppDataFolder, "config.json");

    public double Opacity { get; set; } = 0.0;
    public double FontSize { get; set; } = 21.0;
    public bool IsPinned { get; set; } = false;
    public bool RunOnStartup { get; set; } = false;
    public double Width { get; set; } = 746.4;
    public double Height { get; set; } = 53.6;
    public double Left { get; set; } = 516.8;
    public double Top { get; set; } = 806.4;
    public string ActiveLyricColor { get; set; } = "#C0C0C0";
    public string InactiveLyricColor { get; set; } = "#C0C0C0";
    public double LyricOffsetMs { get; set; } = 2500.0;

    public static AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    return config;
                }
            }
            catch { }
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
