# Hospital2026 room audit (HOSPITAL-AUDIT-2026-06-08)

Generated: 2026-06-08 22:53

## Summary

| Metric | Count |
|--------|------:|
| Total rooms scanned | 53 |
| OK (no flags) | 2 |
| labelMismatch | 30 |
| leakSuspected | 28 |
| emptyOpenings | 46 |
| furnitureMismatch | 2 |

## Priority fixes

- **Critical**: leakSuspected + furnitureMismatch (A-304, B-304 corridor leaks)
- **Major**: labelMismatch > 10%
- **Minor**: emptyOpenings

## Detail

| Room | Label mÂ˛ | Measured mÂ˛ | Î”% | Method | Drzwi | Okna | Meble | Flagi |
|------|---------:|------------:|---:|--------|------:|-----:|------:|-------|
| A-001 | 150 | 134.4 | 10.4 | flood | 0 | 0 | 4 | labelMismatch|emptyOpenings |
| A-002 | 100 | 93.0 | 7.0 | flood | 0 | 0 | 2 | emptyOpenings |
| A-003 | 276 | 220.8 | 20.0 | raycast | 0 | 0 | 5 | leakSuspected|labelMismatch |
| A-101 | 64 | 136 | 112.5 | raycast | 0 | 0 | 0 | leakSuspected|leakSuspected|labelMismatch|emptyOpenings |
| A-102 | 56 | 119 | 112.5 | raycast | 0 | 0 | 0 | leakSuspected|leakSuspected|labelMismatch|emptyOpenings |
| A-103 | 48 | 98.8 | 105.8 | raycast | 0 | 0 | 0 | leakSuspected|leakSuspected|labelMismatch|emptyOpenings |
| A-103 | 48 | 221 | 360.4 | raycast | 0 | 0 | 0 | leakSuspected|leakSuspected|labelMismatch|emptyOpenings |
| A-105 | 56 | 53.2 | 5.0 | raycast | 0 | 0 | 0 | leakSuspected|emptyOpenings |
| A-201 | 96 | 88.9 | 7.4 | flood | 0 | 0 | 1 | emptyOpenings |
| A-202 | 72 | 67.0 | 7.0 | flood | 0 | 0 | 2 | emptyOpenings |
| A-203 | 112 | 106.4 | 5.0 | raycast | 0 | 0 | 3 | leakSuspected|emptyOpenings |
| A-301 |  | 13.8 | 0 | flood | 0 | 0 | 0 | emptyOpenings |
| A-302 |  | 12.5 | 0 | flood | 0 | 0 | 0 | emptyOpenings |
| A-303 |  | 40.6 | 0 | flood | 0 | 0 | 1 | emptyOpenings |
| A-304 | 200 | 184.5 | 7.7 | flood | 0 | 0 | 4 | emptyOpenings|furnitureMismatch |
| A-305 | 200 | 183.3 | 8.4 | flood | 0 | 0 | 4 | emptyOpenings|furnitureMismatch |
| A-401 | 104 | 95.5 | 8.2 | flood | 0 | 0 | 3 | emptyOpenings |
| A-402 | 104 | 97.1 | 6.6 | flood | 0 | 0 | 2 | emptyOpenings |
| A-403 | 104 | 83.8 | 19.4 | flood | 0 | 0 | 4 | labelMismatch|emptyOpenings |
| A-404 | 104 | 67.7 | 34.9 | flood | 0 | 0 | 0 | labelMismatch|emptyOpenings |
| B-101 | 48 | 43.6 | 9.1 | flood | 0 | 0 | 3 | emptyOpenings |
| B-102 | 80 | 129.5 | 61.9 | raycast | 0 | 0 | 5 | leakSuspected|leakSuspected|labelMismatch|emptyOpenings |
| B-102 | 80 | 229.0 | 186.3 | flood | 0 | 0 | 6 | leakSuspected|labelMismatch|emptyOpenings |
| B-201 |  | 7.7 | 0 | flood | 0 | 0 | 0 |  |
| B-202 | 34 | 297.6 | 775.3 | flood | 0 | 0 | 5 | leakSuspected|labelMismatch |
| B-202 | 34 | 297.6 | 775.3 | flood | 0 | 0 | 5 | leakSuspected|labelMismatch |
| B-202 | 34 | 297.6 | 775.3 | flood | 0 | 0 | 5 | leakSuspected|labelMismatch |
| B-203 |  | 12.2 | 0 | flood | 0 | 0 | 1 |  |
| B-204 | 52.5 | 82.2 | 56.7 | raycast | 0 | 0 | 2 | leakSuspected|leakSuspected|labelMismatch |
| B-205 | 52 | 39.0 | 25.1 | flood | 0 | 0 | 0 | labelMismatch|emptyOpenings |
| B-220 | 40 | 60 | 50 | raycast | 0 | 0 | 1 | leakSuspected|labelMismatch|emptyOpenings |
| B-220 | 28 | 158.9 | 467.3 | flood | 0 | 0 | 6 | leakSuspected|labelMismatch|emptyOpenings |
| B-220 | 28 | 158.9 | 467.3 | flood | 0 | 0 | 6 | leakSuspected|labelMismatch|emptyOpenings |
| B-221 | 28 | 82.5 | 194.5 | flood | 0 | 0 | 1 | leakSuspected|labelMismatch|emptyOpenings |
| B-221 | 28 | 82.5 | 194.5 | flood | 0 | 0 | 1 | leakSuspected|labelMismatch|emptyOpenings |
| B-222 | 28 | 106.4 | 280.0 | raycast | 0 | 0 | 5 | leakSuspected|leakSuspected|labelMismatch|emptyOpenings |
| B-301 | 34 | 46.8 | 37.6 | raycast | 0 | 0 | 0 | leakSuspected|labelMismatch|emptyOpenings |
| B-302 | 34 | 28.4 | 16.4 | flood | 0 | 0 | 0 | labelMismatch|emptyOpenings |
| B-303 | 34 | 32.6 | 4.0 | flood | 0 | 0 | 1 | emptyOpenings |
| B-304 | 34 | 150.8 | 343.5 | raycast | 0 | 0 | 1 | leakSuspected|leakSuspected|labelMismatch|emptyOpenings |
| B-401 | 81 | 80.1 | 1.1 | flood | 0 | 0 | 1 | emptyOpenings |
| B-402 | 36 | 34.7 | 3.7 | flood | 0 | 0 | 0 | emptyOpenings |
| B-410 | 80 | 71.8 | 10.2 | flood | 0 | 0 | 2 | labelMismatch|emptyOpenings |
| B-421 | 56 | 54.7 | 2.2 | flood | 0 | 0 | 4 | emptyOpenings |
| B-422 | 56 | 53.7 | 4.1 | flood | 0 | 0 | 3 | emptyOpenings |
| B-502 | 52.5 | 46.5 | 11.4 | flood | 0 | 0 | 1 | labelMismatch|emptyOpenings |
| B-504 | 52.5 | 49.7 | 5.3 | raycast | 0 | 0 | 1 | leakSuspected|emptyOpenings |
| B-601 | 30 | 30 | 0 | raycast | 0 | 0 | 0 | leakSuspected|emptyOpenings |
| B-602 | 30 | 72.2 | 140.7 | flood | 0 | 0 | 2 | leakSuspected|labelMismatch|emptyOpenings |
| B-602 | 30 | 72.2 | 140.7 | flood | 0 | 0 | 2 | leakSuspected|labelMismatch|emptyOpenings |
| B-603 | 30 | 63.3 | 110.9 | flood | 0 | 0 | 0 | leakSuspected|labelMismatch|emptyOpenings |
| B-603 | 30 | 63.3 | 110.9 | flood | 0 | 0 | 0 | leakSuspected|labelMismatch|emptyOpenings |
| B-604 | 30 | 30 | 0 | raycast | 0 | 0 | 0 | leakSuspected|emptyOpenings |

CSV: `C:\Users\DELL\Dev\autocad-mcp\docs\reports\HOSPITAL-AUDIT-2026-06-08.csv`
