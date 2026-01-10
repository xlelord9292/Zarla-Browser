<div align="center">

# 🌐 Zarla Browser

<img src="assets/icons/zarla-logo.svg" width="180" height="180" alt="Zarla Logo">

### **The Browser That Respects You**

*Fast. Private. Powerful.*

A next-generation browser built with C#, WPF & Chromium

<br>

[![Download](https://img.shields.io/badge/⬇_Download-Latest_Release-2ea44f?style=for-the-badge)](https://github.com/xlelord9292/Zarla-Browser/releases)
&nbsp;&nbsp;
![Windows](https://img.shields.io/badge/Windows_10/11-0078D6?style=for-the-badge&logo=windows&logoColor=white)
&nbsp;&nbsp;
![.NET 8](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
&nbsp;&nbsp;
![MIT License](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)

<br>

[**Features**](#-features) · [**Quick Start**](#-quick-start) · [**Build**](#-building-from-source) · [**Shortcuts**](#-keyboard-shortcuts) · [**Contributing**](#-contributing)

---

</div>

<br>

## 🤔 Why Zarla?

Most browsers spy on you, slow you down, and bombard you with ads. **Zarla is different.**

<table>
<tr>
<td width="50%">

### 🔒 **Privacy First**
Zero telemetry. Zero tracking. Your data stays yours.

### 🛡️ **Built-in Protection**
Ads, trackers, and fingerprinting blocked out of the box.

### ⚡ **Lightning Fast**
Smart memory management and tab suspension.

</td>
<td width="50%">

### 🤖 **AI Assistant**
Powered by Groq's blazing-fast LLaMA models.

### 🧩 **Visual Extensions**
Create extensions with blocks — no coding needed.

### 🔄 **Always Updated**
Automatic updates keep you secure.

</td>
</tr>
</table>

<br>

---

## ✨ Features

<details open>
<summary><b>🔐 Privacy & Security</b></summary>
<br>

| Feature | Description |
|:--------|:------------|
| **Ad Blocker** | Blocks ads, popups, and annoyances |
| **Tracker Blocker** | Stops cross-site tracking and analytics |
| **Fingerprint Protection** | Randomizes your browser fingerprint |
| **Password Manager** | Secure, encrypted storage with autofill |
| **Security Scanner** | Scans downloads for malware |
| **HTTPS Enforcement** | Optional HTTPS-only mode |
| **Cookie Control** | Block third-party cookies, auto-dismiss banners |
| **WebRTC Protection** | Prevent IP leaks |
| **Zero Telemetry** | We collect absolutely nothing |

</details>

<details>
<summary><b>🤖 AI Assistant</b></summary>
<br>

| Feature | Description |
|:--------|:------------|
| **Built-in Chat** | Ask questions, get instant answers |
| **Page Summarization** | Summarize any webpage with one click |
| **Key Points** | Extract important info fast |
| **Web Search** | AI searches the web for current info |
| **Custom Models** | Add your own AI models and endpoints |
| **Context Memory** | Remembers conversation context |
| **Powered by Groq** | Lightning-fast LLaMA 4 responses |

</details>

<details>
<summary><b>🧩 Visual Extensions</b></summary>
<br>

Create powerful extensions visually — no coding required!

**Built-in Templates:**
| Extension | What it does |
|:----------|:-------------|
| 🍪 **Cookie Blocker** | Auto-dismiss annoying consent popups |
| 🌙 **Dark Mode** | Force dark mode on any website |
| 📖 **Reading Mode** | Clean, distraction-free reading |
| ▶️ **YouTube Enhancer** | Hide shorts & distracting elements |
| 🐦 **Twitter/X Cleaner** | Hide trending & "who to follow" |
| 👽 **Reddit Enhancer** | Hide promoted posts |
| 🎯 **Focus Mode** | Hide chat widgets & notifications |

*Import/export extensions as `.zarla` files to share with others!*

</details>

<details>
<summary><b>⚡ Performance</b></summary>
<br>

| Feature | Description |
|:--------|:------------|
| **Tab Suspension** | Auto-suspends inactive tabs to save RAM |
| **Smart Memory** | Optimizes memory usage automatically |
| **Hardware Acceleration** | GPU-powered rendering |
| **Lazy Loading** | Optional lazy loading for images |
| **DNS Prefetch** | Faster navigation with predictive DNS |
| **Fast Startup** | Opens in under 2 seconds |

</details>

<details>
<summary><b>🎨 Modern UI</b></summary>
<br>

| Feature | Description |
|:--------|:------------|
| **Dark & Light Themes** | Beautiful, modern interface |
| **Custom Accent Colors** | Make it yours |
| **Tab Previews** | Hover to see tab content |
| **Smooth Animations** | Polished transitions |
| **Compact Mode** | More screen space |
| **Reading Mode** | Distraction-free reading |

</details>

<br>

---

## 🚀 Quick Start

### Option 1: Download Installer (Recommended)

👉 **[Download Latest Release](https://github.com/xlelord9292/Zarla-Browser/releases)**

Just run the installer and you're good to go!

### Option 2: Run from Source

```powershell
git clone https://github.com/xlelord9292/Zarla-Browser.git
cd Zarla-Browser
dotnet run --project src/Zarla.Browser --framework net8.0-windows
```

<br>

---

## 🔨 Building from Source

### Prerequisites

| Requirement | Version | Get it |
|:------------|:--------|:-------|
| Windows | 10 or 11 (64-bit) | — |
| .NET SDK | 8.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| NSIS | 3.x *(for installer)* | [Download](https://nsis.sourceforge.io/) or `winget install NSIS.NSIS` |

### Build Commands

```powershell
# Clone the repo
git clone https://github.com/xlelord9292/Zarla-Browser.git
cd Zarla-Browser

# Build everything (Release + Installer)
.\build.ps1

# That's it! Your installer is at: installer/ZarlaSetup-x.x.x.exe
```

### Build Options

| Command | What it does |
|:--------|:-------------|
| `.\build.ps1` | **Full build** — Release mode + creates installer |
| `.\build.ps1 -DebugOnly` | Quick debug build (no installer) |
| `.\build.ps1 -Clean` | Clean artifacts, then full build |
| `.\build.ps1 -Version "2.0.0"` | Build with specific version number |
| `.\build.ps1 -EncryptKey "key"` | Utility: encrypt an API key |

> 💡 **Tip:** Just run `.\build.ps1` — it does everything automatically!

<br>

---

## ⚙️ Configuration

### zarla-config.json

The main config file in the project root:

```json
{
  "browserName": "Zarla",
  "browserDisplayName": "Zarla Browser",
  "version": "1.0.4",
  
  "enableAutoUpdate": true,
  "updateCheckUrl": "https://api.github.com/repos/YOUR_USER/YOUR_REPO/releases/latest",
  
  "aiEnabled": true,
  "aiApiKey": "",
  "encryptedAIApiKey": "...",
  
  "defaultTheme": "Dark",
  "enableAdBlocker": true,
  "enableTrackerBlocker": true,
  "enableFingerprintProtection": true
}
```

### Key Settings

| Setting | Description |
|:--------|:------------|
| `version` | Auto-updated by build script |
| `updateCheckUrl` | GitHub API URL for auto-updates |
| `aiApiKey` | Plain key (auto-encrypted on build) |
| `encryptedAIApiKey` | Encrypted key for distribution |
| `defaultTheme` | `"Dark"` or `"Light"` |

<br>

---

## 🔑 AI Setup (For Developers)

The AI assistant uses Groq's API. As a developer, you embed your key and it's **automatically encrypted** during build.

### Step 1: Get a Free API Key
1. Go to [console.groq.com/keys](https://console.groq.com/keys)
2. Create an account & generate a key (starts with `gsk_`)

### Step 2: Add to Config
```json
{
  "aiApiKey": "gsk_your_key_here"
}
```

### Step 3: Build
```powershell
.\build.ps1
```

✅ The build script automatically encrypts your key and clears the plain text.  
✅ Users get AI working out of the box — no setup needed!

<br>

---

## ⌨️ Keyboard Shortcuts

<details open>
<summary><b>Tab Management</b></summary>

| Shortcut | Action |
|:---------|:-------|
| `Ctrl+T` | New tab |
| `Ctrl+W` | Close tab |
| `Ctrl+Shift+T` | Reopen closed tab |
| `Ctrl+Tab` | Next tab |
| `Ctrl+Shift+Tab` | Previous tab |
| `Ctrl+1-8` | Switch to tab 1-8 |
| `Ctrl+9` | Last tab |

</details>

<details>
<summary><b>Navigation</b></summary>

| Shortcut | Action |
|:---------|:-------|
| `Ctrl+L` / `Alt+D` | Focus address bar |
| `Ctrl+R` / `F5` | Refresh |
| `Ctrl+Shift+R` | Hard refresh |
| `Alt+Left` | Back |
| `Alt+Right` | Forward |
| `Alt+Home` | Homepage |

</details>

<details>
<summary><b>Tools</b></summary>

| Shortcut | Action |
|:---------|:-------|
| `Ctrl+D` | Bookmark page |
| `Ctrl+H` | History |
| `Ctrl+J` | Downloads |
| `Ctrl+F` | Find on page |
| `F12` | Developer tools |
| `Ctrl+Shift+Delete` | Clear browsing data |
| `F11` | Fullscreen |

</details>

<details>
<summary><b>Zoom & View</b></summary>

| Shortcut | Action |
|:---------|:-------|
| `Ctrl++` | Zoom in |
| `Ctrl+-` | Zoom out |
| `Ctrl+0` | Reset zoom |
| `Ctrl+P` | Print |
| `Ctrl+S` | Save page |

</details>

<br>

---

## 🌐 Internal Pages

| URL | Page |
|:----|:-----|
| `zarla://newtab` | New Tab |
| `zarla://settings` | Settings |
| `zarla://history` | History |
| `zarla://bookmarks` | Bookmarks |
| `zarla://downloads` | Downloads |
| `zarla://extensions` | Extension Builder |
| `zarla://passwords` | Password Manager |
| `zarla://about` | About |

<br>

---

## 📁 Project Structure

```
Zarla/
├── 📄 zarla-config.json       # Main configuration
├── 📄 build.ps1               # Build script (just run it!)
├── 📄 Zarla.sln               # Visual Studio solution
│
├── 📁 src/
│   ├── 📁 Zarla.Browser/      # Main WPF application
│   │   ├── 📁 Views/          # UI windows & dialogs
│   │   ├── 📁 Services/       # Tab manager, downloads, etc.
│   │   └── 📁 Themes/         # Dark & Light themes
│   │
│   ├── 📁 Zarla.Core/         # Core library
│   │   ├── 📁 AI/             # AI service & models
│   │   ├── 📁 Privacy/        # Ad/tracker blocking
│   │   ├── 📁 Security/       # Password manager
│   │   ├── 📁 Extensions/     # Extension system
│   │   └── 📁 Updates/        # Auto-update service
│   │
│   └── 📁 Zarla.Updater/      # Standalone updater
│
├── 📁 assets/                 # Icons & blocklists
├── 📁 installer/              # NSIS installer script
└── 📁 publish/                # Build output
```

<br>

---

## 🤝 Contributing

Contributions are welcome! 

1. **Fork** the repository
2. **Create** a branch: `git checkout -b feature/amazing-feature`
3. **Make** your changes
4. **Test** thoroughly
5. **Commit**: `git commit -m 'Add amazing feature'`
6. **Push**: `git push origin feature/amazing-feature`
7. **Open** a Pull Request

### Development Tips

```powershell
# Hot reload during development
dotnet watch run --project src/Zarla.Browser

# Quick debug build
.\build.ps1 -DebugOnly
```

<br>

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

<br>

---

## 🙏 Acknowledgments

- [CefSharp](https://github.com/cefsharp/CefSharp) — Chromium for .NET
- [Groq](https://groq.com/) — Lightning-fast AI inference
- [EasyList](https://easylist.to/) — Ad blocking filters
- [NSIS](https://nsis.sourceforge.io/) — Installer system

<br>

---

<div align="center">

### ⭐ Star this repo if you find Zarla useful! ⭐

<br>

**Zarla Browser**  
*Browse Fast. Browse Private. Browse Free.*

<br>

Made with ❤️ for a better internet

</div>
