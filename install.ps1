<#
.SYNOPSIS
  Installs Clock Screensaver, sets it as the current screensaver, and reports
  anything that could stop Windows from running it.

.DESCRIPTION
  - Checks that the .NET Desktop Runtime 10 (or newer) is installed, and offers
    to install it with winget if it is missing.
  - Removes the "downloaded from the internet" mark (Mark of the Web) from the
    file, which is what triggers SmartScreen's "Windows protected your PC".
  - Copies ClockScreensaver.scr to C:\Windows\System32 (default, needs
    administrator rights) or, with -PerUser, to your own AppData folder.
  - Sets it as the current screensaver.
  - Prints a short check: runtime, Smart App Control state, Defender detections.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -PerUser
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -CheckOnly
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot 'ClockScreensaver.scr'),
    [switch]$PerUser,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'

function Get-System32 {
    # A 32-bit PowerShell sees SysWOW64 as System32; Sysnative reaches the real one.
    if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) {
        return (Join-Path $env:windir 'Sysnative')
    }
    return (Join-Path $env:windir 'System32')
}

$System32Target = Join-Path (Get-System32) 'ClockScreensaver.scr'
$PerUserDir     = Join-Path $env:LOCALAPPDATA 'Programs\ClockScreensaver'
$PerUserTarget  = Join-Path $PerUserDir 'ClockScreensaver.scr'
$DesktopKey     = 'HKCU:\Control Panel\Desktop'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-DesktopRuntimeMajor {
    $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (-not (Test-Path $dotnet)) { return 0 }
    $best = 0
    foreach ($line in (& $dotnet --list-runtimes 2>$null)) {
        if ($line -match '^Microsoft\.WindowsDesktop\.App (\d+)\.') {
            $major = [int]$Matches[1]
            if ($major -gt $best) { $best = $major }
        }
    }
    return $best
}

function Ensure-DesktopRuntime {
    $major = Get-DesktopRuntimeMajor
    if ($major -ge 10) {
        Write-Host "OK   .NET Desktop Runtime $major is installed."
        return
    }
    Write-Host "MISSING  .NET Desktop Runtime 10 (the screensaver needs it to run)." -ForegroundColor Yellow
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        $answer = Read-Host 'Install it now with winget? [Y/n]'
        if ($answer -eq '' -or $answer -match '^[Yy]') {
            & winget install --id Microsoft.DotNet.DesktopRuntime.10 -e --accept-source-agreements --accept-package-agreements
            if ((Get-DesktopRuntimeMajor) -ge 10) { Write-Host 'OK   Runtime installed.'; return }
        }
    }
    Write-Host 'Download ".NET Desktop Runtime 10 (x64)" from:' -ForegroundColor Yellow
    Write-Host '  https://dotnet.microsoft.com/download/dotnet/10.0'
}

