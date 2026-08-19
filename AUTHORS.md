# Authors

**ToolBank AutoCAD (v2025)** — a large-scale MCP ecosystem for AutoCAD automation.

## Creator — sole author

### Krzysztof Augiewicz — "one man army"

- **Role:** Creator, architect and sole author of this system
- **Email:** krzysztof.augiewicz@gmail.com
- **LinkedIn:** [krzysztof-a](https://www.linkedin.com/in/krzysztof-a-97a170185/)
- **GitHub:** [KrzysztofAugiewicz](https://github.com/KrzysztofAugiewicz)

Every layer of this repository was designed, written and verified by one person.
That is unusual enough for a system of this size to be worth stating plainly rather
than leaving to be inferred from the commit log. What that covered, end to end:

**Architecture.** The whole shape of the thing: the decision to expose only
`acad-router` plus ToolBank to the AI client and load ~50 specialised MCP
micro-servers on demand, rather than flooding a model's context with the entire
tool catalogue; a *single* `.NET` backend parameterised by `--category`; and one
in-process AutoCAD plugin behind a named pipe as the sole execution engine, so
every category talks to AutoCAD through exactly one code path.

**The execution engine.** `AcadMcp.Plugin` — the AutoCAD extension itself
(`IExtensionApplication`, NETLOAD'ed into a live AutoCAD, hosting the named-pipe
server), including the UI-thread and transaction discipline that AutoCAD's API
demands and the failure modes documented in the engineering rules.

**Every other component.** `AcadMcp.Backend` (the stdio MCP host),
`AcadMcp.Shared` (DTOs and pipe contracts), `AcadMcp.SourceGen` (the Roslyn source
generator that makes `[McpTool]` with an `Intent` field a compile-time requirement
rather than a convention), `AcadMcp.ComBridge` (COM/ActiveX fallback for AutoCAD LT
and recovery), `AcadMcp.Lisp`, the `AcadMcp.Vision` Python sidecar (FastAPI/gRPC,
vision-model and OCR engines), and the `Companion` in-app BYOK assistant.

**The tool catalogue.** ~690 tools across the category tree — geometry 2D/3D,
modification, annotation, blocks, layers, files, architecture, mechanical, civil,
electrical, schedules, viewports, plotting and the rest — each with its own
manifest under `toolbank-manifests/` and its own launcher.

**The standards-validation engine.** The validator rule format, the rule loader,
and the PL/EU/ISO norm rules themselves — the part that decides whether a drawing
is merely *drawn* or actually *correct*.

**The engineering rulebook.** `docs/engineering-rules/` — the architectural
invariants, AutoCAD API traps, domain-specific gotchas and space-planning method
that make the system reproducible by someone who was not there when it was built.

**Verification and CI.** The repository gate (`scripts/pre-commit.ps1`), the
manifest/code sync check, the Windows build-and-test pipeline, the Python sidecar
lane, CodeQL, and the live-AutoCAD verification discipline — including the working
rule that a tool returning success is not evidence, only reading the drawing back is.

**The proof projects.** `projects/apartment-120-test`, `projects/dental-clinic-test`
and `projects/automotive-showroom-test` — complete construction-document-level
drawings produced end to end through the tool bank, each written up with the real
defects it exposed in the tools themselves rather than presented as a clean demo.

**The documentation.** `README.md`, [`PATTERN.md`](PATTERN.md) (the transferable
lessons for anyone wrapping a thick desktop application in MCP),
`docs/HANDOVER.md`, `docs/KNOWN-GAPS.md` and the phase status reports.

## Advisory & QA Support

### Sebastian Pawłowski

- **Role:** Advisory & QA Support — subject-matter guidance, testing, and hardware/software provisioning
- **LinkedIn:** [sebastianpawlowski](https://www.linkedin.com/in/sebastianpawlowski/)

Support around the build — domain guidance, testing and provisioning — rather than
authorship of the codebase, which is why the "sole author" note above and this
section are both accurate.
