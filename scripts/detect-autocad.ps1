<#
.SYNOPSIS
    Detects installed AutoCAD versions, vertical products, and capabilities.

.DESCRIPTION
    Probes the Windows registry, filesystem, and COM ProgIDs to determine:
      - Whether AutoCAD is installed
      - Which version(s)
      - Whether it is the LT edition (no .NET plugin support)
      - Which vertical (Civil 3D / Mechanical / Architecture / MEP / Plant 3D)
      - Where managed DLLs live (acmgd.dll, acdbmgd.dll, accoremgd.dll)

    Outputs a structured report and writes the result to scripts/.autocad-detection.json
    used by the build system to pick the correct .NET target framework for AcadMcp.Plugin.

.PARAMETER OutputJson
    Path for the JSON report. Default: scripts/.autocad-detection.json

.PARAMETER Quiet
    Suppress human-readable console output (still writes JSON).

.EXAMPLE
    pwsh scripts/detect-autocad.ps1

.EXAMPLE
    pwsh scripts/detect-autocad.ps1 -Quiet -OutputJson C:\temp\acad.json
#>
[CmdletBinding()]
param(
    [string]$OutputJson,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot }
                 elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
                 else { (Get-Location).Path }
    $OutputJson = Join-Path $scriptDir ".autocad-detection.json"
}

function Write-Info {
    param([string]$Message, [string]$Color = "White")
    if (-not $Quiet) { Write-Host $Message -ForegroundColor $Color }
}

function Write-Section {
    param([string]$Title)
    if (-not $Quiet) {
        Write-Host ""
        Write-Host ("=" * 60) -ForegroundColor DarkGray
        Write-Host " $Title" -ForegroundColor Cyan
        Write-Host ("=" * 60) -ForegroundColor DarkGray
    }
}

function Get-RegistryAutoCADInstallations {
    $installations = @()
    $rootKeys = @(
        "HKLM:\SOFTWARE\Autodesk\AutoCAD",
        "HKLM:\SOFTWARE\WOW6432Node\Autodesk\AutoCAD"
    )

    foreach ($root in $rootKeys) {
        if (-not (Test-Path $root)) { continue }
        $versions = Get-ChildItem -Path $root -ErrorAction SilentlyContinue
        foreach ($verKey in $versions) {
            $verName = $verKey.PSChildName
            $subKeys = Get-ChildItem -Path $verKey.PSPath -ErrorAction SilentlyContinue
            foreach ($subKey in $subKeys) {
                try {
                    $props = Get-ItemProperty -Path $subKey.PSPath -ErrorAction SilentlyContinue
                    $installPath = $props.AcadLocation
                    if (-not $installPath) { $installPath = $props."Install Dir" }
                    if (-not $installPath) { continue }

                    $productName = $props.ProductName
                    if (-not $productName) { $productName = $props.PRODUCTNAME }

                    $isLT = $false
                    if ($productName) {
                        $isLT = $productName -match "AutoCAD\s+LT"
                    }
                    if (-not $isLT -and $installPath -match "ACADLT|AutoCADLT") {
                        $isLT = $true
                    }

                    $installations += [PSCustomObject]@{
                        RegistryVersion = $verName
                        ReleaseId       = $subKey.PSChildName
                        ProductName     = $productName
                        InstallPath     = $installPath
                        Release         = $props.Release
                        IsLT            = $isLT
                    }
                }
                catch { }
            }
        }
    }

    return $installations
}

function Test-PluginCapability {
    param([string]$InstallPath)
    if (-not $InstallPath) { return @{ Capable = $false; MissingDlls = @() } }
    if (-not (Test-Path $InstallPath)) { return @{ Capable = $false; MissingDlls = @("install path missing") } }

    $required = @("acmgd.dll", "acdbmgd.dll", "accoremgd.dll")
    $missing = @()
    $present = @{}
    foreach ($dll in $required) {
        $path = Join-Path $InstallPath $dll
        if (Test-Path $path) {
            $present[$dll] = $path
        }
        else {
            $missing += $dll
        }
    }
    return @{
        Capable         = ($missing.Count -eq 0)
        MissingDlls     = $missing
        FoundDllPaths   = $present
    }
}

