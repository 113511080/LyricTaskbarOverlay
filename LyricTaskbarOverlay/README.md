# Lyric Taskbar Overlay 🎵

A sleek, minimalistic Windows overlay that displays real-time synced lyrics for the song you are currently listening to! It sits right above your taskbar, giving you a karaoke-style experience without cluttering your screen.

## ✨ Features

- **Auto-Syncs with Spotify**: Automatically detects what you're listening to and pulls the synced lyrics. 
- **Auto-Launch**: Simply run the app, and it will automatically open Spotify for you if it isn't already running!
- **Smooth Scrubbing**: Pause, rewind, or skip ahead in your song, and the lyrics will instantly jump to the perfect spot.
- **Customizable UI**: Change the font size, opacity, window size, and text colors directly from the settings menu.
- **Always on Top**: Keeps your lyrics visible over your other windows.

---

## 🚀 Getting Started

### 1. Prerequisites
- **Windows 10 or 11**
- **.NET 8 SDK** (if you are running from the source code)
- **Spotify Desktop App** installed

### 2. How to Run
To start the app, open your terminal in the project folder and run:
```powershell
dotnet run
```
That's it! The app will automatically launch Spotify for you. Once you play a song, the overlay will appear above your taskbar and start displaying lyrics.

### 3. How to Use & Customize
- **Move the Overlay**: Click and drag anywhere on the lyric text to move it around your screen.
- **Settings Menu**: Right-click on the app's icon in your **Windows System Tray** (the small icons in the bottom right of your screen) and select **Settings...**.
- **Close the App**: You can fully exit the overlay by right-clicking the system tray icon and selecting **Close**.
- **Pinning**: In the settings menu, you can check "Pin" to lock the overlay in place so you don't accidentally drag it.
- **Run on Startup**: Want lyrics every time you turn on your PC? Check the "Run on startup" box in the settings!

---

## 🛠️ How it Works (Under the Hood)
- The app uses native **Windows Media Controls** to read your exact playback position. This means it works perfectly in the background without needing a Spotify Premium subscription!
- Lyrics are fetched lightning-fast from the open-source library [LRCLIB](https://lrclib.net), with Netease Cloud Music ([music.163.com](https://music.163.com/)) acting as an alternative source fallback.
- Built with C# and WPF for a lightweight, native Windows experience.

## ⚠️ Known Limitations
- The app requires lyrics to be available on LRCLIB or Netease Cloud Music. If a song is very obscure or new, it may display "No lyrics found".
