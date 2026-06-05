# Lyric Taskbar Overlay

A small Windows overlay that shows lyrics for the song you are listening to. It is designed to look like a compact text strip near the bottom taskbar area.

## How it works

- Uses Windows native Global System Media Transport Controls (GSMTC) to get exact playback position and play/pause state from any media player (like Spotify).
- Looks up lyrics from `https://lrclib.net`.
- Shows the current synced lyric line in an always-on-top WPF window just above the taskbar.

This does not modify the real Windows taskbar. Windows does not provide a reliable public API for inserting arbitrary live text into the taskbar, so the app uses a taskbar-adjacent overlay.

## Requirements

- Windows 10/11
- .NET 8 SDK or newer
- Spotify desktop app (or any supported Windows media player)
- Internet access for lyrics lookup

## Run

```powershell
dotnet run --project .\LyricTaskbarOverlay
```

Start a song in Spotify, then run the app. The overlay should update automatically and sync the lyrics based on the exact timeline position.

Right-click the system tray icon to open Settings or close the app. You can also drag the overlay to move it if it's not pinned.

## Configuration
Spotify Web API integration is available but optional. If you want to use it, you must provide your own Spotify API Client ID and Secret. Otherwise, the app falls back to Windows Media Controls.

## Limitations

- Lyrics availability depends on LRCLIB.
