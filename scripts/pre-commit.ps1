<#
.SYNOPSIS
    Fast local gate run before every commit. Must complete in <60s on a warm cache.
    Enforces rules 40-pre-commit-gates.md.

.DESCRIPTION
    Tier 1 checks ONLY:
      1. Engineering rules well-formed (docs/engineering-rules/*.md non-empty, starts with a heading)
      2. mcpbank-manifests/*.json valid + required fields per rule 30
      3. Forbidden C# patterns (per rule 40)
      4. Secret regex on staged files
      5. CHANGELOG.md was touched if anything under src/ changed
      6. Validator YAMLs (validators/**/*.yaml) load through RuleLoader
         (only runs if AcadMcp.Backend.exe is already built; otherwise skipped
         with a hint). Enforces rules 33-validators-rule-format.md and
         34-validators-engine-traps.md.
      7. xUnit unit tests for AcadMcp.Backend + Shared (net8.0). Runs the
         already-built tests/AcadMcp.Tests.dll via `dotnet test --no-build`
         so the gate stays under the 60 s rule-40 budget. Skipped when the
         test assembly is not yet built (fresh clone).

    Does NOT run dotnet build, no network, no auto-fix.
    Install as a Git hook with: pwsh scripts/pre-commit.ps1 -Install

.PARAMETER Install
    Symlink/copy this script as .git/hooks/pre-commit and exit.

.PARAMETER All
    Check the entire working tree, not just staged changes (default in CI).

.PARAMETER RepoRoot
    Repository root. Defaults to parent of script directory.

.PARAMETER FailFast
    Exit on the first failed check instead of collecting all errors.

.EXAMPLE
    pwsh scripts/pre-commit.ps1
    pwsh scripts/pre-commit.ps1 -Install
    pwsh scripts/pre-commit.ps1 -All
#>
[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$All,
    [string]$RepoRoot,
    [switch]$FailFast
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot }
                 elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
                 else { (Get-Location).Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}
Set-Location $RepoRoot