function Get-Vertical {
    param([string]$InstallPath, [string]$ProductName)

    $vertical = "vanilla"
    if ($ProductName) {
        if ($ProductName -match "Civil 3D")          { $vertical = "civil3d" }
        elseif ($ProductName -match "Mechanical")    { $vertical = "mechanical" }
        elseif ($ProductName -match "Architecture")  { $vertical = "architecture" }
        elseif ($ProductName -match "MEP")           { $vertical = "mep" }
        elseif ($ProductName -match "Plant 3D")      { $vertical = "plant3d" }
        elseif ($ProductName -match "Electrical")    { $vertical = "electrical" }
    }

    if ($vertical -eq "vanilla" -and $InstallPath -and (Test-Path $InstallPath)) {
        if (Test-Path (Join-Path $InstallPath "C3D"))     { $vertical = "civil3d" }
        elseif (Test-Path (Join-Path $InstallPath "ACA")) { $vertical = "architecture" }
        elseif (Test-Path (Join-Path $InstallPath "MEP")) { $vertical = "mep" }
    }

    return $vertical
}

function Get-ComProgIDs {
    $progIds = @()
    $clsidRoots = @("HKLM:\SOFTWARE\Classes", "HKCR:")
    foreach ($root in $clsidRoots) {
        try {
            if ($root -eq "HKCR:" -and -not (Get-PSDrive HKCR -ErrorAction SilentlyContinue)) {
                New-PSDrive -PSProvider Registry -Name HKCR -Root HKEY_CLASSES_ROOT | Out-Null
            }
            $autoCADKeys = Get-ChildItem -Path $root -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -match "^AutoCAD\.Application(\.\d+)?$" }
            foreach ($k in $autoCADKeys) {
                $progIds += $k.PSChildName
            }
        }
        catch { }
    }
    return $progIds | Sort-Object -Unique
}

function Get-NetTargetFramework {
    param([string]$ReleaseId, [bool]$IsLT)
    if ($IsLT) { return @{ Tfm = $null; Reason = "LT does not support .NET plugin"; SupportsPlugin = $false } }

    $year = $null
    if ($ReleaseId -match "^R(\d+)") {
        $relMajor = [int]$Matches[1]
        if ($relMajor -ge 17) {
            $year = 2000 + $relMajor
        }
        else {
            $year = 1994 + $relMajor
        }
    }
    elseif ($ReleaseId -match "^(\d+)\.\d+$") {
        $major = [int]$Matches[1]
        if ($major -ge 17) { $year = 2000 + $major }
    }

    if ($year -ge 2025)   { return @{ Tfm = "net8.0-windows"; Reason = "AutoCAD 2025+ uses .NET 8"; SupportsPlugin = $true; ApiYear = $year } }
    if ($year -ge 2020)   { return @{ Tfm = "net48"; Reason = "AutoCAD 2020-2024 uses .NET Framework 4.8"; SupportsPlugin = $true; ApiYear = $year } }
    if ($year -ge 2017)   { return @{ Tfm = "net47"; Reason = "AutoCAD 2017-2019 uses .NET Framework 4.7"; SupportsPlugin = $true; ApiYear = $year } }

    return @{ Tfm = "net48"; Reason = "Unknown release year, defaulting to net48"; SupportsPlugin = $true; ApiYear = $year }
}

Write-Section "AutoCAD MCP - environment detection"

Write-Info "Scanning Windows registry for AutoCAD installations..." DarkGray
$installations = Get-RegistryAutoCADInstallations

