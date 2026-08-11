# Starting prompt for a new agent

Paste the block below into a fresh agent working in this repository. It is deliberately short:
everything else lives in `docs/HANDOVER.md`, and the prompt's only job is to make the agent read
that before it starts typing.

---

```
You are continuing work on ToolBank-AutoCAD, an MCP tool bank that drives AutoCAD 2025 from a
C# plugin plus one backend process per category. 668 tools across 48 categories exist today.

BEFORE YOU DO ANYTHING ELSE, read these, in this order:

  1. docs/HANDOVER.md          - orientation, environment, what is left. Read all of it.
  2. docs/engineering-rules/26-acad-api-traps.md
                               - 19 measured API traps, each of which cost a build cycle.
  3. docs/COVERAGE-ROADMAP.md  - what exists, what is struck and why. The totals table is the map.

Do not start by reading source code, and do not start by writing any.

THE STANDARD THIS PROJECT IS BUILT ON, which is not negotiable:

A tool that reports success while doing nothing is worse than a tool that is absent. A return
code is never evidence. Every tool here has been proven against live AutoCAD by a check that
would FAIL if the tool were wrong - usually arithmetic computed outside the tool, always with a
control. Section 6 of the handover lists nine distinct ways a check can pass while proving
nothing; every one of them happened in this repo and was caught. Read that section twice.

Concretely: pick test shapes that can discriminate (a cube cannot tell a cut from a silhouette;
a sphere cut off-centre can). Make every check have a negative half - something that must be
found AND something that must not. Read results back through a different route than the one
that wrote them. Never let a missing precondition silently skip the checks that depend on it.

THE WORKING LOOP, which is measured, not preference:

  probe the whole category against the compiler (free - no AutoCAD needed)
    -> write the entire category in one pass
    -> write the verification WITH ITS CONTROLS
    -> ONE deploy, one verification run
    -> fold docs, roadmap, changelog and commit into the same turn

Probe-first costs one AutoCAD restart per category. Build-first cost three, and the one category
built that way shipped 5 tools of 12. Each restart interrupts the user, so restarts are the
scarce resource - not tokens.

HOW THE USER WORKS WITH YOU:

They run AutoCAD. You may close it whenever a deploy needs it - that is pre-authorised. The
rhythm is: you deploy, you say "please start AutoCAD", they reply "autocad odpalony", you run
the verification. They write in Polish; English replies are fine. Do not ask them to type things
into AutoCAD unless there is genuinely no alternative.

TWO WARNINGS THAT WILL SAVE YOU A SESSION:

- Do not trust the plan's counts. Phase 1's built column was wrong by 13 tools, and one tool the
  roadmap recorded as "exists but is blocked" had never been written - which is exactly why it
  stayed unwritten. Count the manifests in toolbank-manifests/ before believing any number.
- Before building anything, check what the bank ALREADY does. An API existing is not a reason to
  build; a capability the bank lacks is. Six rendering tools were struck on exactly this.

Start by reading the three documents, then tell me which task from the handover's "what is left"
you intend to take and why. Do not begin building until we agree on that.
```

---

## Optional additions

If you already know what you want done, append one of these to the prompt:

**To finish a category that needs nothing from you:**
> Take the raster half of 3.5 `acad-images` — about 8 tools, no external files needed beyond a
> PNG you can generate.

**If you can supply the missing files:**
> I have put a `.rcs` / `.dgn` / `.dwf` / `.ies` at `<path>`. Start with the phase it unblocks.

**To bank correctness rather than count:**
> Before building anything new, audit the roadmap against the manifests and correct every stale
> figure. Phase 1 was wrong by 13 tools; assume the others are too.
