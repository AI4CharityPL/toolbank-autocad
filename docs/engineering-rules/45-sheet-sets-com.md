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
in a `Database`, so none of the transaction discipline in [rule 11](11-acad-transactions.md) applies
and none of its guarantees are available either: there is no `Transaction` to abort, so a
half-finished edit stays half-finished.

**Consequence:** every write tool must leave the `.DST` in a state it would be willing to be
interrupted in, and must call `Save()` before returning.

## 2. The commit is `UnlockDb(db, bCommit: true)`, and `Save()` is a trap

*Rewritten 2026-08-06, after measuring. The original text said "every write path ends in
`Save()`" and was wrong on both halves.*

`IAcSmDatabase.Save` takes an `AcSmDSTFiler`. That is **not** a save button: `IAcSmFiler` is a
serialisation primitive you `Init(pUnk, pDb, bForWrite)` and then drive by hand with
`WriteObject` / `WriteString` / `WriteInt`. It is how the DST format writes objects. Calling it
to persist an edit would mean hand-rolling the file format.

The caller-level commit is the second argument of `UnlockDb`:

```csharp
db.LockDb(db);
// … mutate …
db.UnlockDb(db, true);   // true = commit
```

**Put the commit on the success path, not in the `finally`.** It was written the other way
first — `try { db.UnlockDb(db, true); } catch (COMException) { }` inside the `finally` — which
meant a failed save was swallowed and the tool reported success. That is precisely the shape this
whole category exists to remove. The `finally` releases only a lock still held, and there it
passes `bCommit: false`: a half-finished edit is abandoned, not persisted.

**Rule:** every write path commits via `UnlockDb(…, true)` on its success path, where the
exception can reach the caller.

### Verifying that a write persisted

A fresh tool call is **not** sufficient here, unlike everywhere else in this bank.
`IAcSmSheetSetMgr` caches the open database, so a read issued against the same path can be served
from the very object the write mutated and will agree with it whether or not anything reached the
disk. Grepping the file is useless too — a `.DST` is compressed, and not even the *original*
strings appear in its bytes in any encoding.

**Copy the file to a path the manager has never seen and read the copy.** There is no cached
database for a new path, so it must parse from disk. `scripts/verify-sheetsets.py` routes every
persistence claim through `reread_fresh()`, which does exactly that.

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

---

## 10. What a COM signature does NOT tell you

Added after building the first tranche. Three separate rounds of diagnosis went into the COM
entry layer before a single tool returned data, and none of the three causes is discoverable from
the type metadata:

| Signature says | It actually does |
|---|---|
| `FindOpenDatabase(String) -> AcSmDatabase` | **Throws `E_FAIL`** when no sheet set is open, instead of returning null. "Not open" is the normal case for a path nobody has touched, so this must be caught and treated as absence — and until it was, it threw before any later diagnostic could run, making every failure look like the same opaque error. |
| `OpenDatabase(String, Boolean)` | The flag's meaning is **not in the metadata**, and ObjectARX documents it inconsistently. Both values are attempted and each failure reported separately. Guessing a boolean's polarity has cost this repository twice already — `IdPair.IsCloned` and `CompareLayerStateToDb`, both of which made a tool report the opposite of the truth. |
| `Close(AcSmDatabase)` | Will not accept `IAcSmDatabase`. The coclass is required even though every other call in the graph is interface-typed. |

Three more, found by measuring the write half. Each is a case where the API's *shape* implies one
thing and its *behaviour* is another, and none could be read from the metadata:

| What the signature suggests | What it does |
|---|---|
| `AcSmLockStatus.acSmLockStatus_UnLocked` | No such member. They are `AcSmLockStatus_UnLocked` / `AcSmLockStatus_Locked_Local` — capital `A`. Read enum members off the assembly; do not spell them from memory. |
| `IAcSmSheet.SetName` renames a sheet | It does nothing, and reports nothing. A sheet has **no stored name**: what is displayed is composed from its number and title. Measured one variable at a time — setting only the title moved the name from `T-01 TITLE SHEET` to `T-01 PROBE TITLE`, while `SetName("PROBE-NAME")` moved nothing. AutoCAD's own command is *Rename & Renumber Sheet*, which edits both fields; that is what `rename_sheet` does. |
| `SetTitle("")` clears the title | It fails with `E_INVALIDARG`, whose message — "Value does not fall within the expected range" — names neither the value nor the range. `SetTitle(" ")` **is** accepted, and the whitespace is trimmed on the way to disk, so the saved title becomes `""`. A title can therefore be cleared; `""` is simply not the argument that does it. `set_sheet_title` translates `""` to `" "` and reports the `""` that persists, rather than the `" "` that is briefly in memory. |