if ($installations.Count -eq 0) {
    Write-Info ""
    Write-Info "[!] No AutoCAD installation found in registry." Yellow
    Write-Info "    Looked at: HKLM:\SOFTWARE\(WOW6432Node\)?Autodesk\AutoCAD" Yellow
    Write-Info "    Plugin will not load. ComBridge fallback also unavailable." Yellow
    $report = [PSCustomObject]@{
        DetectedAt        = (Get-Date).ToString("o")
        Found             = $false
        Installations     = @()
        ComProgIds        = @()
        RecommendedTfm    = $null
        SupportsPlugin    = $false
        Mode              = "none"
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
    Write-Info ""
    Write-Info "Report written to: $OutputJson" DarkGray
    exit 1
}

$results = @()
foreach ($inst in $installations) {
    Write-Section "Installation: $($inst.ProductName) [$($inst.RegistryVersion)\$($inst.ReleaseId)]"
    Write-Info ("Install path : {0}" -f $inst.InstallPath)
    Write-Info ("Is LT        : {0}" -f $inst.IsLT)
    Write-Info ("Release      : {0}" -f $inst.Release)

    $cap = Test-PluginCapability -InstallPath $inst.InstallPath
    Write-Info ("Plugin DLLs  : {0}" -f $(if ($cap.Capable) { "OK (acmgd.dll, acdbmgd.dll, accoremgd.dll found)" } else { "MISSING: $($cap.MissingDlls -join ', ')" }))

    $vertical = Get-Vertical -InstallPath $inst.InstallPath -ProductName $inst.ProductName
    Write-Info ("Vertical     : {0}" -f $vertical)

    $tfm = Get-NetTargetFramework -ReleaseId $inst.RegistryVersion -IsLT $inst.IsLT
    Write-Info ("Target TFM   : {0} ({1})" -f $tfm.Tfm, $tfm.Reason)

    $mode = if ($inst.IsLT) { "com-only" }
            elseif ($cap.Capable) { "full" }
            else { "com-only" }

    Write-Info ("Mode         : {0}" -f $mode) $(if ($mode -eq "full") { "Green" } else { "Yellow" })

    $results += [PSCustomObject]@{
        ProductName     = $inst.ProductName
        RegistryVersion = $inst.RegistryVersion
        ReleaseId       = $inst.ReleaseId
        InstallPath     = $inst.InstallPath
        IsLT            = $inst.IsLT
        Vertical        = $vertical
        PluginCapable   = $cap.Capable
        MissingDlls     = $cap.MissingDlls
        FoundDllPaths   = $cap.FoundDllPaths
        TargetTfm       = $tfm.Tfm
        TfmReason       = $tfm.Reason
        ApiYear         = $tfm.ApiYear
        Mode            = $mode
    }
}

$progIds = Get-ComProgIDs
Write-Section "COM ProgIDs"
if ($progIds.Count -gt 0) {
    foreach ($p in $progIds) { Write-Info "  $p" }
}
else {
    Write-Info "  (none registered)" DarkGray
}

$recommended = $results | Where-Object { $_.PluginCapable } | Sort-Object -Property ApiYear -Descending | Select-Object -First 1
if (-not $recommended) {
    $recommended = $results | Sort-Object -Property ApiYear -Descending | Select-Object -First 1
}

Write-Section "Summary"
Write-Info ("Total installations : {0}" -f $results.Count)
Write-Info ("Plugin-capable      : {0}" -f ($results | Where-Object { $_.PluginCapable } | Measure-Object | Select-Object -ExpandProperty Count))
Write-Info ("Recommended target  : {0} on {1}" -f $recommended.TargetTfm, $recommended.ProductName) Cyan
Write-Info ("Recommended mode    : {0}" -f $recommended.Mode) $(if ($recommended.Mode -eq "full") { "Green" } else { "Yellow" })

if ($recommended.Mode -ne "full") {
    Write-Info ""
    Write-Info "[!] AutoCAD LT or non-plugin-capable install detected." Yellow
    Write-Info "    Categories will operate in COM-only mode (~30% reduced toolset)." Yellow
    Write-Info "    Tools requiring the .NET plugin will report 'available: false'." Yellow
}

$report = [PSCustomObject]@{
    DetectedAt      = (Get-Date).ToString("o")
    Found           = $true
    Installations   = $results
    ComProgIds      = $progIds
    Recommended     = [PSCustomObject]@{
        ProductName    = $recommended.ProductName
        TargetTfm      = $recommended.TargetTfm
        InstallPath    = $recommended.InstallPath
        Mode           = $recommended.Mode
        Vertical       = $recommended.Vertical
        ApiYear        = $recommended.ApiYear
    }
    RecommendedTfm  = $recommended.TargetTfm
    SupportsPlugin  = $recommended.PluginCapable
    Mode            = $recommended.Mode
}

$report | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding UTF8
Write-Info ""
Write-Info "Report written to: $OutputJson" DarkGray
Write-Info ""
exit 0
