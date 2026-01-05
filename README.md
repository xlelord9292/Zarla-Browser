# Zarla Browser

<p align="center">
  <img src="assets/icons/zarla-logo.svg" width="128" height="128" alt="Zarla Logo">
</p>

<p align="center">
  <strong>A fast, lightweight, privacy-focused web browser</strong><br>
  Built with C#, WPF, and WebView2 (Chromium-based)
</p>

<p align="center">
  <a href="https://github.com/xlelord9292/Zarla-Browser/releases">Download</a> •
  <a href="#features">Features</a> •
  <a href="#building">Building</a> •
  <a href="#keyboard-shortcuts">Shortcuts</a>
</p>

---

## Features

### 🔒 Privacy First
- **Built-in Ad Blocker** - Blocks ads and popups
- **Tracker Blocker** - Prevents cross-site tracking
- **Fingerprint Protection** - Randomizes browser fingerprint
- **Do Not Track** - Sends DNT and GPC headers
- **No Telemetry** - Zero data collection

### ⚡ Blazing Fast
- **Tab Suspension** - Auto-suspends inactive tabs
- **Memory Management** - Smart memory optimization
- **Hardware Acceleration** - GPU-powered rendering

### 🎨 Modern UI
- **Dark & Light Themes** - Beautiful, modern interface
- **Tab Drag & Drop** - Reorder tabs easily
- **Keyboard Shortcuts** - Full keyboard navigation

### 🔧 Network Settings
- **DNS Selection** - System, Cloudflare, Quad9, or Google DNS
- **Proxy Support** - Configure custom proxy servers

---

## Quick Start

### Option 1: Download Installer (Recommended)
Download the latest `ZarlaSetup-1.0.0.exe` from [Releases](https://github.com/xlelord9292/Zarla-Browser/releases)

### Option 2: Run from Source
```powershell
git clone https://github.com/xlelord9292/Zarla-Browser.git
cd Zarla-Browser
dotnet run --project src/Zarla.Browser
```

---

## Building

### Prerequisites
- **Windows 10/11** (64-bit)
- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **NSIS** (optional, for installer) - [Download](https://nsis.sourceforge.io/Download)

### Install Prerequisites
```powershell
# Install .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# Install NSIS (optional, for creating installer)
winget install NSIS.NSIS
```

### Build Commands

| Command | Description |
|---------|-------------|
| `.\build.ps1` | Build in Debug mode |
| `.\build.ps1 -Release` | Build in Release mode + publish |
| `.\build.ps1 -Installer` | Build + create setup installer |
| `.\build.ps1 -Clean` | Clean all build artifacts |

### Step-by-Step: Create Installer

```powershell
# 1. Clone the repository
git clone https://github.com/xlelord9292/Zarla-Browser.git
cd Zarla-Browser

# 2. Build the installer (includes Release build)
.\build.ps1 -Installer

# 3. Find your installer at:
#    installer\ZarlaSetup-1.0.0.exe
```

### Manual Build (without build script)

```powershell
# Build
dotnet build -c Release

# Run
dotnet run --project src/Zarla.Browser -c Release

# Publish standalone executable
dotnet publish src/Zarla.Browser/Zarla.Browser.csproj -c Release -r win-x64 -f net8.0-windows --self-contained -o publish/win-x64

# Create installer manually (requires NSIS)
cd installer
makensis zarla-installer.nsi
```

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+T` | New tab |
| `Ctrl+W` | Close tab |
| `Ctrl+Tab` | Next tab |
| `Ctrl+Shift+Tab` | Previous tab |
| `Ctrl+L` | Focus address bar |
| `Ctrl+R` / `F5` | Refresh |
| `Ctrl+D` | Bookmark page |
| `Ctrl+H` | History |
| `Ctrl+J` | Downloads |
| `F12` | Developer tools |
| `Ctrl++` | Zoom in |
| `Ctrl+-` | Zoom out |
| `Ctrl+0` | Reset zoom |

---

## Internal Pages

| URL | Description |
|-----|-------------|
| `zarla://newtab` | New tab page |
| `zarla://settings` | Browser settings |
| `zarla://history` | Browsing history |
| `zarla://bookmarks` | Bookmark manager |
| `zarla://downloads` | Download manager |
| `zarla://about` | About Zarla |

---

## Project Structure

```
Zarla/
├── src/
│   ├── Zarla.Browser/     # Main WPF application
│   │   ├── Views/         # Settings, History, etc.
│   │   ├── Services/      # Tab manager, downloads
│   │   └── Themes/        # Dark and Light themes
│   └── Zarla.Core/        # Core library
│       ├── Privacy/       # Ad/tracker blocking
│       └── Performance/   # Tab suspension, memory
├── assets/icons/          # Application icons
├── installer/             # NSIS installer script
├── build.ps1              # Build script
└── Zarla.sln              # Solution file
```

---

## Configuration

Settings are stored in:
```
%LOCALAPPDATA%\Zarla\settings.json
```

Browsing data (history, bookmarks):
```
%LOCALAPPDATA%\Zarla\zarla.db
```

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## License

MIT License - see [LICENSE](LICENSE) for details.

---

<p align="center">
  <strong>Zarla</strong> - Browse Fast. Browse Private. Browse Free.
</p>