if ($Install) {
    $hookDir = Join-Path $RepoRoot ".git\hooks"
    if (-not (Test-Path $hookDir)) {
        Write-Error "No .git/hooks dir at $hookDir - run from a git repo (run 'git init' first)."
        exit 2
    }
    $hookPath = Join-Path $hookDir "pre-commit"
    $shimContent = "#!/bin/sh`nexec powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`"`n"
    Set-Content -Path $hookPath -Value $shimContent -Encoding ASCII -NoNewline
    Write-Host "Installed pre-commit hook at $hookPath" -ForegroundColor Green
    exit 0
}

$started   = Get-Date
$errors    = New-Object System.Collections.Generic.List[string]
$checked   = 0

function Add-Err([string]$msg) {
    $script:errors.Add($msg) | Out-Null
    Write-Host "  FAIL: $msg" -ForegroundColor Red
    if ($FailFast) {
        Write-Host ""
        Write-Host "[fail-fast] Aborting after first error." -ForegroundColor Red
        exit 1
    }
}
function Add-OK([string]$msg) {
    $script:checked++
    Write-Host "  ok  : $msg" -ForegroundColor DarkGray
}

# Run a native executable and return its stdout+stderr as plain strings.
#
# Windows PowerShell 5.1 turns every stderr line from a native exe into an ErrorRecord when
# you write `2>&1`, and this script runs with $ErrorActionPreference='Stop', so a single line
# of logging aborted the whole gate. That is not hypothetical: AcadMcp.Backend logs its
# startup banner to stderr -- correctly, because stdout is reserved for JSON-RPC frames --
# and check 6 died on it every time.
#
# Dropping to 'Continue' for the duration keeps the lines flowing as data, and "$_" flattens
# any ErrorRecord back to its text. $LASTEXITCODE stays the real exit code of the process.
function Invoke-NativeCapture([string]$exe, [string[]]$exeArgs) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $lines = & $exe @exeArgs 2>&1 | ForEach-Object { "$_" }
        $script:LastNativeExit = $LASTEXITCODE
        return @($lines)
    }
    finally { $ErrorActionPreference = $prev }
}
$script:LastNativeExit = 0

function Get-StagedFiles {
    if ($All) {
        return Get-ChildItem -Recurse -File |
               Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git)\\' } |
               ForEach-Object { Resolve-Path -Relative $_.FullName }
    }
    try {
        $out = & git diff --cached --name-only --diff-filter=ACMR 2>$null
        if ($LASTEXITCODE -ne 0) { return @() }
        return $out | Where-Object { $_ -and (Test-Path $_) }
    } catch { return @() }
}

$staged = @(Get-StagedFiles)
if ($staged.Count -eq 0 -and -not $All) {
    Write-Host "No staged files - nothing to gate. (Use -All to scan the whole tree.)" -ForegroundColor Yellow
    exit 0
}

Write-Host "=== Pre-commit gate ===" -ForegroundColor Cyan
Write-Host ("Mode: {0}, Files: {1}" -f ($(if ($All) { 'ALL' } else { 'staged' })), $staged.Count) -ForegroundColor DarkGray
Write-Host ""

# 1. Engineering rules well-formed
Write-Host "[1/7] Engineering rules" -ForegroundColor Cyan
$ruleFiles = Get-ChildItem -Path (Join-Path $RepoRoot "docs\engineering-rules") -Filter "*.md" -ErrorAction SilentlyContinue
foreach ($rf in $ruleFiles) {
    $text = Get-Content $rf.FullName -Raw
    if ([string]::IsNullOrWhiteSpace($text)) {
        Add-Err "$($rf.Name): file is empty"
        continue
    }
    if (-not ($text.TrimStart() -match "^#\s+\S")) {
        Add-Err "$($rf.Name): must start with a '# Title' heading"
        continue
    }
    Add-OK "$($rf.Name)"
}

# 2. Manifests valid
Write-Host ""
Write-Host "[2/7] MCPBank manifests" -ForegroundColor Cyan
$required = @("id", "name", "description", "transport", "tags", "intent_examples", "tools_summary")
$mfFiles = Get-ChildItem -Path (Join-Path $RepoRoot "mcpbank-manifests") -Filter "acad-*.json" -ErrorAction SilentlyContinue
foreach ($mf in $mfFiles) {
    try {
        $entry = Get-Content $mf.FullName -Raw | ConvertFrom-Json
    } catch {
        Add-Err "$($mf.Name): invalid JSON - $($_.Exception.Message)"
        continue
    }
    $missing = @($required | Where-Object {
        $p = $entry.PSObject.Properties[$_]
        return ($null -eq $p) -or ($null -eq $p.Value)
    })
    if ($missing.Count -gt 0) {
        Add-Err "$($mf.Name): missing required fields: $($missing -join ', ')"
        continue
    }
    if ($entry.id -ne ($mf.BaseName)) {
        Add-Err "$($mf.Name): id '$($entry.id)' does not match filename '$($mf.BaseName)'"
        continue
    }
    if ($entry.intent_examples.Count -lt 5) {
        Add-Err "$($mf.Name): intent_examples count is $($entry.intent_examples.Count), need >= 5 (rule 31)"
        continue
    }
    # Rule 31: description must be a real sentence, not a scaffold placeholder.
    if ([string]::IsNullOrWhiteSpace($entry.description) -or $entry.description -match '(?i)\bTODO\b' -or $entry.description -match '(?i)Auto-generated stub') {
        Add-Err "$($mf.Name): description still contains a TODO/scaffold placeholder (rule 31)"
        continue
    }
    $wordCount = ($entry.description -split '\s+' | Where-Object { $_ }).Count
    if ($wordCount -lt 30) {
        Add-Err "$($mf.Name): description is only $wordCount words; rule 31 wants 40-100 (one informative paragraph)"
        continue
    }
    # Rule 31: intent_examples must not contain leftover scaffold placeholders.
    $todoIntents = @($entry.intent_examples | Where-Object { $_ -match '(?i)\bTODO\b' -or $_ -match '^\(seed\)' })
    if ($todoIntents.Count -gt 0) {
        Add-Err "$($mf.Name): intent_examples still contains $($todoIntents.Count) scaffold placeholder(s) (rule 31): $($todoIntents -join ' | ')"
        continue
    }
    # Rule 30: metadata.phase should name a real implementation phase.
    if ($null -ne $entry.metadata -and $null -ne $entry.metadata.phase -and $entry.metadata.phase -match '(?i)^TODO$') {
        Add-Err "$($mf.Name): metadata.phase='TODO' - set to the phase id from CHANGELOG (rule 30)"
        continue
    }
    # Rule 31: every tool must have a real description.
    foreach ($t in $entry.tools_summary) {
        if ([string]::IsNullOrWhiteSpace($t.description) -or $t.description -match '(?i)\bTODO\b' -or $t.description -match '(?i)Placeholder tool') {
            Add-Err "$($mf.Name): tool '$($t.name)' has placeholder/TODO description (rule 31)"
            continue
        }
        if ($t.description.Length -lt 25) {
            Add-Err "$($mf.Name): tool '$($t.name)' description is only $($t.description.Length) chars; aim for >= 25 explaining what it does"
        }
    }
    Add-OK "$($mf.Name) ($($entry.tools_summary.Count) tools)"
}

# 3. Forbidden patterns in staged C#
Write-Host ""
Write-Host "[3/7] Forbidden C# patterns" -ForegroundColor Cyan
$csStaged = $staged | Where-Object { $_ -like "*.cs" }
$forbidden = @(
    @{ Pattern = 'Marshal\.GetActiveObject\s*\('; Reason = "use MarshalCompat (rule: AcadMcp.ComBridge)"; Scope = '*' },
    @{ Pattern = 'Console\.WriteLine'; Reason = "use ILogger; stdout reserved for JSON-RPC frames (rule 40)"; Scope = 'src/AcadMcp.Backend/' }
)

# The Intent= check (rule 20) is deliberately NOT a regex.
#
# It used to be:  \[\s*McpTool\s*\((?![^\]]*Intent\s*=)
# and the [^\]] class made it stop at the first ']' *inside the description string* --
# so callouts.insert_title_block, whose description legitimately reads
# "Pass fields=[{key, value}, ...]", was reported as missing an Intent it plainly has.
# Two files failed the gate for that reason and neither had a real defect.
#
# So walk the attribute instead: from '[McpTool(' to its matching ')', counting paren
# depth and skipping over string literals (both \" and "" escapes). That gives the whole
# argument list regardless of what punctuation the prose contains, and lets the failure
# name the offending tool and line rather than just the file.
function Get-McpToolAttributes([string]$body) {
    $found = New-Object System.Collections.Generic.List[object]
    foreach ($m in [regex]::Matches($body, '\[\s*McpTool\s*\(')) {
        $i = $m.Index + $m.Length   # first char after the '('
        $depth = 1
        $inStr = $false
        while ($i -lt $body.Length -and $depth -gt 0) {
            $c = $body[$i]
            if ($inStr) {
                if ($c -eq '\') { $i += 2; continue }                      # \" and friends
                if ($c -eq '"') {
                    if ($i + 1 -lt $body.Length -and $body[$i + 1] -eq '"') { $i += 2; continue }  # "" in @"..."
                    $inStr = $false
                }
            }
            elseif ($c -eq '"') { $inStr = $true }
            elseif ($c -eq '(') { $depth++ }
            elseif ($c -eq ')') { $depth-- }
            $i++
        }
        $span = $body.Substring($m.Index, $i - $m.Index)
        $name = if ($span -match '^\[\s*McpTool\s*\(\s*"([^"]+)"') { $matches[1] } else { '<unnamed>' }
        $line = ($body.Substring(0, $m.Index) -split "`n").Count
        $found.Add([pscustomobject]@{ Name = $name; Text = $span; Line = $line }) | Out-Null
    }
    return $found
}

