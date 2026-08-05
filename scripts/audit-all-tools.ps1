#
# scripts/audit-all-tools.ps1
#
# Full-sweep tool reachability / input-validation audit.
#
# For each AcadMcp category (excluding router which is audited separately):
#   1. Launch AcadMcp.Backend.exe --category <cat> on stdio.
#   2. Initialize MCP, list tools.
#   3. For every tool: call tools/call with args={} (empty object).
#   4. Classify the response:
#       - result, no isError           => PASS        (executed with empty args)
#       - result with isError=true AND error text mentions "required" / "missing" / "cannot be null"
#                                      => VALIDATES   (tool reachable, rejected bad args)
#       - result with isError=true other => ERROR     (tool reachable, but failed with message)
#       - JSON-RPC error response         => RPC-ERROR
#       - no response in time             => HANG
#
# Output:
#   docs/TOOL-AUDIT-2026-04-23.md        — per-category markdown table
#   docs/tool-audit-raw.json             — raw per-tool records (for downstream analysis)
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/audit-all-tools.ps1
#   powershell -ExecutionPolicy Bypass -File scripts/audit-all-tools.ps1 -Category openings
#

param(
    [string]$Category = "",
    [int]$TimeoutMs = 45000,
    [int]$PerCallTimeoutMs = 12000,
    [switch]$Release
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$config = if ($Release) { 'Release' } else { 'Debug' }
$exe = Join-Path $repo "src\AcadMcp.Backend\bin\$config\net8.0\AcadMcp.Backend.exe"
$manifests = Join-Path $repo "toolbank-manifests"

if (-not (Test-Path $exe)) { throw "Backend exe missing: $exe. Run dotnet build src\AcadMcp.sln -c $config." }

# --- per-category stdio driver ---------------------------------------------

function Invoke-ToolsList {
    param([System.Diagnostics.Process]$Proc, [int]$Timeout)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $Timeout) {
        $line = $null
        try { $line = $Proc.StandardOutput.ReadLine() } catch { break }
        if ($null -eq $line) { Start-Sleep -Milliseconds 30; continue }
        $trimmed = $line.Trim()
        if (-not $trimmed.StartsWith('{')) { continue }
        try {
            $obj = $trimmed | ConvertFrom-Json -ErrorAction Stop
            if ($obj.id -eq 2 -and $obj.result -and $obj.result.tools) {
                return @($obj.result.tools)
            }
        } catch { }
    }
    return @()
}

function Invoke-ToolCall {
    param([System.Diagnostics.Process]$Proc, [int]$Id, [string]$ToolName, [int]$Timeout)
    $req = '{"jsonrpc":"2.0","id":' + $Id + ',"method":"tools/call","params":{"name":"' + $ToolName + '","arguments":{}}}'
    $Proc.StandardInput.WriteLine($req)
    $Proc.StandardInput.Flush()

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $Timeout) {
        $line = $null
        try { $line = $Proc.StandardOutput.ReadLine() } catch { break }
        if ($null -eq $line) { Start-Sleep -Milliseconds 10; continue }
        $trimmed = $line.Trim()
        if (-not $trimmed.StartsWith('{')) { continue }
        try {
            $obj = $trimmed | ConvertFrom-Json -ErrorAction Stop
            if ($obj.id -eq $Id) { return $obj }
        } catch { }
    }
    return $null
}

