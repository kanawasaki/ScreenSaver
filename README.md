# Clock Screensaver

A minimal Windows screensaver: a black screen (or a picture) with a small clock, the weather, and gentle weather effects.

## What it does

- Clock in a corner or centered, in one of five bundled fonts or the system font
- Live weather from [Open-Meteo](https://open-meteo.com/) (no API key needed)
- Weather effects: rain and drops on the glass, snow, thunderstorm, sun rays, stars at night
- Background: your own picture, or **weather-matched landscapes** from a folder (see below)
- Burn-in prevention: the clock can move to the next corner every 10 minutes

## Requirements

- Windows 10 or 11, 64-bit
- [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) (x64). `install.ps1` checks for it and offers to install it.

## Install

1. Download `ClockScreensaver-<version>.zip` from [Releases](../../releases) and extract it.
2. In the extracted folder, open PowerShell and run:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```

   It asks for administrator rights, copies the screensaver to `C:\Windows\System32`, removes the
   "downloaded from the internet" mark, sets it as your screensaver and prints a short check.
   To install without administrator rights, add `-PerUser`.
3. Set the wait time in **Settings → Personalization → Lock screen → Screen saver**.

To check an existing install at any time: `.\install.ps1 -CheckOnly`

## If Windows blocks the screensaver

The screensaver is **not code-signed** (see [Code signing](#code-signing)), so Windows' protections may treat it as unknown.
What to do depends on the message you see:

| Message | What it is | What to do |
|---|---|---|
| **"Windows protected your PC"** (blue window) | SmartScreen, because the file was downloaded | Run `install.ps1`, which removes the download mark. Or right-click the file → **Properties** → tick **Unblock**. |
| **"Smart App Control blocked an app that may be unsafe"** | Smart App Control (Windows 11) blocks unsigned apps it doesn't know | Either turn Smart App Control off (**Windows Security → App & browser control → Smart App Control settings**; since the April 2026 Windows update it can be turned back on later), or use a signed build. `install.ps1 -CheckOnly` shows whether it is on. |
| **"Threat found"** / a name like `Trojan:Win32/...` in **Windows Security → Protection history** | Microsoft Defender antivirus, almost certainly a false positive | Submit the file to Microsoft as a false positive at [microsoft.com/wdsi/filesubmission](https://www.microsoft.com/en-us/wdsi/filesubmission); Microsoft usually clears it within days. Meanwhile you can **Allow** it from Protection history. |

Since version 0.3.0 the screensaver is built as a small ordinary .NET program. Earlier versions were a
30 MB compressed bundle that unpacked code into the temp folder at every start, which is a common
reason for antivirus heuristics to flag unsigned programs. If you installed an older version, run
`install.ps1` again to replace it.

## Settings

Open **Screen saver** settings in Windows and click **Settings**, or run `ClockScreensaver.scr /c`.

Settings, the log and the weather cache are stored in `%LOCALAPPDATA%\ClockScreensaver\`.
Click **Open log** in the Settings window to see what the screensaver is doing.

## Weather-matched landscapes

In Settings → **Picture**, choose **Matching landscape from folder** and pick a folder.
The screensaver chooses a picture by its **file name**:

| Part | Words |
|---|---|
| Weather (required) | `clear`, `partly`, `cloudy`, `rain`, `snow`, `storm` |
| Time of day (optional) | `day`, `night`, `dawn`, `dusk` |
| Season (optional) | `spring`, `summer`, `autumn`, `winter` |

Examples: `rain-night-01.jpg`, `clear-day-hills.jpg`, `snow-winter-village.png`.
Words can be in any order, separated by `-`, `_` or spaces; other words are ignored.

If there is no exact match it uses the closest one (a storm falls back to rain, then cloudy; a missing
night picture falls back to the day picture), then your own picture, then black.
Landscape pictures are not tinted for the weather or darkened at night again, because they already show it.

## Build

Prerequisite: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
dotnet publish ClockScreensaver/ClockScreensaver.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
Rename-Item publish\ClockScreensaver.exe ClockScreensaver.scr
.\install.ps1 -Source publish\ClockScreensaver.scr
```

Every push and pull request is built on GitHub Actions; the build is attached to the run as an artifact.
Pushing a tag like `v0.3.0` (or **Actions → Build & Release → Run workflow**) creates a release.

Test modes, for checking output without watching the screen:

```
ClockScreensaver.scr /render <settings.json> <out.png> [Day|Night|Dawn|Dusk] [Spring|Summer|Autumn|Winter]
ClockScreensaver.scr /testpick <folder> <weather> <time> <season> <out.txt> [count]
ClockScreensaver.scr --screenshot-settings [outDir]
```

## Code signing

Releases are currently unsigned. Options considered:

- **SignPath Foundation**: free code signing for open-source projects that qualify. The most realistic option for this project.
- **OV code-signing certificate** from a certificate authority: about $150–300 per year.
- **Microsoft Artifact Signing** (formerly Trusted Signing): about $10 per month, but at present only for individuals in the USA and Canada.

Even a signed file can show SmartScreen warnings until it builds up reputation over several releases.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1                  # keeps your settings
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -RemoveSettings  # also deletes settings and log
```

## License

The code is MIT licensed, see [LICENSE](LICENSE).
The bundled fonts are under the SIL Open Font License 1.1, see [ClockScreensaver/fonts/OFL.txt](ClockScreensaver/fonts/OFL.txt).
