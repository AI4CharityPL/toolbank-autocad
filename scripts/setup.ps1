<#
.SYNOPSIS
    One command to go from a fresh clone to AutoCAD driven by your AI client.

.DESCRIPTION
    Replaces the six-step Quickstart. Runs the whole chain, checks its own work at
    each stage, and tells you exactly what to do next:

      1. Preflight   - .NET SDK 8+, AutoCAD 2025+ (via detect-autocad.ps1)
      2. Build       - dotnet build -c Release (also runs check-manifests)
      3. Package     - one launcher per category (package.ps1)
      4. Plugin      - installs the ApplicationPlugins bundle (install-plugin.ps1)
      5. Client      - writes the acad-router entry into your MCP client's config
      6. Verify      - live handshake with the plugin, if AutoCAD is running

    Only acad-router goes into your client config. The other 30 categories are
    discovered on demand through MCP Nexus - that is the entire point of the
    architecture, and adding them statically would defeat it. See
    docs/engineering-rules/00-architecture-invariants.md, invariant 5.

    Written for Windows PowerShell 5.1 as well as PowerShell 7, so it runs on a
    stock Windows install with no extra prerequisites.

.PARAMETER Client
    Which MCP client to configure: cursor (default), claude-desktop, claude-code,
    or none. 'claude-code' writes .mcp.json in the repository root.

.PARAMETER SkipBuild
    Skip the build. Fails fast if the Release output is not already there.

.PARAMETER SkipPlugin
    Skip installing the AutoCAD bundle. Use when the plugin is already deployed
    and AutoCAD is running - reinstalling would need an AutoCAD restart.

.PARAMETER DryRun
    Report every action without building, writing or installing anything.

.PARAMETER Force
    Overwrite an existing plugin bundle and rewrite the client entry even if it
    already matches.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/setup.ps1

.EXAMPLE
    pwsh scripts/setup.ps1 -Client claude-desktop

.EXAMPLE
    pwsh scripts/setup.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [ValidateSet("cursor", "claude-desktop", "claude-code", "none")]
    [string]$Client = "cursor",
    [switch]$SkipBuild,
    [switch]$SkipPlugin,
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$RepoRoot = Split-Path -Parent $scriptDir

$script:StepNo = 0
$script:Warnings = New-Object System.Collections.Generic.List[string]

function Write-Step([string]$title) {
    $script:StepNo++
    Write-Host ""
    Write-Host ("[{0}/6] {1}" -f $script:StepNo, $title) -ForegroundColor Cyan
}
function Write-Ok([string]$m)   { Write-Host "      $m" -ForegroundColor Green }
function Write-Info([string]$m) { Write-Host "      $m" -ForegroundColor DarkGray }
function Write-Warn([string]$m) { Write-Host "      $m" -ForegroundColor Yellow; $script:Warnings.Add($m) }
function Fail([string]$m, [int]$code = 1) { Write-Host ""; Write-Host "  FAILED: $m" -ForegroundColor Red; Write-Host ""; exit $code }

Write-Host ""
Write-Host "  MCP Nexus AutoCAD - setup" -ForegroundColor White
Write-Host "  repo   : $RepoRoot" -ForegroundColor DarkGray
Write-Host "  client : $Client" -ForegroundColor DarkGray
if ($DryRun) { Write-Host "  DRY RUN - nothing will be written" -ForegroundColor Cyan }

# ── 1. Preflight ────────────────────────────────────────────────────────────
Write-Step "Preflight"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { Fail ".NET SDK not found. Install .NET SDK 8.0 or later: https://dotnet.microsoft.com/download" 2 }
$sdkVersion = (& dotnet --version) 2>$null
$sdkMajor = 0
if ($sdkVersion -match '^(\d+)\.') { $sdkMajor = [int]$Matches[1] }
if ($sdkMajor -lt 8) { Fail ".NET SDK 8.0 or later required, found $sdkVersion" 2 }
Write-Ok ".NET SDK $sdkVersion"

$detect = Join-Path $scriptDir "detect-autocad.ps1"
$acadOk = $false
if (Test-Path $detect) {
    try {
        & $detect -Quiet | Out-Null
        $detectJson = Join-Path $scriptDir ".autocad-detection.json"
        if (Test-Path $detectJson) {
            $d = Get-Content $detectJson -Raw | ConvertFrom-Json
            if ($d.Found -and $d.Recommended) {
                $acadOk = $true
                Write-Ok ("AutoCAD: {0}" -f $d.Recommended.ProductName)
                Write-Info ("path   : {0}" -f $d.Recommended.InstallPath)
                if ($d.Recommended.Mode -ne "full") {
                    Write-Warn "Detected an LT / limited install - the .NET plugin path is unavailable, only the COM bridge will work."
                }
            }
        }
    } catch {
        Write-Warn "detect-autocad.ps1 failed: $($_.Exception.Message)"
    }
}
if (-not $acadOk) {
    Write-Warn "AutoCAD 2025+ not detected. Build will still run, but the plugin cannot be installed or verified."
    if (-not $SkipPlugin) { $SkipPlugin = $true; Write-Info "Skipping the plugin step as a result." }
}

