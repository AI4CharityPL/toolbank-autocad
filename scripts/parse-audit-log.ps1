# Parse audit-script-log.txt (stdout from run-hospital-audit) into CSV/MD.
param(
    [string]$LogPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\reports\audit-script-log.txt'),
    [string]$ReportStem = 'HOSPITAL-AUDIT-2026-06-08',
    [double]$TolerancePct = 10.0
)
$repo = Split-Path -Parent $PSScriptRoot
$log = Get-Content -Path $LogPath -Raw -Encoding UTF8
$rows = New-Object System.Collections.Generic.List[object]
foreach ($m in [regex]::Matches($log, '\{"jsonrpc":"2\.0","id":2,"result":\{[^\n]+\}')) {
    try {
        $frame = $m.Value | ConvertFrom-Json
        $d = $frame.result.content[0].text | ConvertFrom-Json
        if (-not $d.found) { continue }
        $q = ($d.name -split "`r?`n")[0].Trim()
        $label = $d.labelAreaM2
        $measured = $d.areaM2
        $delta = if ($label -and $label -gt 0 -and $measured) { [math]::Abs($measured - $label) / $label * 100.0 } else { $null }
        $flags = @()
        if ($measured -and $label -and $measured -gt $label * 1.5) { $flags += 'leakSuspected' }
        if ($d.method -ne 'flood') { $flags += 'leakSuspected' }
        if ($delta -and $delta -gt $TolerancePct) { $flags += 'labelMismatch' }
        if ($d.doors.Count -eq 0 -and $d.name -notmatch 'KORYTARZ|STREFA|HOL|LOBBY|BOX') { $flags += 'emptyOpenings' }
        $icu = ($d.furniture | Where-Object { $_.blockName -match 'BED-ICU|BED-HOSP|HEADWALL' -or $_.type -in @('icu','medical') })
        if ($d.name -match 'KONFERENC|BIURO|OFFICE|SALA EDUK' -and $icu) { $flags += 'furnitureMismatch' }
        $rows.Add([pscustomobject]@{
            query = $q; name = ($d.name -replace "`r?`n", ' / '); labelM2 = $label; measuredM2 = $measured
            deltaPct = $delta; method = $d.method; doors = $d.doors.Count; windows = $d.windows.Count
            furniture = $d.furniture.Count; flags = ($flags -join '|')
        })
    } catch { }
}
$reportDir = Join-Path $repo 'docs\reports'
$csvPath = Join-Path $reportDir "$ReportStem.csv"
$mdPath = Join-Path $reportDir "$ReportStem.md"
$rows | Sort-Object query | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
$leaks = ($rows | Where-Object { $_.flags -match 'leakSuspected' }).Count
$mismatch = ($rows | Where-Object { $_.flags -match 'labelMismatch' }).Count
$emptyDoors = ($rows | Where-Object { $_.flags -match 'emptyOpenings' }).Count
$furn = ($rows | Where-Object { $_.flags -match 'furnitureMismatch' }).Count
$ok = ($rows | Where-Object { [string]::IsNullOrEmpty($_.flags) }).Count
@"
# Hospital2026 room audit ($ReportStem)

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm')

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

- **Critical**: leakSuspected + furnitureMismatch (A-304, B-304 corridor leaks)
- **Major**: labelMismatch > $TolerancePct%
- **Minor**: emptyOpenings

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
Write-Host "Parsed $($rows.Count) rows -> $mdPath"
