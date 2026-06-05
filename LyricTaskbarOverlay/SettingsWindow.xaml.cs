using System.Windows;
using Microsoft.Win32;

namespace LyricTaskbarOverlay;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly MainWindow _mainWindow;
    private bool _isLoaded;

    public SettingsWindow(AppConfig config, MainWindow mainWindow)
    {
        InitializeComponent();
        _config = config;
        _mainWindow = mainWindow;

        // Initialize UI from config
        OpacitySlider.Value = _config.Opacity;
        FontSizeSlider.Value = _config.FontSize;
        OffsetSlider.Value = _config.LyricOffsetMs;
        UpdateColorRects();
        PinCheckBox.IsChecked = _config.IsPinned;
        StartupCheckBox.IsChecked = _config.RunOnStartup;

        UpdateLabels();
        _isLoaded = true;
    }

    private void UpdateLabels()
    {
        OpacityValueText.Text = $"{(int)(OpacitySlider.Value * 100)}%";
        FontSizeValueText.Text = $"{(int)FontSizeSlider.Value} px";
        OffsetValueText.Text = $"{(int)OffsetSlider.Value} ms";
    }

    private void OnSettingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        UpdateLabels();
        ApplySettings();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        ApplySettings();
    }

    private void UpdateColorRects()
    {
        ActiveColorRect.Fill = ParseColor(_config.ActiveLyricColor, "#F7F7F7");
        InactiveColorRect.Fill = ParseColor(_config.InactiveLyricColor, "#808080");
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
            return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(defaultHex));
        }
    }

    private void ColorBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        
        var isBtnActive = sender == ActiveColorBtn;
        var currentColorBrush = isBtnActive ? ActiveColorRect.Fill : InactiveColorRect.Fill;
        var wpfColor = ((System.Windows.Media.SolidColorBrush)currentColorBrush).Color;

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B),
            FullOpen = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var newHex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            if (isBtnActive)
            {
                _config.ActiveLyricColor = newHex;
            }
            else
            {
                _config.InactiveLyricColor = newHex;
            }
            UpdateColorRects();
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        _config.Opacity = OpacitySlider.Value;
        _config.FontSize = FontSizeSlider.Value;
        _config.LyricOffsetMs = OffsetSlider.Value;
        _config.IsPinned = PinCheckBox.IsChecked ?? false;
        _config.RunOnStartup = StartupCheckBox.IsChecked ?? false;

        _config.Save();
        _mainWindow.ApplyConfig();
        SetRunOnStartup(_config.RunOnStartup);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public static void SetRunOnStartup(bool run)
    {
        try
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            
            var appName = "LyricTaskbarOverlay";
            if (run)
            {
                var path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (path != null)
                {
                    key.SetValue(appName, $"\"{path}\"");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch { }
    }
}
