<#
.SYNOPSIS
    Adds the acad-router MCP entry to the user's Cursor mcp.json (always-loaded).

.DESCRIPTION
    The acad-router is the ONLY AutoCAD MCP server that should be statically loaded
    in Cursor. All other categories (acad-geometry-2d, acad-architecture, ...) are
    discovered and loaded on demand via MCPBank Dynamics, so they MUST NOT be added
    to mcp.json.
    
    This script:
      1. Locates ~/.cursor/mcp.json (creates if missing)
      2. Backs it up to mcp.json.bak.<timestamp>
      3. Inserts/updates ONLY the "acad-router" entry
      4. Leaves every other entry untouched
    
    The router executable path is resolved from the Release build output of
    AcadMcp.Backend (net8.0). Run `dotnet publish` first if you want a single-file
    binary; otherwise the script points at the .exe in bin/Release/net8.0/.

.PARAMETER ConfigPath
    Override path to mcp.json. Defaults to $env:USERPROFILE\.cursor\mcp.json.

.PARAMETER RepoRoot
    Repository root. Defaults to parent of script directory.

.PARAMETER ExePath
    Override the resolved router executable path.

.PARAMETER DryRun
    Print the resulting JSON to stdout without writing.

.PARAMETER Force
    Overwrite even if entry already matches (rewrites the file).

.EXAMPLE
    pwsh scripts/install-cursor-config.ps1

.EXAMPLE
    pwsh scripts/install-cursor-config.ps1 -DryRun

.EXAMPLE
    pwsh scripts/install-cursor-config.ps1 -ExePath "C:\dist\acad-router.exe"
#>
[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$RepoRoot,
    [string]$ExePath,
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot }
                 elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
                 else { (Get-Location).Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $env:USERPROFILE ".cursor\mcp.json"
}

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $c1 = Join-Path $RepoRoot "src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe"
    $c2 = Join-Path $RepoRoot "src\AcadMcp.Backend\bin\Release\net8.0\publish\AcadMcp.Backend.exe"
    $c3 = Join-Path $RepoRoot "src\AcadMcp.Backend\bin\Debug\net8.0\AcadMcp.Backend.exe"
    $candidates = @($c1, $c2, $c3)
    $ExePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $ExePath) {
        Write-Warning "AcadMcp.Backend.exe not found. Building Release..."
        $proj = Join-Path $RepoRoot "src\AcadMcp.Backend\AcadMcp.Backend.csproj"
        & dotnet build $proj -c Release | Out-Null
        $ExePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $ExePath) {
            Write-Error "Build did not produce AcadMcp.Backend.exe. Aborting."
            exit 2
        }
    }
}

Write-Host "Cursor config : $ConfigPath" -ForegroundColor DarkGray
Write-Host "Router exe    : $ExePath"   -ForegroundColor DarkGray

$config = $null
if (Test-Path $ConfigPath) {
    try {
        $raw = Get-Content $ConfigPath -Raw
        if ([string]::IsNullOrWhiteSpace($raw)) {
            $config = [PSCustomObject]@{ mcpServers = [PSCustomObject]@{} }
        } else {
            $config = $raw | ConvertFrom-Json
        }
    } catch {
        Write-Error "Failed to parse existing mcp.json: $($_.Exception.Message)"
        exit 3
    }
} else {
    $configDir = Split-Path -Parent $ConfigPath
    if (-not (Test-Path $configDir)) { New-Item -ItemType Directory -Path $configDir -Force | Out-Null }
    $config = [PSCustomObject]@{ mcpServers = [PSCustomObject]@{} }
}

if (-not $config.PSObject.Properties['mcpServers']) {
    Add-Member -InputObject $config -MemberType NoteProperty -Name mcpServers -Value ([PSCustomObject]@{}) -Force
}

$desiredEntry = [PSCustomObject]@{
    command = $ExePath
    args    = @("--category", "router")
    env     = [PSCustomObject]@{}
}

$existing = $config.mcpServers.PSObject.Properties['acad-router']
$shouldWrite = $true
if ($existing) {
    $oldJson = $existing.Value | ConvertTo-Json -Depth 10 -Compress
    $newJson = $desiredEntry    | ConvertTo-Json -Depth 10 -Compress
    if ($oldJson -eq $newJson -and -not $Force) {
        Write-Host "acad-router entry already up to date - nothing to do." -ForegroundColor DarkGray
        $shouldWrite = $false
    } else {
        Write-Host "Updating existing acad-router entry." -ForegroundColor Yellow
        $config.mcpServers.'acad-router' = $desiredEntry
    }
} else {
    Write-Host "Adding new acad-router entry." -ForegroundColor Green
    Add-Member -InputObject $config.mcpServers -MemberType NoteProperty -Name 'acad-router' -Value $desiredEntry -Force
}

$serializedFinal = $config | ConvertTo-Json -Depth 20

if ($DryRun) {
    Write-Host ""
    Write-Host "=== Resulting mcp.json (DRY RUN) ===" -ForegroundColor Cyan
    Write-Host $serializedFinal
    exit 0
}

if ($shouldWrite) {
    if (Test-Path $ConfigPath) {
        $stamp  = (Get-Date).ToString("yyyyMMdd-HHmmss")
        $backup = "$ConfigPath.bak.$stamp"
        Copy-Item $ConfigPath $backup
        Write-Host "Backup written: $backup" -ForegroundColor DarkGray
    }
    Set-Content -Path $ConfigPath -Value $serializedFinal -Encoding UTF8
    Write-Host "Wrote $ConfigPath" -ForegroundColor Green
    Write-Host "Restart Cursor to load acad-router." -ForegroundColor Cyan
}

exit 0
