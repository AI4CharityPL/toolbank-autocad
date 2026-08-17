# Security policy

## Reporting a vulnerability

Report privately through
[GitHub Security Advisories](https://github.com/AI4CharityPL/toolbank-autocad/security/advisories/new).
Please do not open a public issue for a vulnerability.

Expect an acknowledgement within a week. This is a small project maintained alongside other
work — if a fix will take longer than that, you will be told so rather than left waiting.

Supported: the `main` branch. There are no maintained release branches yet.

## What this software actually is

Worth being blunt, because it shapes every judgement below.

This project lets a language model drive AutoCAD. The MCP server exposes several hundred
tools; the plugin half runs **inside the AutoCAD process**, with AutoCAD's full privileges
on the machine. It can create and modify drawings, read and write files, resolve external
references across the network, and execute AutoLISP.

That is the product working correctly. It is not a sandbox and does not claim to be.

## Trust model

- **The MCP client is trusted.** Anything that can speak to the server on stdio can invoke
  any tool. There is no per-tool authorisation and no audit trail you can rely on for
  attribution. Run it under a desktop account you would already trust with your drawings.
- **The transport is local.** `AcadMcp.Backend` talks over stdio; it reaches the plugin
  through a named pipe on the same machine. It does not open a network socket and should
  not be exposed as a network service.
- **The vision sidecar is a separate HTTP service.** It binds locally by default. Anything
  you send to it may be forwarded to a third-party model provider if you configured one, so
  treat sending a drawing to it as publishing that drawing to that provider.
- **Drawings you did not author are untrusted input.** A `.dwg` can carry AutoLISP that runs
  on open. Xrefs resolve paths that may be remote. Reading a hostile drawing is a risk
  AutoCAD carries with or without this project, but this project makes it easy to do at
  machine speed and without a human looking at the screen.

## The part that deserves your attention

The interesting risk here is not a buffer overflow. It is that **an agent acting on
instructions it read from a drawing** is a plausible attack, and this server is exactly the
mechanism that would turn such an instruction into a file write.

If a model reads text out of a drawing — a title-block note, an attribute, a layer name —
that text is data, not a command. Treat a drawing the way you would treat a downloaded web
page. This is a property of how you wire up your agent, which is why it belongs in a
security policy rather than in a bug tracker.

## In scope

- Path traversal or writing outside an intended directory in the file/export tools
- Command or AutoLISP injection reachable from ordinary tool arguments
- Crashes reachable from valid-looking arguments — AutoCAD holds unsaved work, and killing
  the process loses it (an unhandled exception used to take the server down; a catch-all now
  keeps the session alive, but the class of bug is real)
- Anything letting one MCP client reach another's data or session
- Secrets leaking into logs, error text or tool results
- Dependency vulnerabilities with a practical path to exploitation here

## Out of scope

- The fact that a trusted client can invoke every tool. That is the design.
- AutoCAD's own vulnerabilities — report those to Autodesk.
- Anything requiring an attacker who already has code execution on the machine, since they
  could drive AutoCAD directly.
- The AGPL/licensing status of optional ML dependencies. That is a licensing matter, covered
  in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## What we do on our side

- CodeQL (`security-and-quality`) on every push and pull request, C# and Python.
- Dependabot on NuGet, pip and GitHub Actions.
- A secret scan in the pre-commit gate, run again over the whole tree in CI.
- No API keys in the repository. The vision sidecar reads provider keys from the
  environment.