# ── 2. Build ────────────────────────────────────────────────────────────────
Write-Step "Build"

$sln = Join-Path $RepoRoot "src\AcadMcp.sln"
$exe = Join-Path $RepoRoot "src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe"

if ($SkipBuild) {
    if (-not (Test-Path $exe)) { Fail "-SkipBuild was passed but $exe does not exist. Run without -SkipBuild." 3 }
    Write-Info "skipped (-SkipBuild)"
} elseif ($DryRun) {
    Write-Info "would run: dotnet build `"$sln`" -c Release"
} else {
    Write-Info "dotnet build -c Release ..."
    $buildLog = & dotnet build $sln -c Release --nologo -v minimal 2>&1
    if ($LASTEXITCODE -ne 0) {
        $buildLog | Select-Object -Last 25 | ForEach-Object { Write-Host "      $_" -ForegroundColor Red }
        Fail "Build failed. If it cannot find acdbmgd.dll, pass the AutoCAD path: dotnet build `"$sln`" -c Release -p:AcadInstallPath='C:\Program Files\Autodesk\AutoCAD 2025\'" 3
    }
    if (-not (Test-Path $exe)) { Fail "Build reported success but $exe is missing." 3 }
    Write-Ok "build OK (check-manifests runs as part of it)"
}

# ── 3. Package launchers ────────────────────────────────────────────────────
Write-Step "Package launchers"

$pkg = Join-Path $scriptDir "package.ps1"
if (Test-Path $pkg) {
    $pkgArgs = @{ RepoRoot = $RepoRoot }
    if ($DryRun) { $pkgArgs["DryRun"] = $true }
    & $pkg @pkgArgs | ForEach-Object { Write-Info $_ }
    if ($LASTEXITCODE -ne 0) { Write-Warn "package.ps1 reported a problem - see above." }
} else {
    Write-Warn "package.ps1 not found - skipping."
}

# ── 4. Plugin bundle ────────────────────────────────────────────────────────
Write-Step "AutoCAD plugin bundle"

if ($SkipPlugin) {
    Write-Info "skipped"
} else {
    $acadRunning = @(Get-Process acad -ErrorAction SilentlyContinue).Count -gt 0
    if ($acadRunning) {
        Write-Warn "AutoCAD is running. Its DLLs are locked, so the bundle cannot be replaced right now."
        Write-Info "Close AutoCAD and re-run, or install the bundle separately with:"
        Write-Info "  powershell -ExecutionPolicy Bypass -File scripts\install-plugin.ps1 -Force"
    } elseif ($DryRun) {
        Write-Info "would run: install-plugin.ps1"
    } else {
        $ip = Join-Path $scriptDir "install-plugin.ps1"
        $ipArgs = @{ RepoRoot = $RepoRoot }
        if ($Force) { $ipArgs["Force"] = $true }
        & $ip @ipArgs | ForEach-Object { Write-Info $_ }
        if ($LASTEXITCODE -ne 0) { Write-Warn "install-plugin.ps1 exited with $LASTEXITCODE" } else { Write-Ok "bundle installed - AutoCAD picks it up on next launch" }
    }
}

# ── 5. MCP client config ────────────────────────────────────────────────────
Write-Step "MCP client configuration"

function Get-ClientConfigPath([string]$which) {
    switch ($which) {
        "cursor"        { return (Join-Path $env:USERPROFILE ".cursor\mcp.json") }
        "claude-desktop"{ return (Join-Path $env:APPDATA "Claude\claude_desktop_config.json") }
        "claude-code"   { return (Join-Path $RepoRoot ".mcp.json") }
    }
    return $null
}