function Classify-Response {
    param($Resp)
    if ($null -eq $Resp) { return @{ status = 'HANG'; message = 'no response in time window' } }
    if ($Resp.PSObject.Properties.Name -contains 'error' -and $Resp.error) {
        return @{ status = 'RPC-ERROR'; message = ([string]$Resp.error.message) }
    }
    if ($Resp.result -and $Resp.result.isError -eq $true) {
        $msg = ''
        if ($Resp.result.content) {
            foreach ($c in $Resp.result.content) { if ($c.text) { $msg += $c.text } }
        }
        $l = $msg.ToLowerInvariant()
        $looksValidation = ($l -match 'required') -or ($l -match 'missing') -or ($l -match 'cannot be null') -or ($l -match 'invalid') -or ($l -match 'validation') -or ($l -match 'empty') -or ($l -match 'must be') -or ($l -match 'expected')
        if ($looksValidation) { return @{ status = 'VALIDATES'; message = $msg } }
        return @{ status = 'ERROR'; message = $msg }
    }
    # success
    $short = ''
    if ($Resp.result.content -and $Resp.result.content[0].text) {
        $short = ($Resp.result.content[0].text.Substring(0, [Math]::Min(160, $Resp.result.content[0].text.Length)))
    }
    return @{ status = 'PASS'; message = $short }
}

function Start-CategoryProc {
    param([string]$Cat)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = "--category $Cat"
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $repo
    $p = [System.Diagnostics.Process]::Start($psi)
    try {
        $p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tool-audit","version":"0.1"}}}')
        $p.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
        $p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}')
        $p.StandardInput.Flush()
    } catch { }
    return $p
}

function Stop-CategoryProc {
    param([System.Diagnostics.Process]$P)
    try {
        $P.StandardInput.WriteLine('{"jsonrpc":"2.0","id":99,"method":"shutdown","params":{}}')
        $P.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"exit"}')
        $P.StandardInput.Flush()
    } catch { }
    try { if (-not $P.HasExited) { Start-Sleep -Milliseconds 150; if (-not $P.HasExited) { $P.Kill() } } } catch { }
}

function Run-Category {
    param([string]$Cat)
    $proc = Start-CategoryProc -Cat $Cat
    $tools = Invoke-ToolsList -Proc $proc -Timeout $TimeoutMs
    $results = @()
    $idCounter = 10
    foreach ($t in $tools) {
        $idCounter++
        $resp = Invoke-ToolCall -Proc $proc -Id $idCounter -ToolName $t.name -Timeout $PerCallTimeoutMs
        $cls = Classify-Response -Resp $resp
        $results += [pscustomobject]@{
            category = $Cat
            tool = $t.name
            status = $cls.status
            message = ($cls.message -replace '[\r\n]+', ' ').Trim()
        }
        # Recover from HANG: restart the backend process so downstream tools get a fair chance.
        if ($cls.status -eq 'HANG') {
            Stop-CategoryProc -P $proc
            $proc = Start-CategoryProc -Cat $Cat
            [void](Invoke-ToolsList -Proc $proc -Timeout $TimeoutMs)
            $idCounter = 10
        }
    }
    Stop-CategoryProc -P $proc
    return $results
}

# --- category list ---------------------------------------------------------
$allCats = Get-ChildItem $manifests -Filter 'acad-*.json' | ForEach-Object {
    ($_.BaseName -replace '^acad-', '')
} | Where-Object { $_ -ne 'router' } | Sort-Object

$targets = if ($Category) { @($Category) } else { $allCats }

$allResults = @()
Write-Host "== AcadMcp full tool audit ==" -ForegroundColor Cyan
Write-Host "  categories: $($targets.Count)"
foreach ($cat in $targets) {
    Write-Host ("  [{0}] running ..." -f $cat) -NoNewline
    $r = Run-Category -Cat $cat
    $pass = ($r | Where-Object { $_.status -eq 'PASS' }).Count
    $val = ($r | Where-Object { $_.status -eq 'VALIDATES' }).Count
    $err = ($r | Where-Object { $_.status -eq 'ERROR' }).Count
    $rpc = ($r | Where-Object { $_.status -eq 'RPC-ERROR' }).Count
    $hang = ($r | Where-Object { $_.status -eq 'HANG' }).Count
    Write-Host ((" {0} tools | PASS={1} VAL={2} ERR={3} RPC={4} HANG={5}") -f ([int]$r.Count), ([int]$pass), ([int]$val), ([int]$err), ([int]$rpc), ([int]$hang))
    $allResults += $r
}

