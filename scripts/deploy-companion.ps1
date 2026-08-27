<#
.SYNOPSIS
    Builds and deploys the in-app AI assistant (AcadMcpCompanion bundle) for local testing.

.DESCRIPTION
    Assembles a single ApplicationPlugins bundle that contains everything the in-app
    assistant needs to run end to end:

      Contents/
        AcadMcp.Companion.Host.dll  (+ Agent, Mcp, deps)  -> WPF chat palette (command ACADAI)
        AcadMcp.Plugin.dll          (+ Shared)            -> AutoCAD pipe server (the tool bank host)
        AcadMcp.Backend.exe         (+ deps, validators)  -> tool-bank server, spawned as a child process

    The bundle is copied to %APPDATA%\Autodesk\ApplicationPlugins\AcadMcpCompanion.bundle.
    AutoCAD auto-loads both modules on next launch. Run ACADAI to open the palette and
    enter your own API key (BYOK) in the Settings tab.

.PARAMETER Configuration
    Debug (default) or Release.

.PARAMETER RepoRoot
    Repository root. Defaults to the parent of the script directory.

.PARAMETER Kill
    Terminate running acad.exe processes before deploying (the bundle DLLs are locked while loaded).

.PARAMETER Uninstall
    Remove the deployed bundle and exit.

.EXAMPLE
    pwsh scripts/deploy-companion.ps1 -Configuration Release -Kill
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$RepoRoot,
    [switch]$Kill,
    [switch]$Uninstall,
    # Dev machines that already have AcadMcp.bundle (Cursor integration) running the pipe
    # server should skip the duplicate plugin to avoid a pipe-name conflict at startup.
    [switch]$SkipPlugin
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

$bundleDir   = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\AcadMcpCompanion.bundle"
$contentsDir = Join-Path $bundleDir "Contents"

if ($Uninstall) {
    if (Test-Path $bundleDir) { Remove-Item -Recurse -Force $bundleDir; Write-Host "Removed $bundleDir" -ForegroundColor Yellow }
    else { Write-Host "Bundle not present." -ForegroundColor DarkGray }
    exit 0
}

if ($Kill) {
    Get-Process acad -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

$tfmWin = "net8.0-windows"
$tfm    = "net8.0"
$hostOut    = Join-Path $RepoRoot "src\Companion\AcadMcp.Companion.Host\bin\$Configuration\$tfmWin"
$pluginOut  = Join-Path $RepoRoot "src\AcadMcp.Plugin\bin\$Configuration\$tfmWin"
$backendOut = Join-Path $RepoRoot "src\AcadMcp.Backend\bin\$Configuration\$tfm"

Write-Host "=== Build ($Configuration) ===" -ForegroundColor Cyan
& dotnet build (Join-Path $RepoRoot "src\Companion\AcadMcp.Companion.Host\AcadMcp.Companion.Host.csproj") -c $Configuration | Out-Null
& dotnet build (Join-Path $RepoRoot "src\AcadMcp.Plugin\AcadMcp.Plugin.csproj") -c $Configuration | Out-Null
& dotnet build (Join-Path $RepoRoot "src\AcadMcp.Backend\AcadMcp.Backend.csproj") -c $Configuration | Out-Null

foreach ($p in @($hostOut, $pluginOut, $backendOut)) {
    if (-not (Test-Path $p)) { Write-Error "Missing build output: $p"; exit 2 }
}

Write-Host "=== Assemble bundle ===" -ForegroundColor Cyan
if (Test-Path $bundleDir) { Remove-Item -Recurse -Force $bundleDir }
New-Item -ItemType Directory -Path $contentsDir -Force | Out-Null

# Host + its private deps (Agent, Mcp, ProtectedData, ...). Exclude AutoCAD interop (loaded by AutoCAD).
Get-ChildItem -Path $hostOut -File | Where-Object {
    $_.Extension -in @('.dll', '.pdb', '.json') -and $_.Name -notin @('acmgd.dll', 'acdbmgd.dll', 'accoremgd.dll')
} | ForEach-Object { Copy-Item $_.FullName -Destination $contentsDir -Force }

# Pipe server (tool-bank host inside AutoCAD). On dev machines with AcadMcp.bundle already
# loaded, skip it to avoid a duplicate pipe server (-SkipPlugin).
if (-not $SkipPlugin) {
    Copy-Item (Join-Path $pluginOut "AcadMcp.Plugin.dll") -Destination $contentsDir -Force
    Copy-Item (Join-Path $pluginOut "AcadMcp.Shared.dll") -Destination $contentsDir -Force -ErrorAction SilentlyContinue
} else {
    # Backend still needs AcadMcp.Shared.dll at runtime; ship it without the plugin module.
    Copy-Item (Join-Path $pluginOut "AcadMcp.Shared.dll") -Destination $contentsDir -Force -ErrorAction SilentlyContinue
    Write-Host "  -SkipPlugin: AcadMcp.Plugin.dll skipped (the pipe comes from AcadMcp.bundle)" -ForegroundColor DarkYellow
}

# Backend tool-bank server (spawned as a child process by the palette). Copy the whole output.
Copy-Item -Path (Join-Path $backendOut '*') -Destination $contentsDir -Recurse -Force

$version = (Get-Item (Join-Path $hostOut "AcadMcp.Companion.Host.dll")).VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($version)) { $version = "0.1.0" }

$packageContents = Get-Content (Join-Path $RepoRoot "installer\PackageContents.companion.xml") -Raw
$packageContents = $packageContents.Replace('${VERSION}', $version)
if ($SkipPlugin) {
    # Drop the AcadMcpToolHost ComponentEntry (plugin) so AutoCAD doesn't try to load a
    # second pipe server; only the Companion Host (ACADAI palette) is auto-loaded.
    $packageContents = [System.Text.RegularExpressions.Regex]::Replace(
        $packageContents,
        '(?s)\s*<ComponentEntry AppName="AcadMcpToolHost".*?/>',
        '')
}
Set-Content -Path (Join-Path $bundleDir "PackageContents.xml") -Value $packageContents -Encoding UTF8

Write-Host ""
Write-Host "Deployed AcadMcpCompanion bundle to $bundleDir" -ForegroundColor Green
Write-Host "Files in Contents: $((Get-ChildItem $contentsDir -File).Count)" -ForegroundColor DarkGray
Write-Host "Restart AutoCAD, run ACADAI, then enter your API key in the Settings tab." -ForegroundColor White
exit 0
