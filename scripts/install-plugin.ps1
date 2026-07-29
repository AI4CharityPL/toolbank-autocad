<#
.SYNOPSIS
    Installs AcadMcp.Plugin.dll into AutoCAD's bundle/auto-load location and writes
    an acaddoc.lsp snippet that NETLOADs it on every drawing open.

.DESCRIPTION
    Two install modes:

      -Mode Bundle   (default; recommended)
        Creates %APPDATA%\Autodesk\ApplicationPlugins\AcadMcp.bundle\ with a
        PackageContents.xml manifest. AutoCAD auto-loads it on next launch.

      -Mode Acaddoc
        Appends a NETLOAD line to %APPDATA%\Autodesk\<release>\<product>\<lang>\Support\acaddoc.lsp
        (creating the file if missing). Loads the plugin once per drawing open.

    After install, restart AutoCAD (Bundle mode) or open any drawing (Acaddoc mode), then verify:
        AutoCAD command line: ACADMCP_PING       -> "AcadMcp pong"
        AutoCAD command line: ACADMCP_STATUS     -> pipe state, uptime, registered tools
        Outside AutoCAD:      AcadMcp.Backend.exe --category router --ping-plugin

.PARAMETER Mode
    "Bundle" (default) or "Acaddoc".

.PARAMETER PluginDll
    Override path to AcadMcp.Plugin.dll. Defaults to Release build output.

.PARAMETER RepoRoot
    Repository root. Defaults to parent of script directory.

.PARAMETER Force
    Overwrite existing bundle / acaddoc snippet.

.PARAMETER Uninstall
    Remove the bundle directory and/or acaddoc snippet.

.EXAMPLE
    pwsh scripts/install-plugin.ps1
    pwsh scripts/install-plugin.ps1 -Mode Acaddoc -Force
    pwsh scripts/install-plugin.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [ValidateSet("Bundle", "Acaddoc")]
    [string]$Mode = "Bundle",
    [string]$PluginDll,
    [string]$RepoRoot,
    [switch]$Force,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot }
                 elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
                 else { (Get-Location).Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

if ([string]::IsNullOrWhiteSpace($PluginDll)) {
    $PluginDll = Join-Path $RepoRoot "src\AcadMcp.Plugin\bin\Release\net8.0-windows\AcadMcp.Plugin.dll"
}

$bundleDir   = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\AcadMcp.bundle"
$contentsDir = Join-Path $bundleDir "Contents"
$packageXml  = Join-Path $bundleDir "PackageContents.xml"

if ($Uninstall) {
    Write-Host "=== Uninstall AcadMcp plugin ===" -ForegroundColor Cyan
    if (Test-Path $bundleDir) {
        Remove-Item -Recurse -Force $bundleDir
        Write-Host "  removed $bundleDir" -ForegroundColor Yellow
    } else {
        Write-Host "  bundle dir not present" -ForegroundColor DarkGray
    }
    Get-ChildItem -Path $env:APPDATA -Recurse -Filter "acaddoc.lsp" -ErrorAction SilentlyContinue | ForEach-Object {
        $body = Get-Content $_.FullName -Raw
        if ($body -match 'AcadMcp\.Plugin\.dll') {
            $cleaned = $body -replace '(?ms)^;;; BEGIN AcadMcp.*?;;; END AcadMcp\s*\r?\n', ''
            Set-Content -Path $_.FullName -Value $cleaned -Encoding UTF8
            Write-Host "  cleaned acaddoc.lsp at $($_.FullName)" -ForegroundColor Yellow
        }
    }
    Write-Host "Done." -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $PluginDll)) {
    Write-Warning "Plugin DLL not found at $PluginDll"
    Write-Host "Building Release..." -ForegroundColor DarkGray
    & dotnet build (Join-Path $RepoRoot "src\AcadMcp.Plugin\AcadMcp.Plugin.csproj") -c Release | Out-Null
    if (-not (Test-Path $PluginDll)) {
        Write-Error "Build did not produce $PluginDll"
        exit 2
    }
}

Write-Host "=== Install AcadMcp plugin (mode: $Mode) ===" -ForegroundColor Cyan
Write-Host "Plugin DLL : $PluginDll" -ForegroundColor DarkGray