# --- write outputs ---------------------------------------------------------
$docsDir = Join-Path $repo 'docs'
if (-not (Test-Path $docsDir)) { New-Item -ItemType Directory -Path $docsDir | Out-Null }
$rawJson = Join-Path $docsDir 'tool-audit-raw.json'
$allResults | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $rawJson -Encoding UTF8

$md = Join-Path $docsDir 'TOOL-AUDIT-2026-04-23.md'
$sb = New-Object System.Text.StringBuilder
$totalTools = $allResults.Count
$totalPass = ($allResults | Where-Object { $_.status -eq 'PASS' }).Count
$totalVal = ($allResults | Where-Object { $_.status -eq 'VALIDATES' }).Count
$totalErr = ($allResults | Where-Object { $_.status -eq 'ERROR' }).Count
$totalRpc = ($allResults | Where-Object { $_.status -eq 'RPC-ERROR' }).Count
$totalHang = ($allResults | Where-Object { $_.status -eq 'HANG' }).Count

[void]$sb.AppendLine('# AutoCAD MCP — full tool audit (2026-04-23)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine("**Total tools audited: $totalTools** across $($targets.Count) categories.")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Status | Count | Meaning |')
[void]$sb.AppendLine('|--------|------:|---------|')
[void]$sb.AppendLine("| PASS | $totalPass | Tool executed successfully with empty args (read-only / optional-only). |")
[void]$sb.AppendLine("| VALIDATES | $totalVal | Tool is reachable and correctly rejected empty args with a validation error. |")
[void]$sb.AppendLine("| ERROR | $totalErr | Tool is reachable but returned a runtime error that is NOT an input-validation message. |")
[void]$sb.AppendLine("| RPC-ERROR | $totalRpc | JSON-RPC protocol error (unreachable or registration problem - REAL BUGS). |")
[void]$sb.AppendLine("| HANG | $totalHang | No response within timeout - backend hung or deadlocked. |")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Summary per category')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Category | Tools | PASS | VALIDATES | ERROR | RPC-ERROR | HANG |')
[void]$sb.AppendLine('|----------|------:|-----:|----------:|------:|----------:|-----:|')
foreach ($cat in $targets) {
    $r = $allResults | Where-Object { $_.category -eq $cat }
    $p = ($r | Where-Object { $_.status -eq 'PASS' }).Count
    $v = ($r | Where-Object { $_.status -eq 'VALIDATES' }).Count
    $e = ($r | Where-Object { $_.status -eq 'ERROR' }).Count
    $rp = ($r | Where-Object { $_.status -eq 'RPC-ERROR' }).Count
    $h = ($r | Where-Object { $_.status -eq 'HANG' }).Count
    [void]$sb.AppendLine("| $cat | $($r.Count) | $p | $v | $e | $rp | $h |")
}
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Per-tool detail')
[void]$sb.AppendLine('')
foreach ($cat in $targets) {
    $r = $allResults | Where-Object { $_.category -eq $cat }
    [void]$sb.AppendLine("### $cat ($($r.Count) tools)")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('| Tool | Status | Message |')
    [void]$sb.AppendLine('|------|--------|---------|')
    foreach ($row in $r) {
        $msg = $row.message
        if ($msg.Length -gt 120) { $msg = $msg.Substring(0, 117) + '...' }
        $msg = $msg -replace '\|', '\|'
        [void]$sb.AppendLine("| `$($row.tool)` | $($row.status) | $msg |")
    }
    [void]$sb.AppendLine('')
}
Set-Content -LiteralPath $md -Value $sb.ToString() -Encoding UTF8

Write-Host ''
Write-Host "== Summary ==" -ForegroundColor Cyan
Write-Host ("  total: {0}   PASS: {1}   VALIDATES: {2}   ERROR: {3}   RPC-ERROR: {4}   HANG: {5}" -f $totalTools, $totalPass, $totalVal, $totalErr, $totalRpc, $totalHang)
Write-Host ("  report: {0}" -f $md)
Write-Host ("  raw:    {0}" -f $rawJson)
