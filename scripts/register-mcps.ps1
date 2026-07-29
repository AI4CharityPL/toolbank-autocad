<#
.SYNOPSIS
    Pushes all mcpbank-manifests/acad-*.json files into the local MCPBank registry.

.DESCRIPTION
    Reads each acad-*.json from mcpbank-manifests/, validates required fields, then upserts
    the entry (matched by id) into the MCPBank registry JSON.
    
    Default registry path matches the user's existing setup:
        C:\Users\DELL\mcpbank\registry\mcpd-registry.json
    
    Override with -Registry. Use -DryRun to preview without writing.

.PARAMETER Registry
    Full path to the MCPBank registry JSON. Auto-detected from the user's mcp.json if not provided.

.PARAMETER RepoRoot
    Repository root. Defaults to parent of script directory.

.PARAMETER DryRun
    Print what would change but do not write.

.PARAMETER Restart
    After registration, suggest the user restart Cursor to pick up registry changes.

.EXAMPLE
    pwsh scripts/register-mcps.ps1

.EXAMPLE
    pwsh scripts/register-mcps.ps1 -DryRun

.EXAMPLE
    pwsh scripts/register-mcps.ps1 -Registry "D:\custom\mcpd-registry.json"
#>
[CmdletBinding()]
param(
    [string]$Registry,
    [string]$RepoRoot,
    [switch]$DryRun,
    [switch]$Restart
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot }
                 elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
                 else { (Get-Location).Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

$manifestsDir = Join-Path $RepoRoot "mcpbank-manifests"
if (-not (Test-Path $manifestsDir)) {
    Write-Error "Manifests directory not found: $manifestsDir"
    exit 2
}

if ([string]::IsNullOrWhiteSpace($Registry)) {
    $cursorMcp = Join-Path $env:USERPROFILE ".cursor\mcp.json"
    if (Test-Path $cursorMcp) {
        try {
            $cfg = Get-Content $cursorMcp -Raw | ConvertFrom-Json
            $bankDyn = $cfg.mcpServers.'mcpbank-dynamic'
            if ($bankDyn -and $bankDyn.args) {
                $idx = [array]::IndexOf($bankDyn.args, "--registry")
                if ($idx -ge 0 -and $idx + 1 -lt $bankDyn.args.Count) {
                    $Registry = $bankDyn.args[$idx + 1] -replace "/", "\"
                    Write-Host "Detected MCPBank registry from mcp.json: $Registry" -ForegroundColor DarkGray
                }
            }
        } catch { }
    }
    if ([string]::IsNullOrWhiteSpace($Registry)) {
        $Registry = Join-Path $env:USERPROFILE "mcpbank\registry\mcpd-registry.json"
        Write-Host "Falling back to default registry path: $Registry" -ForegroundColor DarkGray
    }
}

if (-not (Test-Path $Registry)) {
    Write-Warning "Registry file not found: $Registry"
    Write-Warning "Will create a fresh one (mcpd_version 1.0)."
    $registryDir = Split-Path -Parent $Registry
    if (-not (Test-Path $registryDir)) { New-Item -ItemType Directory -Path $registryDir -Force | Out-Null }
    $now = (Get-Date).ToString("o")
    $fresh = [PSCustomObject]@{
        mcpd_version = "1.0"
        metadata = [PSCustomObject]@{
            name        = "MCPBank Registry"
            description = "Created by AutoCAD MCP register-mcps.ps1"
            created_at  = $now
            updated_at  = $now
            sources     = @($Registry)
        }
        servers = @()
    }
    if (-not $DryRun) {
        $fresh | ConvertTo-Json -Depth 20 | Set-Content -Path $Registry -Encoding UTF8
    }
}

$registryObj = Get-Content $Registry -Raw | ConvertFrom-Json
if (-not $registryObj.servers) {
    Add-Member -InputObject $registryObj -MemberType NoteProperty -Name servers -Value @() -Force
}

$manifestFiles = Get-ChildItem -Path $manifestsDir -Filter "acad-*.json" -ErrorAction SilentlyContinue
if ($manifestFiles.Count -eq 0) {
    Write-Warning "No acad-*.json manifests found in $manifestsDir"
    exit 0
}

$added = 0; $updated = 0; $skipped = 0; $invalid = 0
$serverList = New-Object System.Collections.Generic.List[object]
foreach ($s in $registryObj.servers) { $serverList.Add($s) }

foreach ($mf in $manifestFiles) {
    Write-Host ""
    Write-Host ("=== {0} ===" -f $mf.Name) -ForegroundColor Cyan
    try {
        $entry = Get-Content $mf.FullName -Raw | ConvertFrom-Json
    } catch {
        Write-Host "  [INVALID JSON] $($_.Exception.Message)" -ForegroundColor Red
        $invalid++; continue
    }

    $required = @("id", "name", "description", "transport", "tags", "intent_examples", "tools_summary")
    $missing = @($required | Where-Object {
        $val = $entry.PSObject.Properties[$_]
        return ($null -eq $val) -or ($null -eq $val.Value)
    })
    if ($missing.Count -gt 0) {
        Write-Host ("  [INVALID] Missing required fields: {0}" -f ($missing -join ", ")) -ForegroundColor Red
        $invalid++; continue
    }

    $entryId = $entry.id
    $existingIdx = -1
    for ($i = 0; $i -lt $serverList.Count; $i++) {
        if ($serverList[$i].id -eq $entryId) { $existingIdx = $i; break }
    }

    if ($existingIdx -ge 0) {
        $oldJson = $serverList[$existingIdx] | ConvertTo-Json -Depth 20 -Compress
        $newJson = $entry | ConvertTo-Json -Depth 20 -Compress
        if ($oldJson -eq $newJson) {
            Write-Host "  [unchanged] $entryId" -ForegroundColor DarkGray
            $skipped++
        } else {
            Write-Host "  [UPDATE   ] $entryId  ($($entry.tools_summary.Count) tools)" -ForegroundColor Yellow
            if (-not $DryRun) { $serverList[$existingIdx] = $entry }
            $updated++
        }
    } else {
        Write-Host "  [ADD      ] $entryId  ($($entry.tools_summary.Count) tools)" -ForegroundColor Green
        if (-not $DryRun) { $serverList.Add($entry) | Out-Null }
        $added++
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host ("  Added     : {0}" -f $added)
Write-Host ("  Updated   : {0}" -f $updated)
Write-Host ("  Unchanged : {0}" -f $skipped)
Write-Host ("  Invalid   : {0}" -f $invalid) -ForegroundColor $(if ($invalid -gt 0) { "Red" } else { "DarkGray" })

if ($DryRun) {
    Write-Host ""
    Write-Host "[DRY RUN] No changes written." -ForegroundColor Yellow
    exit 0
}

if ($added -gt 0 -or $updated -gt 0) {
    $registryObj.servers = $serverList.ToArray()
    if ($registryObj.metadata) {
        $registryObj.metadata.updated_at = (Get-Date).ToString("o")
    }
    $json = $registryObj | ConvertTo-Json -Depth 20
    Set-Content -Path $Registry -Value $json -Encoding UTF8
    Write-Host ""
    Write-Host "Registry written: $Registry" -ForegroundColor Green
    if ($Restart) {
        Write-Host "Restart Cursor to pick up the changes." -ForegroundColor Cyan
    }
}

if ($invalid -gt 0) { exit 1 } else { exit 0 }
