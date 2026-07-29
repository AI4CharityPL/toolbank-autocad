# Temporary ad-hoc smoke script for live router ↔ plugin calls.
# Will be absorbed into scripts/e2e-smoke.ps1 once Phase 7.0 lands.
param(
    [string]$Tool = "acad_status",
    [string]$ArgsJson = "{}",
    [int]$TimeoutMs = 600000
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo "src\AcadMcp.Backend\bin\Debug\net8.0\AcadMcp.Backend.exe"
if (-not (Test-Path $exe)) { throw "Backend exe missing: $exe" }

$req1 = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"0.0"}}}'
$req2 = '{"jsonrpc":"2.0","method":"notifications/initialized"}'
$req3 = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"' + $Tool + '","arguments":' + $ArgsJson + '}}'
$req4 = '{"jsonrpc":"2.0","id":3,"method":"shutdown","params":{}}'

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $exe
$psi.Arguments = "--category router"
$psi.RedirectStandardInput  = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError  = $true
$psi.UseShellExecute = $false
$psi.WorkingDirectory = $repo

$proc = [System.Diagnostics.Process]::Start($psi)
$proc.StandardInput.WriteLine($req1)
$proc.StandardInput.WriteLine($req2)
$proc.StandardInput.WriteLine($req3)
$proc.StandardInput.WriteLine($req4)
$proc.StandardInput.Close()

if (-not $proc.WaitForExit($TimeoutMs)) {
    try { $proc.Kill() } catch {}
    throw "router process hung after $TimeoutMs ms"
}
$stdout = $proc.StandardOutput.ReadToEnd()
$stderr = $proc.StandardError.ReadToEnd()

Write-Host "--- STDOUT ---"
Write-Host $stdout
if ($stderr) { Write-Host "--- STDERR ---"; Write-Host $stderr }
