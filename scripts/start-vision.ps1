<#
    start-vision.ps1
    --------------------------------------------------------------------------
    Idempotent launcher for the AcadMcp.Vision Python sidecar.

    -EnsureRunning  : if no live PID + healthy /health, start the sidecar.
                      If a live PID exists, do nothing.
    -WaitHealthy    : block (up to 20 s) until /health returns 200.
    -Force          : kill any existing sidecar first, then start fresh.
    -Port <int>     : override port (default 50062). Persisted to vision.port.
    -Stop           : just stop the sidecar (kill PID + remove discovery files).

    Discovery files (rule 29 §3):
      %LOCALAPPDATA%\AcadMcp\vision.pid
      %LOCALAPPDATA%\AcadMcp\vision.port
#>
[CmdletBinding()]
param(
    [switch] $EnsureRunning,
    [switch] $WaitHealthy,
    [switch] $Force,
    [switch] $Stop,
    [int]    $Port = 50062
)

$ErrorActionPreference = 'Stop'

$RepoRoot      = Resolve-Path (Join-Path $PSScriptRoot '..') | ForEach-Object Path
$VisionDir     = Join-Path $RepoRoot 'src\AcadMcp.Vision'
$LocalAppData  = $env:LOCALAPPDATA
if (-not $LocalAppData) { $LocalAppData = Join-Path $HOME '.local\share' }
$StateDir      = Join-Path $LocalAppData 'AcadMcp'
$null          = New-Item -ItemType Directory -Force -Path $StateDir
$PidFile       = Join-Path $StateDir 'vision.pid'
$PortFile      = Join-Path $StateDir 'vision.port'
$LogDir        = Join-Path $StateDir 'logs'
$null          = New-Item -ItemType Directory -Force -Path $LogDir
$LogFile       = Join-Path $LogDir 'vision.log'
$ErrLogFile    = Join-Path $LogDir 'vision.err.log'

function Get-LiveSidecarPid {
    if (-not (Test-Path $PidFile)) { return $null }
    $raw = (Get-Content $PidFile -Raw -ErrorAction SilentlyContinue)
    if (-not $raw) { return $null }
    $pidVal = 0
    if (-not [int]::TryParse($raw.Trim(), [ref] $pidVal)) { return $null }
    $proc = Get-Process -Id $pidVal -ErrorAction SilentlyContinue
    if ($proc -and $proc.ProcessName -match 'python|acadmcp-vision') {
        return $pidVal
    }
    return $null
}

function Get-SidecarPort {
    if (Test-Path $PortFile) {
        $raw = (Get-Content $PortFile -Raw -ErrorAction SilentlyContinue)
        $portVal = 0
        if ($raw -and [int]::TryParse($raw.Trim(), [ref] $portVal)) { return $portVal }
    }
    return $Port
}

function Test-SidecarHealth {
    param([int] $TestPort)
    try {
        $r = Invoke-WebRequest -Uri ("http://127.0.0.1:{0}/health" -f $TestPort) `
                               -UseBasicParsing -TimeoutSec 2
        return $r.StatusCode -eq 200
    } catch {
        return $false
    }
}

function Stop-Sidecar {
    $existingPid = Get-LiveSidecarPid
    if ($existingPid) {
        Write-Host "Stopping vision sidecar PID $existingPid..."
        Stop-Process -Id $existingPid -Force -ErrorAction SilentlyContinue
    }
    Remove-Item $PidFile  -Force -ErrorAction SilentlyContinue
    Remove-Item $PortFile -Force -ErrorAction SilentlyContinue
}

if ($Stop) { Stop-Sidecar; return }

if ($Force) { Stop-Sidecar }

$existingPid = Get-LiveSidecarPid
if ($existingPid -and -not $Force) {
    $existingPort = Get-SidecarPort
    if (Test-SidecarHealth -TestPort $existingPort) {
        Write-Host ("Vision sidecar already running PID {0} on port {1}." -f $existingPid, $existingPort)
        return
    } else {
        Write-Warning "Stale PID file - sidecar PID exists but /health failed. Restarting."
        Stop-Sidecar
    }
}

if (-not $EnsureRunning -and -not $Force) {
    Write-Error "Sidecar not running. Pass -EnsureRunning to start it."
    return
}

$python = $null
foreach ($cmd in @('python','python3','py')) {
    if (Get-Command $cmd -ErrorAction SilentlyContinue) { $python = $cmd; break }
}
if (-not $python) {
    Write-Error "No python interpreter found in PATH. Install Python 3.11+ first."
    return
}

Write-Host ("Starting AcadMcp.Vision sidecar on port {0}..." -f $Port)

$args = @(
    '-m', 'acadmcp_vision.app',
    '--http-port', $Port,
    '--host', '127.0.0.1'
)

$proc = Start-Process -FilePath $python `
                      -ArgumentList $args `
                      -WorkingDirectory $VisionDir `
                      -WindowStyle Hidden `
                      -RedirectStandardOutput $LogFile `
                      -RedirectStandardError  $ErrLogFile `
                      -PassThru
Set-Content -Path $PidFile  -Value $proc.Id     -NoNewline -Encoding ascii
Set-Content -Path $PortFile -Value $Port        -NoNewline -Encoding ascii

if ($WaitHealthy) {
    $deadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 400
        if (Test-SidecarHealth -TestPort $Port) {
            Write-Host ("Vision sidecar healthy: http://127.0.0.1:{0}/health" -f $Port)
            return
        }
    } while ((Get-Date) -lt $deadline)
    Write-Error "Vision sidecar failed to become healthy within 20 s. Check $LogFile."
    return
}

Write-Host ("Vision sidecar starting in background, log: {0}" -f $LogFile)
