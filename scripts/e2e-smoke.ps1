# E2E regression smoke test for AcadMcp.Backend (all 19 MCP servers).
#
# For each category it spawns `AcadMcp.Backend.exe --category <name>` on stdio,
# runs `initialize -> notifications/initialized -> tools/list -> shutdown -> exit`,
# reads stdout line-by-line looking for the tools/list response (id=2) and asserts
# the tool count matches the ToolBank manifest. The process is killed once the
# expected response arrives (we don't rely on graceful exit; on Windows redirected
# stdin closure is sometimes not observed by the child before we want to move on).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/e2e-smoke.ps1            # all categories
#   powershell -ExecutionPolicy Bypass -File scripts/e2e-smoke.ps1 -Live      # + router/plugin checks
#   powershell -ExecutionPolicy Bypass -File scripts/e2e-smoke.ps1 -Category civil
#
# Exits 0 on full pass, 1 otherwise.

param(
    [string]$Category = "",
    [switch]$Live,
    [int]$TimeoutMs = 8000
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe  = Join-Path $repo "src\AcadMcp.Backend\bin\Debug\net8.0\AcadMcp.Backend.exe"
$manifests = Join-Path $repo "toolbank-manifests"

if (-not (Test-Path $exe)) {
    Write-Error "Backend exe missing: $exe. Run `dotnet build src\AcadMcp.sln` first."
    exit 1
}
if (-not (Test-Path $manifests)) {
    Write-Error "Manifest folder missing: $manifests"
    exit 1
}

function Get-ExpectedCounts {
    $map = @{}
    Get-ChildItem $manifests -Filter 'acad-*.json' | ForEach-Object {
        $j = Get-Content $_.FullName -Raw | ConvertFrom-Json
        $count = 0
        if ($j.tools_summary) { $count = @($j.tools_summary).Count }
        $id = $_.BaseName -replace '^acad-', ''
        $map[$id] = $count
    }
    return $map
}

function Invoke-CategoryStdio {
    param([string]$Cat)

    $req1 = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"e2e-smoke","version":"0.1"}}}'
    $req2 = '{"jsonrpc":"2.0","method":"notifications/initialized"}'
    $req3 = '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
    $req4 = '{"jsonrpc":"2.0","id":3,"method":"shutdown","params":{}}'
    $req5 = '{"jsonrpc":"2.0","method":"exit"}'

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = "--category $Cat"
    $psi.RedirectStandardInput  = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $repo

    $proc = [System.Diagnostics.Process]::Start($psi)
    try {
        $proc.StandardInput.WriteLine($req1)
        $proc.StandardInput.WriteLine($req2)
        $proc.StandardInput.WriteLine($req3)
        $proc.StandardInput.WriteLine($req4)
        $proc.StandardInput.WriteLine($req5)
        $proc.StandardInput.Flush()
    } catch { }

    # Read stdout line-by-line until we see the tools/list response (id=2), or we hit the timeout.
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $toolCount = -1
    $allLines = New-Object System.Text.StringBuilder
    while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
        if ($proc.StandardOutput.Peek() -lt 0 -and $proc.HasExited) { break }
        $line = $null
        try { $line = $proc.StandardOutput.ReadLine() } catch { break }
        if ($null -eq $line) { Start-Sleep -Milliseconds 50; continue }
        [void]$allLines.AppendLine($line)
        $trimmed = $line.Trim()
        if ($trimmed.StartsWith('{')) {
            try {
                $obj = $trimmed | ConvertFrom-Json -ErrorAction Stop
                if ($obj.id -eq 2 -and $obj.result) {
                    if ($obj.result.tools) { $toolCount = @($obj.result.tools).Count }
                    else { $toolCount = 0 }
                    break
                }
            } catch { }
        }
    }

    try { if (-not $proc.HasExited) { $proc.Kill() } } catch { }
    try { $proc.WaitForExit(2000) | Out-Null } catch { }

    $stderr = ""
    try { $stderr = $proc.StandardError.ReadToEnd() } catch { }
    return @{ toolCount = $toolCount; stdout = $allLines.ToString(); stderr = $stderr }
}

$expected = Get-ExpectedCounts
$targets = if ($Category) { @($Category) } else { $expected.Keys | Sort-Object }

$failed = @()
$passed = @()

Write-Host "== AcadMcp E2E stdio smoke =="
foreach ($cat in $targets) {
    if (-not $expected.ContainsKey($cat)) {
        Write-Host "  [skip] $cat (no manifest)" -ForegroundColor Yellow
        continue
    }
    $exp = $expected[$cat]
    $label = "  $cat".PadRight(24)
    $res = Invoke-CategoryStdio -Cat $cat
    if ($res.toolCount -lt 0) {
        Write-Host "$label FAIL (no tools/list response in $TimeoutMs ms)" -ForegroundColor Red
        $failed += $cat
    }
    elseif ($res.toolCount -ne $exp) {
        Write-Host "$label FAIL (got $($res.toolCount), expected $exp)" -ForegroundColor Red
        $failed += $cat
    }
    else {
        Write-Host "$label ok ($($res.toolCount) tools)" -ForegroundColor Green
        $passed += $cat
    }
}

if ($Live) {
    Write-Host ""
    Write-Host "== Live router -> plugin checks =="
    $smoke = Join-Path $PSScriptRoot 'tmp-router-smoke.ps1'
    if (Test-Path $smoke) {
        $out = (& powershell -ExecutionPolicy Bypass -File $smoke -Tool acad_status) 2>&1 | Out-String
        # acad_status returns its JSON nested inside MCP content text, which is
        # typically re-serialized as "\u0022alive\u0022: true". Match either form.
        if ($out -notmatch '(?:\\u0022alive\\u0022|"alive")\s*:\s*true') {
            Write-Host "  acad_status FAIL" -ForegroundColor Red
            Write-Host $out
            $failed += "router-live-status"
        } else {
            Write-Host "  acad_status ok" -ForegroundColor Green
            $passed += "router-live-status"
        }
    } else {
        Write-Host "  tmp-router-smoke.ps1 not found; skipping live checks" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "== Summary =="
Write-Host "  passed: $($passed.Count) / $($passed.Count + $failed.Count)"
if ($failed.Count -gt 0) {
    Write-Host "  failed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "  All categories green."
exit 0