function Show-Check {
    param([string]$Path)
    Write-Host ''
    Write-Host '--- Check ---'

    if ($Path -and (Test-Path $Path)) {
        $item = Get-Item $Path
        $ver  = $item.VersionInfo.ProductVersion
        Write-Host "File      $Path  (version $ver, $([math]::Round($item.Length / 1KB)) KB)"
        $motw = Get-Item $Path -Stream Zone.Identifier -ErrorAction SilentlyContinue
        if ($motw) { Write-Host 'WARNING   File still has the "downloaded from internet" mark.' -ForegroundColor Yellow }
        else       { Write-Host 'OK        No "downloaded from internet" mark.' }
        $sig = Get-AuthenticodeSignature $Path
        Write-Host "Signature $($sig.Status)  (unsigned is expected for this project)"
    } else {
        Write-Host 'File      not installed'
    }

    $current = (Get-ItemProperty $DesktopKey -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue).'SCRNSAVE.EXE'
    Write-Host "Current screensaver: $(if ($current) { $current } else { '(none)' })"

    $major = Get-DesktopRuntimeMajor
    Write-Host ".NET Desktop Runtime: $(if ($major) { $major } else { 'not installed' })"

    # Smart App Control (Windows 11): 0 = off, 1 = on, 2 = evaluation.
    $sac = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' -Name VerifiedAndReputablePolicyState -ErrorAction SilentlyContinue).VerifiedAndReputablePolicyState
    switch ($sac) {
        0       { Write-Host 'Smart App Control: off' }
        1       { Write-Host 'Smart App Control: ON - it blocks unsigned apps it does not know, including this one.' -ForegroundColor Yellow }
        2       { Write-Host 'Smart App Control: evaluation mode - Windows may switch it on and start blocking.' -ForegroundColor Yellow }
        default { Write-Host 'Smart App Control: not present' }
    }

    try {
        $hits = Get-MpThreatDetection -ErrorAction Stop | Where-Object { ($_.Resources -join ' ') -match 'ClockScreensaver' }
        if ($hits) {
            Write-Host 'Microsoft Defender has flagged this screensaver:' -ForegroundColor Yellow
            foreach ($h in $hits) {
                $name = (Get-MpThreat -ThreatID $h.ThreatID -ErrorAction SilentlyContinue).ThreatName
                Write-Host "  $($h.InitialDetectionTime)  $name  $($h.Resources -join ', ')"
            }
            Write-Host '  See README, section "If Windows blocks the screensaver".'
        } else {
            Write-Host 'Microsoft Defender: no detections of ClockScreensaver.'
        }
    } catch {
        Write-Host 'Microsoft Defender: could not read detections (run as administrator to see them).'
    }
}

# ----------------------------------------------------------------------------

if ($CheckOnly) {
    $installed = @($System32Target, $PerUserTarget) | Where-Object { Test-Path $_ } | Select-Object -First 1
    Show-Check -Path $installed
    exit 0
}

if (-not (Test-Path $Source)) {
    Write-Host "Cannot find $Source" -ForegroundColor Red
    Write-Host 'Put install.ps1 next to ClockScreensaver.scr, or pass -Source <path>.'
    exit 1
}
$Source = (Resolve-Path $Source).Path

if (-not $PerUser -and -not (Test-Admin)) {
    Write-Host 'Installing to System32 needs administrator rights - asking Windows to elevate...'
    $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Source `"$Source`""
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argList
    exit 0
}

Ensure-DesktopRuntime

# Removing the Mark of the Web is what stops SmartScreen's "Windows protected your PC".
Unblock-File -Path $Source

$target = if ($PerUser) { $PerUserTarget } else { $System32Target }
$targetDir = Split-Path $target
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Force $targetDir | Out-Null }

Copy-Item -Path $Source -Destination $target -Force
Unblock-File -Path $target
Write-Host "OK   Installed to $target"

# Remove a stale copy in the other location so there is only one version around.
$other = if ($PerUser) { $System32Target } else { $PerUserTarget }
if (Test-Path $other) {
    try { Remove-Item $other -Force; Write-Host "OK   Removed old copy $other" }
    catch { Write-Host "NOTE Old copy left at $other (needs administrator rights to remove)." }
}
$wow = Join-Path $env:windir 'SysWOW64\ClockScreensaver.scr'
if (Test-Path $wow) {
    try { Remove-Item $wow -Force; Write-Host "OK   Removed old copy $wow" }
    catch { Write-Host "NOTE Old copy left at $wow" }
}

Set-ItemProperty -Path $DesktopKey -Name 'SCRNSAVE.EXE' -Value $target
Set-ItemProperty -Path $DesktopKey -Name 'ScreenSaveActive' -Value '1'
Write-Host 'OK   Set as the current screensaver.'

Show-Check -Path $target

Write-Host ''
Write-Host 'Next: open Settings > Personalization > Lock screen > Screen saver to set the wait time,'
Write-Host 'or run the screensaver now with:'
Write-Host "  & '$target' /s"
if ($Host.Name -eq 'ConsoleHost') { Read-Host 'Press Enter to close' | Out-Null }
