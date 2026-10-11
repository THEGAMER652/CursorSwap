<p align="center">
  <img src="docs/logo.png" width="96" alt="CursorSwap logo">
</p>

<h1 align="center">CursorSwap</h1>

<p align="center">
  Custom Windows cursors that only show up where you want them.<br>
  Discord by default, with a cursor for every state and animated click effects.
</p>

<p align="center">
  <a href="https://github.com/THEGAMER652/CursorSwap/releases/tag/1.0"><b>⬇ Download v1.0</b></a>
  ·
  <a href="https://thegamer652.github.io/CursorSwap/">Website</a>
  ·
  <a href="https://github.com/THEGAMER652/CursorSwap/issues">Report a bug</a>
</p>

---

## Features

- **Per-app cursors**: the custom cursor only shows while you hover the chosen app. Discord is the default, and it works even before Discord is open.
- **A cursor for every state**: Main, Link, Text, Busy, Working, Precision, Unavailable, Move, Resize and Help. Supports `.png`, `.cur` and animated `.ani`.
- **Adjustable hotspots**: right-click a cursor tile to choose top-left, center or top-center.
- **10 click effects**: Ripple, Pulse, Burst, Ring, Sparkle, Shockwave, Square, Confetti, Splash and Glow, with separate effects and colors for left and right click, size and speed sliders, and a live preview.
- **Zero lag**: it swaps the real Windows cursor, so there's no overlay and no double cursor.
- **Always restores** your normal cursor when you leave the app, exit, or if it crashes.
- **Start with Windows** (optional), launching hidden in the system tray.
- **Pitch-black animated UI** built from custom-drawn WinForms controls.

## Install

1. Download `CursorSwap.exe` from the [latest release](https://github.com/THEGAMER652/CursorSwap/releases/tag/1.0).
2. Run it. There's nothing to install.

Requires Windows 10 or 11 (64-bit).

> If you enable **Start with Windows**, keep the exe in the same folder afterwards, because Windows saves its path.

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/THEGAMER652/CursorSwap.git
cd CursorSwap/CursorSwap
dotnet run -c Release
```

To build a single self-contained `.exe`:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Output: `CursorSwap/bin/Release/net8.0-windows/win-x64/publish/CursorSwap.exe`

Pushing a tag such as `1.1` builds the exe automatically with GitHub Actions and attaches it to a release (see `.github/workflows/release.yml`).

## How it works

| Part | What it does |
| --- | --- |
| `MainForm.cs` | UI and the main 10 ms loop: finds the window under the mouse (`WindowFromPoint` → process name), applies or restores cursors, and detects clicks |
| `CursorManager.cs` | Loads images or `.cur`/`.ani` files and swaps the system cursors with `SetSystemCursor`. `SPI_SETCURSORS` restores the user's own scheme |
| `ClickEffect.cs` | A tiny transparent, click-through, topmost layered window (`UpdateLayeredWindow`) that plays one effect |
| `EffectRenderer.cs` | Draws all ten effects. It's shared by the real overlay and the in-app preview |
| `Controls*.cs`, `Theme.cs` | Custom animated controls (toggles, sliders, chips, tiles…) and the dark theme |
| `Settings.cs` | JSON settings in `%AppData%\CursorSwap\settings.json` and the startup registry key |

The startup option writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CursorSwap`.

## Troubleshooting

**Cursor stuck as the custom one?**
Open Settings → Bluetooth & devices → Mouse → Additional mouse settings → Pointers, click **Use Default**, then **Apply**.

## Contributing

Issues and pull requests are welcome. Please describe what you changed and test it on Windows before opening a PR.

## License

[MIT](LICENSE) © THEGAMER652
