# Deploy the freshly built plugin DLL into AutoCAD's ApplicationPlugins bundle
# so the next AutoCAD start auto-loads it. AutoCAD must be CLOSED first — it
# holds AcadMcp.Plugin.dll locked while running. Pass -Kill to force-terminate.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/deploy-plugin.ps1
#   powershell -ExecutionPolicy Bypass -File scripts/deploy-plugin.ps1 -Kill

param(
    [switch]$Kill,
    # Default follows what README, setup.ps1 and the Quickstart all tell you to build.
    # It used to be Debug, which meant deploying after the documented Release build
    # silently pushed whatever stale Debug DLL happened to be on disk - a plugin months
    # old, with no warning, presenting as a successful deploy.
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo   = Split-Path -Parent $PSScriptRoot
$src    = Join-Path $repo ("src\AcadMcp.Plugin\bin\$Configuration\net8.0-windows")
$bundle = "$env:APPDATA\Autodesk\ApplicationPlugins\AcadMcp.bundle\Contents"

# Deploying a build older than the source is almost never intended, so say so loudly.
$dll = Join-Path $src 'AcadMcp.Plugin.dll'
if (Test-Path $dll) {
    $newestSource = Get-ChildItem (Join-Path $repo 'src\AcadMcp.Plugin') -Recurse -Filter *.cs -ErrorAction SilentlyContinue |
                    Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($newestSource -and $newestSource.LastWriteTime -gt (Get-Item $dll).LastWriteTime) {
        Write-Warning ("$Configuration build is OLDER than the source " +
            "($((Get-Item $dll).LastWriteTime) vs $($newestSource.LastWriteTime) in $($newestSource.Name)). " +
            "Run: dotnet build src\AcadMcp.sln -c $Configuration")
    }
}

if (-not (Test-Path $src)) {
    Write-Error "Build output missing: $src. Run: dotnet build src\AcadMcp.sln -c $Configuration"
    exit 1
}
if (-not (Test-Path $bundle)) {
    Write-Error "Bundle folder missing: $bundle. Was AcadMcp.bundle ever installed?"
    exit 1
}

$acad = Get-Process -Name 'acad' -ErrorAction SilentlyContinue
if ($acad) {
    if ($Kill) {
        Write-Host "Stopping acad.exe (PID $($acad.Id))..." -ForegroundColor Yellow
        $acad | Stop-Process -Force
        Start-Sleep -Seconds 2
    } else {
        Write-Error @"
AutoCAD is running (PID $($acad.Id)). Close it first so the plugin DLL can be replaced.
If you have unsaved work in Rysunek1.dwg, save it (CTRL+S) before closing.
Re-run this script once AutoCAD is closed, or pass -Kill to force-terminate.
"@
        exit 1
    }
}

$files = @(
    'AcadMcp.Plugin.dll',
    'AcadMcp.Plugin.pdb',
    'AcadMcp.Plugin.deps.json',
    'AcadMcp.Shared.dll',
    'AcadMcp.Shared.pdb'
)

Write-Host "== Deploying plugin to bundle =="
Write-Host "  src    : $src"
Write-Host "  bundle : $bundle"
$changed = 0
foreach ($f in $files) {
    $s = Join-Path $src $f
    $d = Join-Path $bundle $f
    if (-not (Test-Path $s)) {
        Write-Host "  [skip] $f (not in build output)" -ForegroundColor DarkGray
        continue
    }
    $sInfo = Get-Item $s
    $dInfo = Get-Item $d -ErrorAction SilentlyContinue
    $sameSize = $dInfo -and $dInfo.Length -eq $sInfo.Length
    $sameTime = $dInfo -and $dInfo.LastWriteTime -ge $sInfo.LastWriteTime
    Copy-Item $s $d -Force
    $arrow = if ($sameSize -and $sameTime) { '==' } else { '->' }
    $dInfo2 = Get-Item $d
    Write-Host ("  {0,-28} {1} {2,8} bytes ({3})" -f $f, $arrow, $dInfo2.Length, $dInfo2.LastWriteTime)
    if (-not ($sameSize -and $sameTime)) { $changed++ }
}

Write-Host ""
Write-Host "Deployed. $changed file(s) updated." -ForegroundColor Green
Write-Host "Next steps:"
Write-Host "  1. Start AutoCAD. Auto-load will pick up the new DLL."
Write-Host "  2. Open (or return to) Rysunek1.dwg."
Write-Host "  3. In AutoCAD command line: ACADMCP_STATUS  (should report a tool count ~80)."
