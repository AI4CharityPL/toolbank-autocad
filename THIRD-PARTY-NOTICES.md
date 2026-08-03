# Third-party notices

MCP Nexus AutoCAD is licensed under the [MIT License](LICENSE). That covers the code in
this repository. It does not cover the third-party software listed below, which stays
under its own terms.

Two things here need a real decision from you before you deploy, so they are first:

---

## 1. Autodesk AutoCAD managed assemblies — not redistributed

`AcadMcp.Plugin` and `AcadMcp.Companion.Host` compile against four assemblies that ship
with AutoCAD:

| Assembly | Used for |
|---|---|
| `acmgd.dll` | Editor, Document, application-level API |
| `acdbmgd.dll` | Database, entities, transactions |
| `accoremgd.dll` | Core console / command APIs |
| `acdbmgdbrep.dll` | Boundary representation (face enumeration, surface areas) |

**Nothing from Autodesk is redistributed by this repository.** Every reference is
declared as:

```xml
<Reference Include="acdbmgd">
  <HintPath>$(AcadInstallPath)acdbmgd.dll</HintPath>
  <Private>false</Private>
  <ExcludeAssets>runtime</ExcludeAssets>
</Reference>
```

`Private=false` and `ExcludeAssets=runtime` mean the assemblies are used at compile time
only and are never copied into build output, into the `.bundle`, or into any release
artifact. At runtime the plugin resolves them from the AutoCAD process it was NETLOAD'ed
into.

Consequences for you:

- **You need your own licensed AutoCAD 2025 (or later) install to build or run this.**
  `$(AcadInstallPath)` defaults to `C:\Program Files\Autodesk\AutoCAD 2025\` and is
  overridable as an MSBuild property; `scripts/detect-autocad.ps1` finds it for you.
- The MIT licence on this repository grants you no rights whatsoever to Autodesk
  software. Your use of AutoCAD stays governed by your Autodesk licence agreement.
- `AcadMcp.ComBridge` is deliberately free of this: it uses late-bound COM/ActiveX and
  references no Autodesk assembly at all (`NEVER references acmgd.dll`, per its own
  project description). It is the fallback path for AutoCAD LT and recovery scenarios.

## 2. Ultralytics YOLO — AGPL-3.0, opt-in only

`src/AcadMcp.Vision/acadmcp_vision/engines/yolo.py` can drive a YOLO model for CAD-symbol
detection. **Ultralytics is licensed AGPL-3.0**, which is a copyleft licence and is not
compatible with simply folding it into an MIT project.

This repository is arranged so that a default install never touches it:

- `ultralytics` is **not** a dependency of `acadmcp-vision`. It appears only in the
  optional `[ml]` extra.
- `yolo.py` never imports it at module load. It resolves the module through
  `importlib.import_module("ultralytics")` inside `_get_model()`, at call time, and
  raises a plain `ImportError` with installation instructions when it is absent.
- No YOLO weights are committed. `weights_path()` looks for
  `%LOCALAPPDATA%\AcadMcp\vision-models\cad-symbols-{discipline}.pt`, and
  `.gitignore` excludes `src/AcadMcp.Vision/models/*.pt|*.onnx|*.bin`.

So: **the MIT code in this repository links no AGPL code as shipped.** If you run
`pip install acadmcp-vision[ml]`, or otherwise install `ultralytics`, the AGPL-3.0
obligations attach to *your* deployment — including, if you offer it as a network
service, the requirement to offer corresponding source to your users. That is your call
to make, not ours, which is why it is opt-in and flagged in `pyproject.toml` itself.

If you want symbol detection without AGPL, the `describe`/`classify` paths in
`engines/vision_llm.py` (hosted vision models) and OCR via PaddleOCR (Apache-2.0) are
unaffected by this.

---

## 3. .NET dependencies (NuGet)

All MIT-licensed:

| Package | Version | Licence | Project |
|---|---|---|---|
| `Microsoft.Extensions.DependencyInjection` | 8.0.1 | MIT | Backend |
| `Microsoft.Extensions.Hosting` | 8.0.1 | MIT | Backend |
| `Microsoft.Extensions.Logging.Console` | 8.0.1 | MIT | Backend |
| `Microsoft.Extensions.Logging.Debug` | 8.0.1 | MIT | Backend |
| `YamlDotNet` | 15.1.6 | MIT | Backend (validator rule loading) |
| `System.Text.Json` | 8.0.5 | MIT | Shared |
| `System.Memory` | 4.5.5 | MIT | Shared |
| `System.Security.Cryptography.ProtectedData` | 8.0.0 | MIT | Companion (`SecureKeyStore`) |
| `Microsoft.CodeAnalysis.CSharp` | 4.11.0 | MIT | SourceGen (build-time only, `PrivateAssets=all`) |
| `Microsoft.CodeAnalysis.Analyzers` | 3.11.0 | MIT | SourceGen (build-time only, `PrivateAssets=all`) |

## 4. Python dependencies (vision sidecar)

### Core — installed by default

| Package | Licence |
|---|---|
| `fastapi` | MIT |
| `uvicorn` | BSD-3-Clause |
| `pydantic`, `pydantic-settings` | MIT |
| `structlog` | Apache-2.0 / MIT (dual) |
| `anyio` | MIT |
| `httpx` | BSD-3-Clause |
| `pillow` | MIT-CMU (HPND) |
| `pypdfium2` | BSD-3-Clause / Apache-2.0; bundles PDFium (BSD-3-Clause) |

### Optional — resolved lazily, never imported unless installed

| Package | Licence | Resolved by | In an extra? |
|---|---|---|---|
| `ultralytics` | **AGPL-3.0** | `engines/yolo.py` | `[ml]` |
| `torch`, `torchvision` | BSD-3-Clause | via `ultralytics` / `paddleocr` | `[ml]` |
| `paddlepaddle`, `paddleocr` | Apache-2.0 | `engines/ocr.py` | `[ml]` |
| `anthropic` | MIT | `engines/vision_llm.py` | `[ml]` |
| `openai` | Apache-2.0 | `engines/vision_llm.py` | `[ml]` |
| `google-genai` | Apache-2.0 | `engines/vision_llm.py` | `[ml]` |
| `easyocr` | Apache-2.0 | `engines/ocr.py` (2nd fallback) | **no** |
| `pytesseract` | Apache-2.0 | `engines/ocr.py` (3rd fallback) | **no** |
| `numpy` | BSD-3-Clause | `engines/ocr.py` | **no** (arrives via the OCR stacks) |

`easyocr` and `pytesseract` are supported fallbacks that no extra installs for you — if
you want either, install it yourself. `pytesseract` additionally needs the Tesseract
binary (Apache-2.0) present on the system; it is a wrapper, not the engine.

### Development

`pytest` (MIT), `pytest-asyncio` (Apache-2.0), `ruff` (MIT), `mypy` (MIT),
`types-protobuf` (Apache-2.0).

## 5. Model weights

No model weights are committed to this repository. `scripts/setup-vision-models.ps1`
downloads them on demand and `.gitignore` keeps them out of version control.

Weights carry their own licences, independent of the code that loads them — YOLO weights
you train or obtain yourself, and PaddleOCR's published models (Apache-2.0). Check the
terms of any weights you deploy.

## 6. Sample drawings and reports

The files under `assets/` and `projects/hospital-2026/` (`Hospital2026*.dwg`, the
generated PDFs, PNG renders and audit reports) were produced by this project as its own
test material. They are covered by the MIT licence along with the rest of the repository
and depict no real client work.

---

## Keeping this file honest

This list is maintained by hand and will drift. If you add a dependency, add it here in
the same pass.

An automated licence scan (`dotnet-project-licenses` for NuGet, `pip-licenses` for the
sidecar) is not wired into CI yet — until it is, treat this file as best-effort and
verify anything you are relying on legally.

Found something wrong or missing? Please open an issue — getting attribution right
matters more to us than being right the first time.
