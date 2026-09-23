# Clock Screensaver

A minimal Windows screensaver — pure black screen with a small clock, weather, and ambient effects.

## What it does

- Clock in a chosen corner (or centered) in one of five bundled fonts
- Live weather from [Open-Meteo](https://open-meteo.com/) (no API key needed)
- Ambient effects: rain, snow, thunderstorm, light rays, twinkling stars at night
- Background picture with per-condition brightness and color grading
- Burn-in prevention: clock rotates corners every 10 minutes

## Settings

Right-click the screensaver in **Settings → Personalization → Screen saver** and click **Settings**. Or run:

```
ClockScreensaver.scr /c
```

Options: position, font (IBM Plex Mono, Space Grotesk, Fraunces, Major Mono Display, Syne, system), weight, size, brightness, margin, 24-hour/seconds/date, weather units and location, background picture, effects strength and time-of-day, preview condition for testing.

## How to build

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

```powershell
cd ClockScreensaver
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
Rename-Item publish\ClockScreensaver.exe ClockScreensaver.scr
```

The output is a single self-contained `ClockScreensaver.scr` file (~30 MB) with no external dependencies.

## How to install

**Option A — double-click:** Right-click `ClockScreensaver.scr` → **Install**.

**Option B — manual:**
```powershell
Copy-Item ClockScreensaver.scr "$env:SystemRoot\System32\ClockScreensaver.scr"
```
Then open **Settings → Personalization → Screen saver** and select **ClockScreensaver**.

## How to uninstall

```powershell
Remove-Item "$env:SystemRoot\System32\ClockScreensaver.scr"
# Remove saved settings:
Remove-Item -Path "HKCU:\Software\ClockScreensaver" -Recurse
```

## License

The screensaver code is MIT licensed. See [LICENSE](LICENSE).  
The bundled fonts are SIL Open Font License 1.1. See [ClockScreensaver/fonts/OFL.txt](ClockScreensaver/fonts/OFL.txt).
