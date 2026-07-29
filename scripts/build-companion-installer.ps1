<#
.SYNOPSIS
    Builds a client-distributable installer for the in-app AutoCAD AI Assistant.

.DESCRIPTION
    1. Builds Release of Companion.Host, AcadMcp.Plugin and AcadMcp.Backend.
    2. Stages a complete ApplicationPlugins bundle under dist\AcadMcpCompanion.bundle
       (Host + tool host + backend server + first-run readme + PackageContents.xml).
    3. If the Inno Setup compiler (ISCC.exe) is available, compiles installer\AcadMcpCompanion.iss
       into dist\AcadMcpCompanion-Setup-<version>.exe. Otherwise zips the staged bundle so it can
       be dropped into %APPDATA%\Autodesk\ApplicationPlugins manually.

    The installer is per-user (no admin) and asks the client for nothing: API keys are entered
    inside the palette (BYOK) on first run.

.PARAMETER RepoRoot
    Repository root. Defaults to the parent of the script directory.

.EXAMPLE
    pwsh scripts/build-companion-installer.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

$cfg     = "Release"
$tfmWin  = "net8.0-windows"
$tfm     = "net8.0"
$distDir   = Join-Path $RepoRoot "dist"
$stageDir  = Join-Path $distDir "AcadMcpCompanion.bundle"
$contents  = Join-Path $stageDir "Contents"

$hostOut    = Join-Path $RepoRoot "src\Companion\AcadMcp.Companion.Host\bin\$cfg\$tfmWin"
$pluginOut  = Join-Path $RepoRoot "src\AcadMcp.Plugin\bin\$cfg\$tfmWin"
$backendOut = Join-Path $RepoRoot "src\AcadMcp.Backend\bin\$cfg\$tfm"

Write-Host "=== Build Release ===" -ForegroundColor Cyan
& dotnet build (Join-Path $RepoRoot "src\Companion\AcadMcp.Companion.Host\AcadMcp.Companion.Host.csproj") -c $cfg | Out-Null
& dotnet build (Join-Path $RepoRoot "src\AcadMcp.Plugin\AcadMcp.Plugin.csproj") -c $cfg | Out-Null
& dotnet build (Join-Path $RepoRoot "src\AcadMcp.Backend\AcadMcp.Backend.csproj") -c $cfg | Out-Null

Write-Host "=== Stage bundle ===" -ForegroundColor Cyan
if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
New-Item -ItemType Directory -Path $contents -Force | Out-Null

Get-ChildItem -Path $hostOut -File | Where-Object {
    $_.Extension -in @('.dll', '.json') -and $_.Name -notin @('acmgd.dll', 'acdbmgd.dll', 'accoremgd.dll')
} | ForEach-Object { Copy-Item $_.FullName -Destination $contents -Force }

Copy-Item (Join-Path $pluginOut "AcadMcp.Plugin.dll") -Destination $contents -Force
Copy-Item (Join-Path $pluginOut "AcadMcp.Shared.dll") -Destination $contents -Force -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $backendOut '*') -Destination $contents -Recurse -Force
Copy-Item (Join-Path $RepoRoot "installer\README-pierwsze-kroki.txt") -Destination $contents -Force

$version = (Get-Item (Join-Path $hostOut "AcadMcp.Companion.Host.dll")).VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($version)) { $version = "0.1.0" }

$pkg = Get-Content (Join-Path $RepoRoot "installer\PackageContents.companion.xml") -Raw
$pkg = $pkg.Replace('${VERSION}', $version)
Set-Content -Path (Join-Path $stageDir "PackageContents.xml") -Value $pkg -Encoding UTF8

Write-Host "  staged to $stageDir (version $version)" -ForegroundColor DarkGray

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    foreach ($p in @("$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $p) { $iscc = $p; break }
    }
}

if ($iscc) {
    Write-Host "=== Compile installer (Inno Setup) ===" -ForegroundColor Cyan
    $isccPath = if ($iscc -is [System.Management.Automation.CommandInfo]) { $iscc.Source } else { $iscc }
    & $isccPath "/DStagingDir=$stageDir" "/DAppVersion=$version" (Join-Path $RepoRoot "installer\AcadMcpCompanion.iss")
    Write-Host "Installer written to $distDir\AcadMcpCompanion-Setup-$version.exe" -ForegroundColor Green
} else {
    $zip = Join-Path $distDir "AcadMcpCompanion-$version.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path $stageDir -DestinationPath $zip
    Write-Host "Inno Setup not found. Packaged bundle as $zip" -ForegroundColor Yellow
    Write-Host "To install manually: extract into %APPDATA%\Autodesk\ApplicationPlugins\ and restart AutoCAD." -ForegroundColor White
}

exit 0
