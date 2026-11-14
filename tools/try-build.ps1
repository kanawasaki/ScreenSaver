<#
.SYNOPSIS
  Downloads a Clock Screensaver build from GitHub and installs it, in one step.
  Needs the GitHub CLI (winget install --id GitHub.cli, then gh auth login).

.EXAMPLE
  .\tools\try-build.ps1                  # newest successful build of master
.EXAMPLE
  .\tools\try-build.ps1 -Pr 3            # newest successful build of pull request 3
.EXAMPLE
  .\tools\try-build.ps1 -Release v0.3.0  # a published release
#>
param(
    [int]$Pr,
    [string]$Branch = 'master',
    [string]$Release
)

$ErrorActionPreference = 'Stop'
$repo = 'kanawasaki/ScreenSaver'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI not found. Install it with: winget install --id GitHub.cli  (then open a new terminal and run: gh auth login)'
}

$dir = Join-Path $env:TEMP 'ClockScreensaver-build'
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory $dir | Out-Null

if ($Release) {
    Write-Host "Downloading release $Release..."
    gh release download $Release --repo $repo --pattern '*.zip' --dir $dir
    if ($LASTEXITCODE -ne 0) { throw "Could not download release $Release" }
    $zip = Get-ChildItem $dir -Filter *.zip | Select-Object -First 1
    Expand-Archive $zip.FullName -DestinationPath $dir
} else {
    if ($Pr) {
        $Branch = gh pr view $Pr --repo $repo --json headRefName --jq .headRefName
        if ($LASTEXITCODE -ne 0 -or -not $Branch) { throw "Pull request $Pr not found" }
    }
    $runs = gh run list --repo $repo --workflow build.yml --branch $Branch --status success --limit 1 `
                --json databaseId,headSha,createdAt,displayTitle | ConvertFrom-Json
    if (-not $runs) { throw "No successful build found for branch '$Branch'" }
    $run = $runs[0]
    Write-Host "Build $($run.databaseId): $($run.displayTitle)"
    Write-Host "  branch $Branch, commit $($run.headSha.Substring(0, 7)), built $($run.createdAt)"
    gh run download $run.databaseId --repo $repo --dir $dir
    if ($LASTEXITCODE -ne 0) { throw 'Could not download the build' }
}

$scr = Get-ChildItem $dir -Recurse -Filter ClockScreensaver.scr | Select-Object -First 1
if (-not $scr) { throw 'ClockScreensaver.scr not found in the download' }

# Use the installer that came with this build, so script and screensaver match.
& (Join-Path $scr.DirectoryName 'install.ps1') -Source $scr.FullName
