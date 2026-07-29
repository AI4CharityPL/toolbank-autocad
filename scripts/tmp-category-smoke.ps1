# Ad-hoc tool caller: spawns AcadMcp.Backend --category <name> on stdio
# and runs a single tools/call. Dev-only, analogous to tmp-router-smoke.ps1.
param(
    [Parameter(Mandatory = $true)][string]$Category,
    [Parameter(Mandatory = $true)][string]$Tool,
    [string]$ArgsJson = '{}',
    [int]$TimeoutMs = 600000
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dllRelease = Join-Path $repo "src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.dll"
$dllDebug   = Join-Path $repo "src\AcadMcp.Backend\bin\Debug\net8.0\AcadMcp.Backend.dll"
$dll = if (Test-Path $dllRelease) { $dllRelease } else { $dllDebug }
if (-not (Test-Path $dll)) { throw "AcadMcp.Backend.dll not found. Run: dotnet build src\AcadMcp.sln -c Release" }

$req1 = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"0.1"}}}'
$req2 = '{"jsonrpc":"2.0","method":"notifications/initialized"}'
$req3 = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"' + $Tool + '","arguments":' + $ArgsJson + '}}'
$req4 = '{"jsonrpc":"2.0","id":3,"method":"shutdown","params":{}}'
$req5 = '{"jsonrpc":"2.0","method":"exit"}'

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "`"$dll`" --category $Category"
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
$proc.StandardInput.WriteLine($req5)
$proc.StandardInput.Close()

if (-not $proc.WaitForExit($TimeoutMs)) {
    try { $proc.Kill() } catch {}
    throw "category '$Category' process hung after $TimeoutMs ms (tool=$Tool)"
}
$stdout = $proc.StandardOutput.ReadToEnd()
$stderr = $proc.StandardError.ReadToEnd()

Write-Host '--- STDOUT ---'
Write-Host $stdout
if ($stderr) { Write-Host '--- STDERR ---'; Write-Host $stderr }
