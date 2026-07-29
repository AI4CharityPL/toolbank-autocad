# Batch room audit for Hospital2026 using get_room_data (works without router restart).
# Writes CSV + Markdown under docs/reports/.
param(
    [string]$ReportStem = "HOSPITAL-AUDIT-2026-06-08",
    [double]$TolerancePct = 10.0
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$smoke = Join-Path $PSScriptRoot 'tmp-category-smoke.ps1'
$labelsJson = '{"allLayers":false,"labelLayers":["A-AREA-IDEN","A-ANNO-ROOM"]}'

function Invoke-CategoryTool([string]$Tool, [string]$ArgsJson, [int]$TimeoutMs = 120000) {
    $raw = & $smoke -Category schedules -Tool $Tool -ArgsJson $ArgsJson -TimeoutMs $TimeoutMs 2>&1 | Out-String
    $jsonLine = ($raw -split "`n" | Where-Object { $_ -match '^\{"jsonrpc"' -and $_ -match '"id"\s*:\s*2' } | Select-Object -First 1)
    if (-not $jsonLine) { throw "No id=2 response for $Tool. Output: $($raw.Substring(0, [Math]::Min(500, $raw.Length)))" }
    $frame = $jsonLine | ConvertFrom-Json
    $text = $frame.result.content[0].text
    return $text | ConvertFrom-Json
}

Write-Host "Scanning room labels via get_room_data..."
$queries = @(
    'A-001','A-002','A-003','A-101','A-102','A-103','A-104','A-105','A-201','A-202','A-203',
    'A-301','A-302','A-303','A-304','A-305','A-401','A-402','A-403','A-404',
    'B-101','B-102','B-201','B-202','B-203','B-204','B-205','B-220','B-221','B-222','B-225',
    'B-301','B-302','B-303','B-304','B-401','B-402','B-410','B-411','B-421','B-422','B-423',
    'B-501','B-502','B-503','B-504','B-520','B-601','B-602','B-603','B-604','B-605','B-606'
)

$rows = New-Object System.Collections.Generic.List[object]
foreach ($q in $queries) {
    Write-Host "  $q ..."
    try {
        Start-Sleep -Milliseconds 500
        $argsObj = @{ query = $q; allLayers = $false; labelLayers = @('A-AREA-IDEN') }
        $argsJson = ($argsObj | ConvertTo-Json -Compress)
        $d = Invoke-CategoryTool 'get_room_data' $argsJson 90000
        if (-not $d.found) { continue }
        $label = $d.labelAreaM2
        $measured = $d.areaM2
        $delta = if ($label -and $label -gt 0 -and $measured) { [math]::Abs($measured - $label) / $label * 100.0 } else { $null }
        $flags = @()
        if ($measured -and $label -and $measured -gt $label * 1.5) { $flags += 'leakSuspected' }
        if ($d.method -ne 'flood') { $flags += 'leakSuspected' }
        if ($delta -and $delta -gt $TolerancePct) { $flags += 'labelMismatch' }
        if ($d.doors.Count -eq 0 -and $d.name -notmatch 'KORYTARZ|STREFA|HOL|LOBBY') { $flags += 'emptyOpenings' }
        $icu = ($d.furniture | Where-Object { $_.blockName -match 'BED-ICU|BED-HOSP|HEADWALL' -or $_.type -in @('icu','medical') })
        if ($d.name -match 'KONFERENC|BIURO|OFFICE|SALA EDUK' -and $icu) { $flags += 'furnitureMismatch' }
        $rows.Add([pscustomobject]@{
            query = $q; name = ($d.name -replace "`r?`n", ' / '); labelM2 = $label; measuredM2 = $measured
            deltaPct = $delta; method = $d.method; doors = $d.doors.Count; windows = $d.windows.Count
            furniture = $d.furniture.Count; flags = ($flags -join '|')
        })
    } catch {
        Write-Warning "$q failed: $_"
    }
}

$reportDir = Join-Path $repo 'docs\reports'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$csvPath = Join-Path $reportDir "$ReportStem.csv"
$mdPath = Join-Path $reportDir "$ReportStem.md"
$rows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

$leaks = ($rows | Where-Object { $_.flags -match 'leakSuspected' }).Count
$mismatch = ($rows | Where-Object { $_.flags -match 'labelMismatch' }).Count
$emptyDoors = ($rows | Where-Object { $_.flags -match 'emptyOpenings' }).Count
$furn = ($rows | Where-Object { $_.flags -match 'furnitureMismatch' }).Count
$ok = ($rows | Where-Object { $_.flags -eq '' }).Count

@"
# Hospital2026 room audit ($ReportStem)

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm') UTC+local

## Summary

| Metric | Count |
|--------|------:|
| Total rooms scanned | $($rows.Count) |
| OK (no flags) | $ok |
| labelMismatch | $mismatch |
| leakSuspected | $leaks |
| emptyOpenings | $emptyDoors |
| furnitureMismatch | $furn |

## Priority fixes

- **Critical**: leakSuspected + furnitureMismatch (A-304 ICU bed, B-304 corridor leak)
- **Major**: labelMismatch > $TolerancePct%
- **Minor**: emptyOpenings (missing doors in non-corridor rooms)

## Detail

| Room | Label m² | Measured m² | Δ% | Method | Drzwi | Okna | Meble | Flagi |
|------|---------:|------------:|---:|--------|------:|-----:|------:|-------|
$(
($rows | Sort-Object query | ForEach-Object {
    "| $($_.query) | $($_.labelM2) | $([math]::Round($_.measuredM2,1)) | $([math]::Round($_.deltaPct,1)) | $($_.method) | $($_.doors) | $($_.windows) | $($_.furniture) | $($_.flags) |"
}) -join "`n"
)

CSV: ``$csvPath``
"@ | Set-Content -Path $mdPath -Encoding UTF8

Write-Host "Wrote $mdPath and $csvPath ($($rows.Count) rows)"
