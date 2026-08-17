# Wrapping a thick desktop application in MCP

Notes from building an MCP server over AutoCAD — 478 tools across 39 categories, every one
of them driven by hand against a live copy of the application at least once.

This document is the part that has nothing to do with AutoCAD. If you are wrapping Excel,
Photoshop, Revit, SAP, a PLC toolchain or an EDA suite, the application differs and almost
none of these problems do. It is written as a set of claims with the evidence that produced
them, because advice without the failure behind it is impossible to weigh.

The short version, if you read nothing else:

> **The return code is not the evidence. Look at what the application did.**

Three of the worst defects in this repository returned entirely healthy JSON while doing the
wrong thing. Every test asserting `result.handle != null` passed on all three.

---

## 1. A desktop application is not an API with a GUI attached

The tempting model is that the application has a "real" API underneath and the GUI is a skin
over it. For a thick application this is false, and the falseness is where the schedule goes.

What you actually get is three layers with different reliability:

| Layer | Reliability | Use it for |
|---|---|---|
| Object model (`Database`, `Transaction`, typed entities) | High. Deterministic, testable, composable. | Everything you can. |
| Command layer (send a command string, as a user would type) | **Low.** Fails opaquely, queues silently, depends on modal state. | Last resort, and say so in the result. |
| UI automation (windows, clicks) | Lowest. | Never, if the other two exist. |

The command layer is seductive because every feature is reachable from it and the
documentation is written in its terms. It is a trap. In this project `Editor.Command` and
`SendStringToExecute` produced `eInvalidInput` for four different approaches to applying
parametric constraints, and *silently queued* an undo so that the tool returned
`affected: 1` before anything had happened. That number was fabricated by the wrapper, not
reported by the application.

Two rules came out of this:

- **Prefer the object model even when it is five times more code.** `zoom_extents` was a
  one-line `ZOOM _E` command that failed with `eInvalidInput`; rewritten against
  `ViewTableRecord` plus an explicit extents update, it became reliable.
- **When you genuinely cannot avoid the command layer, do not invent a result.** Return
  `{queued: true, note: "..."}`. A caller can handle "I asked and cannot confirm". A caller
  cannot handle a confident lie.

## 2. Three processes, and why it is not over-engineering

The architecture that survived:

```
MCP client  ──stdio JSON-RPC──▶  server process  ──named pipe──▶  in-app plugin
(agent)                          (owns the tool                   (owns the object
                                  contract)                        model, in-process)
```

The plugin half must live inside the application's process — that is the only place the
object model exists, and in most thick applications it is single-threaded and hostile to
being called from anywhere else. But you do not want your MCP server there:

- The application restarts constantly during development. A server in-process restarts with
  it and takes the client session down.
- You cannot unit-test anything that only loads inside a running application.
- Crashing the server would crash the application, and the application is holding the user's
  unsaved work.

Splitting them means the server is an ordinary program: it builds on a CI runner, it has
tests, and a bug in it costs a restart of something cheap. The cost is a serialisation
boundary, and that boundary bites (§6).

**Corollary for CI:** the in-app half usually cannot be built by CI at all. Here the plugin
references vendor assemblies that ship only with a paid installed product, so no runner can
compile it. Rather than hide this, the build is split by a solution filter and the limit is
stated at the top of the CI file, in the pull-request template, and in the known-gaps
document. A green check that silently covers 80% of the code is worse than no check, because
people believe it.

## 3. Settle the contract before writing the code

The clearest single data point in the project.

Two categories were built the same week by the same person. The coordinate-systems category
(13 tools) shipped and passed every check **first time**. The viewports category (12 tools)
took **three attempts**.

The difference was not difficulty. Before the first line of the coordinate-systems code, a
one-page rule was written answering the awkward questions: is the parameter optional, what
does absent mean, what coordinate space do results come back in, what happens for a name
that does not exist. Four questions, one page. The viewports category was written by
starting from the API.

Write down what happens at the edges — absent argument, unknown name, legal-but-absurd
value — before you implement. Answering "what should this do?" while debugging is what turns
one attempt into three.

## 4. Fail loudly; a plausible default is worse than an error

An agent cannot see the screen. Every silent fallback becomes a wrong drawing that nobody
notices until much later.

The worst example: a "select by colour" tool took a colour index, failed to match, fell
through to an RGB comparison, and RGB `(0,0,0)` matched every entity inheriting its colour
from its layer — which is nearly everything. It returned a long list of perfectly valid
handles. Nothing in the response indicated that the filter had not filtered. A downstream
"delete these" would have emptied the drawing.

The fix was not better matching. It was: **a request expressed in colour-index terms is
answered in colour-index terms, or it is an error.**

The same rule applies to name resolution. An unknown line style is an error, not a quiet
substitution of the default. If the caller misspelled something, they need to know now.

## 5. Localisation will break your lookup tables, and it will not be systematic

If the application is localised, it very likely renames its *content*, not only its menus:
style names, layer names, symbol names, the contents of its support files.

On a Polish installation of AutoCAD, `DASHED` is `KRESKOWA` and `CENTER` is `ŚRODEK`.

The part that matters: **`CENTER` and `CENTERX2` are translated, while `CENTER2` is not.**
There is no rule. Any pattern you infer from three examples will be wrong on the fourth. It
has to be a lookup table, built by dumping the actual symbol list from a localised install,
and it has to resolve **in both directions** so that a caller may use either name.

