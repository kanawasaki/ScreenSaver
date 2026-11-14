<#
.SYNOPSIS
  Removes Clock Screensaver.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -RemoveSettings
#>
param([switch]$RemoveSettings)

$ErrorActionPreference = 'Continue'

$sys = if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) {
    Join-Path $env:windir 'Sysnative'
} else {
    Join-Path $env:windir 'System32'
}
$paths = @(
    (Join-Path $sys 'ClockScreensaver.scr'),
    (Join-Path $env:windir 'SysWOW64\ClockScreensaver.scr'),
    (Join-Path $env:LOCALAPPDATA 'Programs\ClockScreensaver\ClockScreensaver.scr')
)

$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$isAdmin = ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$needsAdmin = $paths[0..1] | Where-Object { Test-Path $_ }
if ($needsAdmin -and -not $isAdmin) {
    Write-Host 'Removing files from the Windows folder needs administrator rights - asking Windows to elevate...'
    $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    if ($RemoveSettings) { $argList += ' -RemoveSettings' }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argList
    exit 0
}

foreach ($p in $paths) {
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "Removed $p" }
}
$perUserDir = Join-Path $env:LOCALAPPDATA 'Programs\ClockScreensaver'
if ((Test-Path $perUserDir) -and -not (Get-ChildItem $perUserDir)) { Remove-Item $perUserDir -Force }

$key = 'HKCU:\Control Panel\Desktop'
$current = (Get-ItemProperty $key -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue).'SCRNSAVE.EXE'
if ($current -match 'ClockScreensaver') {
    Remove-ItemProperty -Path $key -Name 'SCRNSAVE.EXE'
    Write-Host 'Screensaver set to (none).'
}

if ($RemoveSettings) {
    $data = Join-Path $env:LOCALAPPDATA 'ClockScreensaver'
    if (Test-Path $data) { Remove-Item $data -Recurse -Force; Write-Host "Removed settings, log and pictures in $data" }
    $old = Join-Path $env:APPDATA 'ClockScreensaver'
    if (Test-Path $old) { Remove-Item $old -Recurse -Force; Write-Host "Removed old weather cache in $old" }
} else {
    Write-Host "Settings kept in $(Join-Path $env:LOCALAPPDATA 'ClockScreensaver'). Use -RemoveSettings to delete them."
}
if ($Host.Name -eq 'ConsoleHost') { Read-Host 'Press Enter to close' | Out-Null }
