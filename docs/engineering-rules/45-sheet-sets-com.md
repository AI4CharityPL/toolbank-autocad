# 45 — Sheet sets, and the contract for COM

Written **before** `acad-sheetsets` exists, for the reason
[COVERAGE-ROADMAP 2.1](../COVERAGE-ROADMAP.md) gives: this is the first category in the bank that
cannot be built on the managed API, and the two categories that went best — `acad-ucs` (13/13
first time) and page setups (23/23 first time) — were both preceded by a rule rather than
followed by one.

Everything below was established by reading `AcSmComponents.Interop` metadata on AutoCAD 2025,
not from documentation. 134 types; `IAcSmSheetSetMgr`, `IAcSmSheetSet`, `IAcSmSheet`,
`IAcSmSubset`, `IAcSmCustomPropertyBag` and `IAcSmDatabase` are all present.

---

## 1. A sheet set is a FILE, not drawing state

A `.DST` file, edited through a COM object graph, entirely outside the DWG. Nothing about it lives
in a `Database`, so none of the transaction discipline in [rule 11](11-transactions.md) applies
and none of its guarantees are available either: there is no `Transaction` to abort, so a
half-finished edit stays half-finished.

**Consequence:** every write tool must leave the `.DST` in a state it would be willing to be
interrupted in, and must call `Save()` before returning.

## 2. `Save()` is explicit, and forgetting it is the defect this bank keeps finding

`IAcSmSheetSet.Save()` exists as a separate operation. A tool that renames a sheet and returns
without saving reports success over a change that will be gone. This is the same shape as
`db.LayerFilters = tree` in the layer-filter tools and `db.SummaryInfo = builder` in drawing
properties — both of which were caught only because a verification re-read the state in a
**separate call**.

**Rule:** every write path ends in `Save()`, and every verification re-reads through a **fresh
tool call** rather than trusting the reply.

## 3. Take a PATH, never hold a handle

The roadmap flagged `open_sheet_set` / `close_sheet_set` as session state and noted that every
stateful thing this bank has attempted — `refedit_*`, the plot queue, `undo` — either failed or
was withdrawn. The COM surface makes that concern avoidable:

    IAcSmSheetSetMgr.FindOpenDatabase(path)   // already open? use it
    IAcSmSheetSetMgr.OpenDatabase(path, ...)  // otherwise open it

**Rule: every sheet-set tool takes the `.DST` path as an argument and resolves it per call.** No
tool holds a database across calls, so there is no answer needed to "what happens when a second
client opens a different set" — the question does not arise. `open_sheet_set` and
`close_sheet_set` are therefore **not built**, and their absence is a design decision to state in
the category README, not an omission.

## 4. Never call `CloseAll()`

`IAcSmSheetSetMgr.CloseAll()` exists and would close sheet sets the user has open in the Sheet Set
Manager palette, discarding their unsaved work. No tool in this bank calls it. A tool that opened
a database itself closes that one, by path, and nothing else.

## 5. COM lifetime, and the `GetActiveObject` ban

`scripts/pre-commit.ps1` already refuses `Marshal.GetActiveObject` bank-wide and points at
`AcadMcp.ComBridge`'s `MarshalCompat`. That ban stands here.

Every COM object obtained must be released with `Marshal.ReleaseComObject` in a `finally`, in
reverse order of acquisition. An enumerator held past its parent is the classic way to leave
AutoCAD holding a `.DST` open after the process that touched it has gone.

## 6. The error model is different, and must be translated

COM reports failure as `COMException` carrying an `HRESULT`, which names nothing a caller can act
on — the same problem as the bare `eInvalidInput` that made `create_page_setup` undiagnosable for
a full cycle ([KNOWN-GAPS A10](../KNOWN-GAPS.md)).

**Rule:** no `COMException` reaches the caller unwrapped. Each is caught at the operation that
raised it and rethrown naming the operation, the sheet set path, and what the caller can do next.

## 7. Modal dialogs are the failure mode to design against

`SetPromptForDwgName` and `SetPromptForDwt` exist on `IAcSmSheetSet`, which means a sheet set can
be configured to **prompt**. In this session a modal AutoCAD dialog twice turned a diagnosable
failure into a ten-minute hang, and both times the cause was found by a human looking at the
screen rather than by any return value.

**Rule:** any tool that could trigger a prompt sets these to `false` first, and the category's
timeout is enforced client-side (already fixed in `PluginPipeClient`, see KNOWN-GAPS A10).

## 8. Build order — `get_sheet_property` first

`fields.insert_field_sheet_set_property` is already shipped and **blocked on this category**; it
is why title blocks cannot yet carry live sheet-set data. Whatever subset lands first must include
`get_sheet_property`, or that field stays blocked for nothing.

Suggested first tranche, read-only, which needs none of the write discipline above and unblocks
the field:

    get_sheet_set_info    get_sheet_set_path    list_sheets
    list_subsets          get_sheet_property    list_custom_properties

Then the writes, in the order a real project needs them: `set_sheet_number` and `rename_sheet`
before anything else, because renumbering is the single most common sheet-set operation and a
sheet set whose sheets cannot be renumbered is not usable.

## 9. What is NOT in this category

Decided in the roadmap revision and repeated here so it is not re-litigated:

| Tool | Belongs to |
|---|---|
| `publish_sheet_set` | `acad-publish` — a publish operation that happens to take a sheet set |
| `create_sheet_list_table`, `update_sheet_list_table` | `acad-schedules` — a sheet list is one more schedule, not a new mechanism |
| `archive_sheet_set`, `etransmit_sheet_set` | eTransmit is its own subsystem with its own packaging rules |