$patternHits = 0
$toolsSeen = 0
foreach ($f in $csStaged) {
    $abs = Join-Path $RepoRoot $f
    if (-not (Test-Path $abs)) { continue }
    $body = Get-Content $abs -Raw
    foreach ($rule in $forbidden) {
        if ($rule.Scope -ne '*' -and ($f -replace '\\','/') -notlike ("*" + $rule.Scope + "*")) { continue }
        if ($body -match $rule.Pattern) {
            Add-Err "${f}: forbidden pattern - $($rule.Reason)"
            $patternHits++
        }
    }
    foreach ($att in (Get-McpToolAttributes $body)) {
        $toolsSeen++
        if ($att.Text -notmatch 'Intent\s*=') {
            Add-Err "${f}:$($att.Line): [McpTool(`"$($att.Name)`")] has no Intent= (rule 20)"
            $patternHits++
        }
    }
}
if ($patternHits -eq 0) {
    Add-OK "no forbidden patterns in $($csStaged.Count) C# files; all $toolsSeen [McpTool] attributes carry Intent="
}

# 4. Secrets
Write-Host ""
Write-Host "[4/7] Secret scan" -ForegroundColor Cyan
$secretRegex = '(?i)(api[_-]?key|password|secret|token|access[_-]?key)\s*[:=]\s*["''][A-Za-z0-9_\-]{16,}["'']'
$skipExt = @(".png", ".jpg", ".jpeg", ".gif", ".webp", ".pdf", ".dwg", ".dxf", ".zip", ".7z", ".dll", ".pdb", ".exe")
foreach ($f in $staged) {
    $ext = [System.IO.Path]::GetExtension($f).ToLower()
    if ($skipExt -contains $ext) { continue }
    $abs = Join-Path $RepoRoot $f
    if (-not (Test-Path $abs)) { continue }
    $body = Get-Content $abs -Raw -ErrorAction SilentlyContinue
    if ($body -and $body -match $secretRegex) {
        $matchText = $matches[0]
        $masked = $matchText.Substring(0, [Math]::Min(20, $matchText.Length)) + "..."
        Add-Err "${f}: possible secret -> $masked"
    }
}
Add-OK "secret scan complete"

# 5. CHANGELOG touched if src/ touched
Write-Host ""
Write-Host "[5/7] CHANGELOG.md gate" -ForegroundColor Cyan
$srcTouched = $staged | Where-Object { ($_ -replace '\\','/') -like 'src/*' -and $_ -notlike '*.md' -and $_ -notlike '*Tests*' }
$changelogTouched = $staged | Where-Object { $_ -eq 'CHANGELOG.md' -or $_ -eq './CHANGELOG.md' }
if ($srcTouched -and -not $changelogTouched) {
    Add-Err "src/ files staged ($($srcTouched.Count)) but CHANGELOG.md NOT staged (rule 51)"
} else {
    Add-OK "CHANGELOG gate ok"
}

# 6. Validator YAML self-check (rules 33 + 34).
# Skipped automatically when AcadMcp.Backend.exe is not built yet, so the
# gate stays viable on a freshly cloned repo.
Write-Host ""
Write-Host "[6/7] Validator rules self-check" -ForegroundColor Cyan
$yamlTouched = $staged | Where-Object { ($_ -replace '\\','/') -like 'validators/*' -and $_ -like '*.yaml' }
$shouldRunSelfCheck = $All -or ($yamlTouched.Count -gt 0)
if (-not $shouldRunSelfCheck) {
    Add-OK "no validator yaml staged - skipped"
} else {
    $backendExe = Join-Path $RepoRoot 'src\AcadMcp.Backend\bin\Release\net8.0\AcadMcp.Backend.exe'
    if (-not (Test-Path $backendExe)) {
        $backendExe = Join-Path $RepoRoot 'src\AcadMcp.Backend\bin\Debug\net8.0\AcadMcp.Backend.exe'
    }
    if (-not (Test-Path $backendExe)) {
        Add-OK "AcadMcp.Backend.exe not built - skipped (build first to validate yaml rules)"
    } else {
        $output = Invoke-NativeCapture $backendExe @('--validators-self-check')
        $code = $script:LastNativeExit
        if ($code -ne 0) {
            $errLines = @($output) | Where-Object { $_ -match '(?i)error|FAIL' } | Select-Object -First 5
            Add-Err "validators self-check failed (exit $code): $($errLines -join ' | ')"
        } else {
            $summary = @($output) | Where-Object { $_ -match 'self-check:' } | Select-Object -First 1
            Add-OK "validators self-check OK ($summary)"
        }
    }
}

# 7. xUnit unit tests (rule 25-mcp-tool-tests.md + rule 40 §3).
# Runs the already-compiled tests/AcadMcp.Tests.dll via `dotnet test --no-build`
# so the gate stays under the 60 s rule-40 budget on a warm cache (<3 s here).
# Skipped silently when the assembly is not yet built (fresh clone) so new
# contributors aren't blocked before their first `dotnet build`.
Write-Host ""
Write-Host "[7/7] Unit tests" -ForegroundColor Cyan
$testsDll = Join-Path $RepoRoot 'tests\AcadMcp.Tests\bin\Debug\net8.0\AcadMcp.Tests.dll'
if (-not (Test-Path $testsDll)) {
    $testsDllRel = Join-Path $RepoRoot 'tests\AcadMcp.Tests\bin\Release\net8.0\AcadMcp.Tests.dll'
    if (Test-Path $testsDllRel) { $testsDll = $testsDllRel }
}
if (-not (Test-Path $testsDll)) {
    Add-OK "AcadMcp.Tests.dll not built - skipped (run 'dotnet build src/AcadMcp.sln' first)"
} else {
    # Target the test project, not src/AcadMcp.sln: the solution also contains
    # AcadMcp.Plugin and Companion.Host, which reference the AutoCAD managed assemblies
    # via $(AcadInstallPath). On a machine without AutoCAD -- every CI runner -- evaluating
    # those projects is noise at best. The tests never touch them.
    # --no-build keeps the gate inside its 60 s budget, but it will happily run a months-old
    # assembly and report the result as current. That is not theoretical either: this check
    # once failed on a schedules test that had been fixed long before, because the DLL predated
    # the fix. A stale PASS is the dangerous direction. Same class of bug as deploy-plugin.ps1
    # defaulting to Debug and shipping an April build.
    $newestSource = Get-ChildItem -Path (Join-Path $RepoRoot 'src'), (Join-Path $RepoRoot 'tests') `
                        -Recurse -File -Include *.cs, *.csproj -ErrorAction SilentlyContinue |
                    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
                    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($newestSource -and $newestSource.LastWriteTimeUtc -gt (Get-Item $testsDll).LastWriteTimeUtc) {
        Add-Err ("test assembly is STALE - {0} is newer than AcadMcp.Tests.dll. " -f $newestSource.Name +
                 "Run: dotnet build tests/AcadMcp.Tests/AcadMcp.Tests.csproj")
    }

    $testProj = Join-Path $RepoRoot 'tests\AcadMcp.Tests\AcadMcp.Tests.csproj'
    $testOutput = Invoke-NativeCapture 'dotnet' @('test', $testProj, '--no-build', '--nologo', '--verbosity', 'quiet')
    $testCode = $script:LastNativeExit
    if ($testCode -ne 0) {
        $failLines = @($testOutput) | Where-Object { $_ -match '(?i)FAIL|Niepowodzenie|failed' } | Select-Object -First 5
        Add-Err "dotnet test failed (exit $testCode): $($failLines -join ' | ')"
    } else {
        $summaryLine = @($testOutput) | Where-Object { $_ -match 'Powodzenie!|Passed!' } | Select-Object -First 1
        if (-not $summaryLine) { $summaryLine = 'tests passed' }
        Add-OK "unit tests OK ($($summaryLine.Trim()))"
    }
}

$elapsed = ((Get-Date) - $started).TotalSeconds
Write-Host ""
Write-Host "=== Result ===" -ForegroundColor Cyan
Write-Host ("Checked   : {0} items" -f $checked)
Write-Host ("Errors    : {0}" -f $errors.Count) -ForegroundColor $(if ($errors.Count -gt 0) { 'Red' } else { 'Green' })
Write-Host ("Elapsed   : {0:F2} s" -f $elapsed)

if ($elapsed -gt 60) {
    Write-Warning "Pre-commit took >60s - violates rule 40. Trim a check or move to CI."
}

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Commit BLOCKED. Fix the failures above. Bypass with --no-verify only on WIP branches (rule 40)." -ForegroundColor Red
    exit 1
}

Write-Host "OK - safe to commit." -ForegroundColor Green
exit 0