Assume any name you did not create yourself may arrive translated.

## 6. The serialisation boundary will eat your fields, silently

Three times in this project a field vanished between the in-app plugin and the client. The
plugin computed it, put it in the payload, and the client never saw it — because the server's
data-transfer object did not declare that field, and the deserialiser dropped what it did not
recognise without complaint.

There is no error for this. The tool returns success, with less in it than it should have.

Two defences, and you want both:

- A contract test asserting that every field the producer emits is declared by the consumer.
- Reviewing the DTO as the *first* suspect whenever a value goes missing, rather than
  debugging the producer that is working fine.

The same class of bug has a second form. If a discovery tool advertises a parameter shape,
the action tool must accept exactly that shape. **Four separate defects in one review were a
catalogue advertising something the tool then refused** — dictionary parameters described as
arrays, and three catalogues whose own entry names their tool rejected. One contract test
comparing catalogue to consumer would have caught all four together. It is still the most
valuable test not yet written here.

## 7. Never write an empty catch

Two long-lived defects survived precisely because an exception was swallowed.

The expensive one: the application's system variables are 16-bit integers. Passing a 32-bit
integer throws. The call sat inside `catch { }`, so the setting silently never applied, and
the export tool it belonged to returned before the file existed. Two full restart cycles went
into a bug that a single log line would have handed over immediately.

If you must proceed on failure, log first and say why. In Python the same rule reads: use
`contextlib.suppress` so the intent is explicit at the call site rather than buried in a
comment.

And a subtler variant found while cleaning up this repository: seven places raised a new HTTP
exception from inside an `except` block without chaining the original. The status code was
right; the cause was gone. Preserve the cause — `raise ... from err`.

## 8. Verify by looking at the output

The claim from the top, with its evidence:

| Tool | Returned | Actually did |
|---|---|---|
| Revision cloud | a valid polyline handle | drew a plain rectangle — no arcs at all |
| Diametric dimension | a valid handle | labelled **every** circle `Ø0` |
| Select by colour | a plausible list of handles | selected the entire drawing |

All three reported success. All three would pass a test asserting a non-null handle, a
non-empty list, no exception.

For a tool whose output is visual, **the screenshot is the assertion.** This is why the
issue templates and the pull-request template in this repository both ask for one, and say
why they are asking. Automating this is possible — render, compare — but even a human
glancing at the result once per tool catches the entire class.

Second-order consequence: build a screenshot capability early. Not as a feature — as test
equipment.

## 9. Run one experiment, not ten guesses

When the syntax for the application's field-expression language turned out to be
undocumented in the form needed, the temptation was to try one candidate, restart, try the
next. Ten guesses, ten restarts of a heavy application.

Instead: **ten candidate expressions in a single run, side by side, results compared.** One
restart. It settled three separate questions at once, including two where the documented
answer was wrong.

Where the feedback loop is expensive — and with a thick desktop application it always is —
batch your uncertainty. Design the experiment to distinguish between hypotheses rather than
to confirm one.

## 10. Name tools for the task, not for the API

An agent finds your tool by matching a plain-language request against its name and
description. This makes naming a functional requirement rather than a matter of taste.

`add_annotation_scale` is findable. `set_object_context_collection` — the name the API would
suggest — is not, though it is the same operation.

Two things that measurably helped:

- **An intent list per tool.** Several phrasings of the request the tool answers, in every
  language your users actually speak. Here every tool carries both English and Polish, and a
  build gate enforces the field's presence.
- **Category descriptions that say what the category does *not* cover.** The description is
  the only thing an agent reads before choosing. Four of them here were a bare feature list
  of 26–29 words; rewritten to name the sibling category to use instead, they started routing
  correctly.

## 11. Automate the gate, then run it over everything

A pre-commit gate that only inspects *changed* files inspects the parts already receiving
attention. The first time this one was pointed at the whole tree it found six problems in
files nobody had touched in months — including two failures caused by a defect in the gate
itself, which had been quietly mis-reporting for as long as it had existed.

Also, from the same afternoon:

- A gate that runs a pre-built test assembly for speed will happily run a **months-old** one
  and report the result as current. A false failure wastes an hour. A false pass is the one
  that hurts. Compare the assembly's timestamp against the newest source file.
- A gate that greps output for failure patterns must have a fallback. Structural failures —
  wrong configuration, missing file — match none of the patterns, so the check reports "it
  failed" and nothing else, exactly when you need the text most.

---

## Checklist

Before shipping a tool that drives a desktop application:

- [ ] Edge-case contract written **before** the implementation
- [ ] Object model, not the command layer — and if the command layer, the result says it is
      unconfirmed rather than inventing a count
- [ ] Unknown input is an error, never a silent fallback to a plausible default
- [ ] Names that arrive from the application are resolved through a table, both directions,
      assuming localisation
- [ ] Every field the in-app half emits is declared by the server's DTO
- [ ] The discovery catalogue and the action tool agree on parameter shape
- [ ] No empty catch anywhere on the path
- [ ] Named for the task, with intent phrasings in each language your users speak
- [ ] **Somebody looked at what the application actually did**

---

*From [ToolBank for AutoCAD](https://github.com/AI4CharityPL/toolbank-autocad), MIT.
The AutoCAD-specific version of all of this lives in `docs/engineering-rules/` — 56 rules,
each written the day something surprised us.*
