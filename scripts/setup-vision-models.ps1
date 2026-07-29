<#
    setup-vision-models.ps1
    --------------------------------------------------------------------------
    Install heavy ML deps + download YOLO weights into the per-discipline files
    expected by the AcadMcp.Vision sidecar (rule 32, trap #5):

        %LOCALAPPDATA%\AcadMcp\vision-models\cad-symbols-{arch,mech,elec,pid}.pt

    Defaults:
      -All        : install ML deps AND download every discipline's weights.
      -OcrOnly    : install only PaddleOCR / EasyOCR / Tesseract, no YOLO.
      -Discipline : pick a subset of YOLO weight files to download.

    The actual weight URLs are NOT hardcoded yet - this is a stub that prints the
    expected target paths so the AI agent / operator can drop weights manually
    until Phase 8 trains real ones.
#>
[CmdletBinding()]
param(
    [switch] $All,
    [switch] $OcrOnly,
    [string[]] $Discipline = @('arch','mech','elec','pid')
)

$ErrorActionPreference = 'Stop'

$RepoRoot     = Resolve-Path (Join-Path $PSScriptRoot '..') | ForEach-Object Path
$VisionDir    = Join-Path $RepoRoot 'src\AcadMcp.Vision'
$LocalAppData = $env:LOCALAPPDATA
if (-not $LocalAppData) { $LocalAppData = Join-Path $HOME '.local\share' }
$ModelDir     = Join-Path $LocalAppData 'AcadMcp\vision-models'
$null         = New-Item -ItemType Directory -Force -Path $ModelDir

if (-not $OcrOnly) {
    Write-Host "[1/3] Installing AcadMcp.Vision + ML extras (this can take a while)..."
    Push-Location $VisionDir
    try {
        & python -m pip install -e ".[ml]"
        if ($LASTEXITCODE -ne 0) { throw "pip install failed: $LASTEXITCODE" }
    } finally { Pop-Location }
} else {
    Write-Host "[1/3] Installing AcadMcp.Vision base + OCR engines only..."
    Push-Location $VisionDir
    try {
        & python -m pip install -e "."
        & python -m pip install paddleocr paddlepaddle easyocr pytesseract
    } finally { Pop-Location }
}

Write-Host "[2/3] Verifying model directory: $ModelDir"
foreach ($disc in $Discipline) {
    $weightsPath = Join-Path $ModelDir ("cad-symbols-{0}.pt" -f $disc)
    if (Test-Path $weightsPath) {
        $sizeMB = [math]::Round((Get-Item $weightsPath).Length / 1MB, 2)
        Write-Host (" - {0,-10} OK  ({1} MB)" -f $disc, $sizeMB)
    } else {
        Write-Host (" - {0,-10} MISSING - drop a YOLO .pt at: {1}" -f $disc, $weightsPath) -ForegroundColor Yellow
    }
}

Write-Host "[3/3] Done. Vision sidecar will lazy-load each model at first use."
Write-Host "Tip: set ANTHROPIC_API_KEY or OPENAI_API_KEY to enable describe_image / classify_drawing."