if ($Client -eq "none") {
    Write-Info "skipped (-Client none)"
} else {
    $cfgPath = Get-ClientConfigPath $Client
    Write-Info "config: $cfgPath"

    $cfg = $null
    if (Test-Path $cfgPath) {
        $raw = Get-Content $cfgPath -Raw
        if ([string]::IsNullOrWhiteSpace($raw)) {
            $cfg = [PSCustomObject]@{ mcpServers = [PSCustomObject]@{} }
        } else {
            try { $cfg = $raw | ConvertFrom-Json }
            catch { Fail "Existing config is not valid JSON: $cfgPath`n$($_.Exception.Message)" 4 }
        }
    } else {
        $dir = Split-Path -Parent $cfgPath
        if ($dir -and -not (Test-Path $dir) -and -not $DryRun) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $cfg = [PSCustomObject]@{ mcpServers = [PSCustomObject]@{} }
    }
    if (-not $cfg.PSObject.Properties['mcpServers']) {
        Add-Member -InputObject $cfg -MemberType NoteProperty -Name mcpServers -Value ([PSCustomObject]@{}) -Force
    }

    $entry = [PSCustomObject]@{ command = $exe; args = @("--category", "router") }
    $existing = $cfg.mcpServers.PSObject.Properties['acad-router']
    $write = $true
    if ($existing) {
        $same = (($existing.Value | ConvertTo-Json -Depth 10 -Compress) -eq ($entry | ConvertTo-Json -Depth 10 -Compress))
        if ($same -and -not $Force) { Write-Info "acad-router already up to date"; $write = $false }
        else { $cfg.mcpServers.'acad-router' = $entry; Write-Ok "updated acad-router" }
    } else {
        Add-Member -InputObject $cfg.mcpServers -MemberType NoteProperty -Name 'acad-router' -Value $entry -Force
        Write-Ok "added acad-router"
    }

    # Nothing else from this repo belongs in a static client config.
    $stale = @($cfg.mcpServers.PSObject.Properties.Name | Where-Object { $_ -like 'acad-*' -and $_ -ne 'acad-router' })
    if ($stale.Count -gt 0) {
        Write-Warn ("Found {0} statically configured acad-* entries besides the router: {1}" -f $stale.Count, ($stale -join ", "))
        Write-Info "These should be discovered through MCP Nexus, not pinned here (invariant 5). Left in place - remove them yourself if they were not deliberate."
    }

    if ($write -and -not $DryRun) {
        if (Test-Path $cfgPath) {
            $backup = "$cfgPath.bak.$((Get-Date).ToString('yyyyMMdd-HHmmss'))"
            Copy-Item $cfgPath $backup
            Write-Info "backup: $backup"
        }
        # UTF8 without BOM - some MCP clients choke on a BOM.
        [System.IO.File]::WriteAllText($cfgPath, ($cfg | ConvertTo-Json -Depth 20), [System.Text.UTF8Encoding]::new($false))
        Write-Ok "written"
    } elseif ($write) {
        Write-Info "(dry run - not written)"
    }
}

# ── 6. Verify ───────────────────────────────────────────────────────────────
Write-Step "Verify"

if ($DryRun) {
    Write-Info "skipped (dry run)"
} elseif (-not (Test-Path $exe)) {
    Write-Warn "backend not built - nothing to verify"
} else {
    $acadRunning = @(Get-Process acad -ErrorAction SilentlyContinue).Count -gt 0
    if (-not $acadRunning) {
        Write-Info "AutoCAD is not running - skipping the live handshake."
        Write-Info "Start AutoCAD, then verify with: src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe --category router --ping-plugin"
    } else {
        # The backend logs to stderr. Under Windows PowerShell 5.1, redirecting a
        # native command's stderr while $ErrorActionPreference is 'Stop' turns every
        # log line into a terminating ErrorRecord, so the whole script dies on a
        # successful ping. Capture through a file instead of 2>&1.
        $pingLog = Join-Path ([System.IO.Path]::GetTempPath()) ("acadmcp-ping-{0}.log" -f [guid]::NewGuid())
        try {
            $proc = Start-Process -FilePath $exe `
                -ArgumentList @("--category", "router", "--ping-plugin") `
                -NoNewWindow -Wait -PassThru `
                -RedirectStandardError $pingLog -RedirectStandardOutput "$pingLog.out"
            $ping = ((Get-Content $pingLog -Raw -ErrorAction SilentlyContinue) + "`n" +
                     (Get-Content "$pingLog.out" -Raw -ErrorAction SilentlyContinue))
        } finally {
            Remove-Item $pingLog, "$pingLog.out" -Force -ErrorAction SilentlyContinue
        }
        if ($ping -match 'handshake OK') {
            Write-Ok "plugin handshake OK - the backend is talking to AutoCAD"
            if ($ping -match 'no-active-document') {
                Write-Info "No drawing is open. Open one, or let the agent call files.new_document."
            }
        } elseif ($ping -match 'pipe|Connecting') {
            Write-Warn "Could not reach the plugin over the named pipe."
            Write-Info "Most likely the bundle is installed but AutoCAD has not been restarted since. Restart AutoCAD and re-run this step."
            Write-Info "Inside AutoCAD, ACADMCP_PING should answer 'AcadMcp pong'."
        } else {
            Write-Warn "Unexpected ping output - run it by hand to see the detail."
        }
    }
}

# ── Summary ─────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  Done." -ForegroundColor White
if ($script:Warnings.Count -gt 0) {
    Write-Host ("  {0} warning(s) above." -f $script:Warnings.Count) -ForegroundColor Yellow
}
Write-Host ""
Write-Host "  Next:" -ForegroundColor White
if ($Client -ne "none") { Write-Host "    - restart $Client so it picks up acad-router" -ForegroundColor DarkGray }
Write-Host "    - MCP Nexus must also be configured for the other 30 categories to be discoverable:" -ForegroundColor DarkGray
Write-Host "      https://github.com/KrzysztofAugiewicz/MCPNexus" -ForegroundColor DarkGray
Write-Host "      then: powershell -ExecutionPolicy Bypass -File scripts\register-mcps.ps1" -ForegroundColor DarkGray
Write-Host "    - ask your client to call acad_status" -ForegroundColor DarkGray
Write-Host ""

exit 0