if ($Mode -eq "Bundle") {
    if ((Test-Path $bundleDir) -and -not $Force) {
        Write-Error "Bundle already exists at $bundleDir. Re-run with -Force to overwrite."
        exit 3
    }
    if (Test-Path $bundleDir) { Remove-Item -Recurse -Force $bundleDir }
    New-Item -ItemType Directory -Path $contentsDir -Force | Out-Null

    $sourceDir = Split-Path -Parent $PluginDll
    Get-ChildItem -Path $sourceDir -File | Where-Object {
        $_.Extension -in @('.dll', '.pdb', '.json')
    } | ForEach-Object {
        Copy-Item $_.FullName -Destination $contentsDir
    }
    Write-Host "  copied $((Get-ChildItem $contentsDir).Count) files to $contentsDir" -ForegroundColor DarkGray

    $version = "0.1.0"
    try {
        $verInfo = (Get-Item $PluginDll).VersionInfo
        if ($verInfo.FileVersion) { $version = $verInfo.FileVersion }
    } catch { }

    $packageContents = @"
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage SchemaVersion="1.0"
                    AppVersion="$version"
                    Name="AcadMcp"
                    Description="AutoCAD MCP plugin - named pipe server hosting AI agent tools"
                    Author="AutoCAD MCP Megasystem"
                    ProductType="Application"
                    AutodeskProduct="AutoCAD">
  <CompanyDetails Name="AutoCAD MCP Megasystem" Url="" Email="" />
  <Components Description="MCP plugin assembly">
    <RuntimeRequirements OS="Win64" Platform="AutoCAD" SeriesMin="R25" />
    <ComponentEntry AppName="AcadMcp"
                    Version="$version"
                    ModuleName="./Contents/AcadMcp.Plugin.dll"
                    AppDescription="Hosts named pipe server, dispatches MCP tool calls to AutoCAD"
                    LoadOnAutoCADStartup="True" />
  </Components>
</ApplicationPackage>
"@
    Set-Content -Path $packageXml -Value $packageContents -Encoding UTF8
    Write-Host "  wrote $packageXml" -ForegroundColor DarkGray

    Write-Host ""
    Write-Host "Bundle installed. Restart AutoCAD, then:" -ForegroundColor Green
    Write-Host "  - In AutoCAD command line: ACADMCP_PING" -ForegroundColor White
    Write-Host "  - In AutoCAD command line: ACADMCP_STATUS" -ForegroundColor White
    Write-Host "  - In a separate terminal:  AcadMcp.Backend.exe --category router --ping-plugin" -ForegroundColor White
} elseif ($Mode -eq "Acaddoc") {
    $supportDirs = @(
        Get-ChildItem -Path $env:APPDATA -Filter "Support" -Recurse -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\Autodesk\\AutoCAD' }
    )
    if ($supportDirs.Count -eq 0) {
        Write-Error "No AutoCAD Support folders found under $env:APPDATA\Autodesk. Install AutoCAD first or use -Mode Bundle."
        exit 3
    }
    foreach ($sd in $supportDirs) {
        $acaddoc = Join-Path $sd.FullName "acaddoc.lsp"
        $existing = if (Test-Path $acaddoc) { Get-Content $acaddoc -Raw } else { "" }
        if ($existing -match 'BEGIN AcadMcp') {
            if (-not $Force) {
                Write-Host "  skip: $acaddoc already references AcadMcp (use -Force to overwrite)" -ForegroundColor DarkGray
                continue
            }
            $existing = $existing -replace '(?ms)^;;; BEGIN AcadMcp.*?;;; END AcadMcp\s*\r?\n', ''
        }
        $escapedDll = $PluginDll.Replace('\','\\')
        $snippet = @"
;;; BEGIN AcadMcp - auto-generated by scripts/install-plugin.ps1, do not edit
(if (not (member "AcadMcp.Plugin" (atoms-family 1)))
  (vl-cmdf "_.NETLOAD" "$escapedDll"))
;;; END AcadMcp
"@
        Set-Content -Path $acaddoc -Value ($existing.TrimEnd() + "`n" + $snippet + "`n") -Encoding UTF8
        Write-Host "  patched $acaddoc" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "acaddoc.lsp patched. Open ANY drawing in AutoCAD, then:" -ForegroundColor Green
    Write-Host "  - In AutoCAD command line: ACADMCP_PING" -ForegroundColor White
    Write-Host "  - In a separate terminal:  AcadMcp.Backend.exe --category router --ping-plugin" -ForegroundColor White
}

exit 0
