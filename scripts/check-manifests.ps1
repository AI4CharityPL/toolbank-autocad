<#
.SYNOPSIS
    Verifies that for every C# category folder Categories/<Name>/ there is a matching MCPBank manifest
    mcpbank-manifests/acad-<name>.json, and vice versa. Also verifies tool names in code match the manifest.

.DESCRIPTION
    Run by:
      - MSBuild target CheckManifestSync (after Backend build, in Release)
      - scripts/pre-commit.ps1
      - manually before declaring a category done

    Exit codes:
      0  all good (or both empty)
      1  drift detected (logged with details)
      2  invalid arguments / structural error

.PARAMETER RepoRoot
    Repository root. Defaults to parent of script directory.

.PARAMETER FailFast
    If set, stop on first error. Otherwise lists all errors before exiting non-zero.

.PARAMETER Json
    Emit machine-readable JSON instead of human text (for CI consumption).
#>
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [switch]$FailFast,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot }
                 elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
                 else { (Get-Location).Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

$categoriesRoot = Join-Path $RepoRoot "src\AcadMcp.Backend\Categories"
$manifestsRoot  = Join-Path $RepoRoot "mcpbank-manifests"

$problems = New-Object System.Collections.Generic.List[object]

function Add-Problem {
    param([string]$Code, [string]$Severity, [string]$Message, [string]$Where = "")
    $problems.Add([PSCustomObject]@{ Code = $Code; Severity = $Severity; Where = $Where; Message = $Message })
    if ($FailFast) { throw $Message }
}

function Get-CodeCategoriesAndTools {
    param([string]$Root)
    $result = @{}
    if (-not (Test-Path $Root)) { return $result }

    $categoryDirs = Get-ChildItem -Path $Root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne "_Shared" -and -not $_.Name.StartsWith("_") }

    foreach ($d in $categoryDirs) {
        $tools = @()
        $csFiles = Get-ChildItem -Path $d.FullName -Filter *.cs -Recurse -ErrorAction SilentlyContinue
        foreach ($f in $csFiles) {
            $content = Get-Content -Path $f.FullName -Raw
            $matches = [regex]::Matches(
                $content,
                '\[McpTool\s*\(\s*name\s*:\s*"([a-z][a-z0-9_]*)"',
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            foreach ($m in $matches) {
                $tools += $m.Groups[1].Value
            }
            $matches2 = [regex]::Matches(
                $content,
                '\[McpTool\s*\(\s*"([a-z][a-z0-9_]*)"')
            foreach ($m in $matches2) {
                $tools += $m.Groups[1].Value
            }
        }
        $tools = $tools | Sort-Object -Unique
        $result[$d.Name] = $tools
    }
    return $result
}

function Get-ManifestCategoriesAndTools {
    param([string]$Root)
    $result = @{}
    if (-not (Test-Path $Root)) { return $result }

    $manifests = Get-ChildItem -Path $Root -Filter "acad-*.json" -ErrorAction SilentlyContinue
    foreach ($mf in $manifests) {
        $catName = $mf.BaseName -replace "^acad-", ""
        try {
            $obj = Get-Content -Path $mf.FullName -Raw | ConvertFrom-Json
            $tools = @()
            $toolList = $obj.tools_summary
            if (-not $toolList) { $toolList = $obj.tools }
            if ($toolList) {
                foreach ($t in $toolList) {
                    if ($t.name) { $tools += $t.name }
                }
            }
            $result[$catName] = @{
                Tools          = ($tools | Sort-Object -Unique)
                ManifestFile   = $mf.Name
                Id             = $obj.id
                IntentExamples = $obj.intent_examples
            }
        }
        catch {
            Add-Problem -Code "MF0001" -Severity "error" `
                -Message "Manifest is not valid JSON: $($_.Exception.Message)" -Where $mf.Name
        }
    }
    return $result
}

function Normalize-CategoryId {
    param([string]$Name)
    return ($Name -replace '[^a-zA-Z0-9]', '').ToLowerInvariant()
}

# Always tolerate router (it has no Categories/ folder)
$alwaysAllowedManifests = @("router")

$codeCats = Get-CodeCategoriesAndTools -Root $categoriesRoot
$manifestCats = Get-ManifestCategoriesAndTools -Root $manifestsRoot

# Drift check 1: every code category has a manifest
foreach ($cat in $codeCats.Keys) {
    $normalized = Normalize-CategoryId $cat
    $found = $false
    foreach ($mf in $manifestCats.Keys) {
        if ((Normalize-CategoryId $mf) -eq $normalized) { $found = $true; break }
    }
    if (-not $found) {
        Add-Problem -Code "MF1001" -Severity "error" `
            -Message "Code category 'Categories/$cat/' has no matching manifest 'mcpbank-manifests/acad-$($cat.ToLowerInvariant()).json'" `
            -Where "Categories/$cat"
    }
}

# Drift check 2: every manifest has a code category
foreach ($mf in $manifestCats.Keys) {
    if ($alwaysAllowedManifests -contains $mf) { continue }
    $normalized = Normalize-CategoryId $mf
    $found = $false
    foreach ($cat in $codeCats.Keys) {
        if ((Normalize-CategoryId $cat) -eq $normalized) { $found = $true; break }
    }
    if (-not $found) {
        Add-Problem -Code "MF1002" -Severity "error" `
            -Message "Manifest 'acad-$mf.json' has no matching code category 'src/AcadMcp.Backend/Categories/<Name>/'" `
            -Where "mcpbank-manifests/acad-$mf.json"
    }
}

# Drift check 3: tool name set matches per category
foreach ($cat in $codeCats.Keys) {
    $codeTools = $codeCats[$cat]
    $manifestEntry = $null
    foreach ($mf in $manifestCats.Keys) {
        if ((Normalize-CategoryId $mf) -eq (Normalize-CategoryId $cat)) {
            $manifestEntry = $manifestCats[$mf]; break
        }
    }
    if ($null -eq $manifestEntry) { continue }

    $manifestTools = $manifestEntry.Tools
    $manifestFile  = $manifestEntry.ManifestFile
    $missingInManifest = @($codeTools | Where-Object { $manifestTools -notcontains $_ })
    $extraInManifest   = @($manifestTools | Where-Object { $codeTools -notcontains $_ })

    foreach ($t in $missingInManifest) {
        Add-Problem -Code "MF1003" -Severity "error" `
            -Message "Tool '$t' exists in code (Categories/$cat/) but is missing from manifest tools_summary" `
            -Where "mcpbank-manifests/$manifestFile"
    }
    foreach ($t in $extraInManifest) {
        Add-Problem -Code "MF1004" -Severity "error" `
            -Message "Tool '$t' is listed in manifest tools_summary but does not exist in code" `
            -Where "Categories/$cat"
    }
}

# Output
if ($Json) {
    $payload = [PSCustomObject]@{
        Ok       = ($problems.Count -eq 0)
        Problems = $problems
        Code     = @{ CategoryCount = $codeCats.Count; Tools = $codeCats }
        Manifests = @{ Count = $manifestCats.Count }
    }
    $payload | ConvertTo-Json -Depth 6
}
else {
    Write-Host ""
    Write-Host "=== check-manifests ==="  -ForegroundColor Cyan
    Write-Host "Code categories  : $($codeCats.Count)"
    Write-Host "Manifests        : $($manifestCats.Count)"
    Write-Host "Problems         : $($problems.Count)" -ForegroundColor $(if ($problems.Count -eq 0) { "Green" } else { "Yellow" })

    foreach ($p in $problems) {
        $color = switch ($p.Severity) { "error" { "Red" } "warning" { "Yellow" } default { "White" } }
        Write-Host ("  [{0}] {1} :: {2}" -f $p.Code, $p.Where, $p.Message) -ForegroundColor $color
    }

    if ($problems.Count -eq 0) {
        Write-Host ""
        Write-Host "OK" -ForegroundColor Green
    }
}

if ($problems.Count -gt 0) { exit 1 } else { exit 0 }