That last one is worth generalising: **`GetX()` immediately after `SetX()` can disagree with the
file.** The tool answered `" "` while the disk held `""`. A read-back inside the same call is
evidence that the object changed, never that the file did.

And one more, from the subsets:

| What the signature suggests | What it does |
|---|---|
| `InsertComponent(comp, before)` moves a component into a subset | It **adds**; it does not re-parent. Called on a sheet that still belongs to another subset it fails `E_INVALIDARG`; called on one already under the target it answers `0x800288C6` *duplicate identifier*. The second message is what explained the first. A move is `owner.RemoveSheet(sheet)` followed by `target.InsertComponent(sheet, null)`. |
| A sheet or sheet set **is** a property bag | It is not. `IAcSmSheet` implements exactly `IAcSmComponent` and `IAcSmPersist`. The bag is **fetched**: `GetCustomPropertyBag()`. See the section below — this one shipped. |
| `ImportSheet(layoutRef)` files the new sheet under the subset | It **builds** the sheet and returns it; the sheet was not findable in the tree afterwards and had to be `InsertComponent`ed explicitly. Third instance of the same split in this one API — creating an object and placing it are separate steps. Insert only when the sheet is missing, since inserting one the subset already holds answers `0x800288C6`. |
| `IAcSmAcDbLayoutReference` needs AXDBLib | Only `SetAcDbObject` / `ResolveAcDbObject` do. The reference can be built entirely from strings — `SetFileName`, `SetName`, `SetAcDbHandle` — which keeps this category free of an interop assembly that is not reliably installed. The layout's handle comes from a side-loaded `Database.ReadDwgFile`, so a sheet can be added from any drawing on disk without disturbing what the user has open. |
| `new AcSmCustomPropertyValue()` gives you a usable object | Half of one. It is not attached to a database until `InitNew(owner)` runs, and `SetValue` on an uninitialised value throws a bare `NullReferenceException` from inside the interop layer — no HRESULT, nothing naming initialisation as the problem. Every `IAcSmPersist` carries `InitNew` for this reason. |

### The nearest miss: a wrong cast that produced an EMPTY RESULT

`list_custom_properties` and the `custom` half of `get_sheet_property` shipped testing
`x is IAcSmCustomPropertyBag bag` and falling back to an empty dictionary. Since neither a sheet
nor a sheet set implements that interface, the test **always** failed, and both tools answered
"no custom properties" for every sheet set ever passed to them. Both were verified at the time.

The failure had no exception, no wrong value, no crooked geometry on screen. **A call that
succeeds and returns nothing is indistinguishable from a sheet set that has nothing in it.** It
was found only when the write tools forced the question of whether the bag was reachable at all,
and confirmed by pointing the fixed reads at AutoCAD's sample set, which turned out to have
carried `Client Name: ALLAN CONSTRUCTION LTD.` and eleven others the whole time.

**Rule:** a read tool that can return an empty collection must be verified against a fixture
**known to be non-empty**. Asserting that the call succeeded proves nothing about a tool whose
failure mode is silence. `scripts/verify-sheetsets.py` now writes a property and reads it back,
so empty can never pass again.

`RemoveSheet` **detaches rather than destroys** — measured, by counting the set's sheets before
and after a full move-out-and-back cycle and finding them equal. That was worth establishing
rather than assuming, and the way to establish it safely is the next rule.

### Verify inside the lock, before the commit

When a step's behaviour is unknown and the downside is destroying user data, check the outcome
**while still holding the lock and before committing**. `move_sheet_to_subset` re-finds the sheet
after the remove-and-insert; if it were gone, it throws, the `finally` unlocks with
`bCommit: false`, and the `.DST` on disk is untouched.

That safety net exists only because the commit sits on the success path (§2). With the commit in
the `finally`, a destroyed sheet would have been saved.

**The rule this produces:** in COM, a nullable-looking return may throw, an undocumented flag is a
coin toss to be tried rather than guessed, interface types are not interchangeable with their
coclasses, and a setter may be a no-op that says nothing. None of that is true of the managed API,
which is why this category needed its own contract before its own code.

Verified against `Sample/Sheet Sets/Architectural/IRD Addition.dst`: 11 sheets, 2 subsets
(Architectural 6, Structural 4), sheets addressable by name and by number.
