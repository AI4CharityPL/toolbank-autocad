# Authors

**ToolBank AutoCAD (v2025)** — a large-scale MCP ecosystem for AutoCAD automation.

## Lead — originator, architect and principal author

### Krzysztof Augiewicz

- **Role:** Originator of the idea, architect, and author of the large majority of the code
- **Email:** krzysztof.augiewicz@gmail.com
- **LinkedIn:** [krzysztof-a](https://www.linkedin.com/in/krzysztof-a-97a170185/)
- **GitHub:** [KrzysztofAugiewicz](https://github.com/KrzysztofAugiewicz)

The idea, the architecture and nearly all of the implementation are his. Every design
decision below was made once, by one person, and then defended across the whole tree —
which is what keeps a system of this size coherent. He was not, however, working alone:
the people listed under *Contributors* below shaped parts of it, and the project is
better for that.

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

**The tool catalogue.** 692 tools across 51 categories — geometry 2D/3D,
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

## Contributors

Smaller in volume than the work above, and load-bearing anyway. Each of these
shaped something that would have been worse, or missing, without them.

### Kacper Pisarczyk — verification and drawing review

- **Role:** Live-AutoCAD verification and drawing review
- **LinkedIn:** [kacper-pisarczyk](https://www.linkedin.com/in/kacper-pisarczyk-b165311aa/)

This project's central working rule is that a tool returning a success code is not
evidence — only reading the drawing back is. That rule costs someone real hours in front
of a live AutoCAD, comparing what a tool claimed against what actually landed on the
sheet. Several defects that every automated check had passed were caught exactly there,
by looking.

### Mateusz Wiszniowski — discovery layer and technical documentation

- **Role:** ToolBank discovery-layer reasoning, integration review, technical documentation
- **LinkedIn:** [mateusz-wiszniowski](https://www.linkedin.com/in/mateusz-wiszniowski-263049216/)

Co-author of [`docs/TOOLBANK-TECHNICAL-PROOF.md`](docs/TOOLBANK-TECHNICAL-PROOF.md) — the
document that measures what the discovery layer actually buys, rather than asserting it:
the token cost of a naive registration against lazy discovery, and why `acad-router`
consumes ToolBank instead of replacing it. The measurement, and the argument built on it,
came out of that work.

## Advisory & QA support

### Sebastian Pawłowski

- **Role:** Advisory & QA support — subject-matter guidance, testing, and hardware/software provisioning
- **LinkedIn:** [sebastianpawlowski](https://www.linkedin.com/in/sebastianpawlowski/)

Support around the build — domain guidance, testing and provisioning — rather than
authorship of the codebase, which is why this section sits apart from the ones above.
