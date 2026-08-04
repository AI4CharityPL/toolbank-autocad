# 44. Named page setups

Settled before `acad-publish` was written, for the reason
[rule 43](43-coordinate-systems.md) exists: the categories in this bank that were contracted
first passed their live check first time, and the ones that were not needed three attempts.

## What a page setup is, and what it is not

A **page setup** is a *named* `PlotSettings` object living in the drawing's plot-settings
dictionary. It is not the same thing as a layout's own plot configuration:

| | Layout's own settings | Named page setup |
|---|---|---|
| Where it lives | On the `Layout` itself | `db.PlotSettingsDictionaryId`, under a name |
| How many | One per layout, unnamed | Any number, shared |
| Reusable | No | Yes — apply to many layouts, import into other drawings |
| Existing tool | `layouts.configure_plot` | this category |

`layouts.configure_plot` stays exactly as it is. It configures one layout directly and is the
right tool when there is nothing to reuse. The two do not overlap and neither is a
"better version" of the other.

## The contract

1. **A name is required and names are the identity.** No positional or index-based access to
   page setups. An unknown name is an error listing the names that exist, never a fallback to
   a default setup — plotting an issued sheet through the wrong page setup is a silent,
   expensive mistake.

2. **`create_page_setup` never overwrites by default.** `overwrite: false` is the default and a
   name collision is an error. A firm's standard page setups are the kind of thing an agent
   should not be able to quietly redefine. Passing `overwrite: true` is the caller saying they
   meant it.

3. **Two ways to create, and they are explicit.** Either `fromLayout` (snapshot that layout's
   current plot configuration under a name) or the explicit device/media/scale arguments.
   Supplying both is an error rather than a precedence rule nobody will remember.

4. **`apply_page_setup` requires the caller to say which layouts.** There is no "all layouts"
   default. Applying a page setup to every tab in a drawing because the argument was omitted is
   exactly the class of accident this bank spent a sweep removing. `layouts: [...]` names them;
   `allLayouts: true` is available and has to be typed.

5. **Applying reports per layout.** Some layouts may fail — a page setup naming a device that
   this machine does not have, say. The result lists every layout with its outcome rather than
   an affected count, so a partial success is visible as a partial success.

6. **`import_page_setup` reads a side database and never modifies the source.** The source
   drawing is opened read-only. A name collision follows rule 2: refused unless `overwrite`.

7. **Device and media names are validated against what AutoCAD actually has**, and the error
   lists them. This is the same discipline as the annotation-scale and visual-style tools: the
   set is per-machine and per-drawing, so the caller cannot be expected to know it in advance.

## Deliberately not here

- **Applying a page setup at plot time as a one-off.** `files.export_file` already plots with
  explicit settings and does not need a named setup to do it.
- **`set_plot_stamp`.** Plot stamps are a per-plot decoration configured through `PLOTSTAMP`,
  not part of `PlotSettings`; it belongs with the plotting tools, not here.
