# AutoCAD text / dimension / table API traps

AutoCAD text / MText / dimension / leader / table API landmines. Read BEFORE touching DBText, MText, AttributeReference, Dimension subclasses, MLeader or Table.

These are the documented sharp edges encountered (or known up-front) while building `acad-annotations` and `acad-dimensions` in Phase 3. They are not bugs - they are the actual public surface. **Do not "fix" them.**

## 1. `DBText` vs `MText` are NOT interchangeable

| API class | namespace | use for |
| --------- | --------- | ------- |
| `DBText`  | `Autodesk.AutoCAD.DatabaseServices` | single-line, no formatting (the old `_TEXT` command) |
| `MText`   | `Autodesk.AutoCAD.DatabaseServices` | multi-line, `\fArial|b1|i0|;...` inline formatting (the `_MTEXT` command) |
| `AttributeDefinition` / `AttributeReference` | same | text inside a block definition / a block reference |

`DBText.TextString` does NOT understand MText format codes. Putting `\P` or `\fArial...;` into `DBText.TextString` produces literal garbage on the screen. Always pick the right class for the job.

## 2. `MText` width = 0 means "auto" (single unwrapped line)

```csharp
mtext.Width = 0.0;   // auto-width: never wraps. Looks like a long DBText.
mtext.Width = 100.0; // wraps at 100 units of WCS width.
```

If a tool exposes `width` on its DTO, treat `null` / `0` as "auto" and document it. Do not default to a tiny value or the text will wrap into a single column of letters.

## 3. `MText` content must be encoded with the AutoCAD escape rules

Special characters need escaping when building inline-formatted MText programmatically. The minimum safe set:

| literal | encoded |
| ------- | ------- |
| `\` | `\\` |
| `{` | `\{` |
| `}` | `\}` |
| line break | `\P` (NOT `\n`) |
| non-breaking space | `\~` |

If the `MText.Contents` payload is user-supplied freeform text, run it through a small `EncodeMTextLiteral(string)` helper. Never `string.Format` user text directly into a format-code string.

## 4. `TextStyleTableRecord` must exist before assignment

`DBText.TextStyleId` and `MText.TextStyleId` must reference a record already in the `TextStyleTable`. Setting `.TextStyleId = ObjectId.Null` throws. The safe pattern is:

```csharp
var styles = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
ObjectId styleId = styles.Has(name) ? styles[name] : styles["Standard"]; // "Standard" always exists
```

## 5. Dimensions are concrete subclasses, not a single `Dimension` factory

There is no "create dimension" call. You instantiate the right subclass directly:

| user wants | class to `new` |
| ---------- | -------------- |
| linear (horiz / vert / rotated) | `RotatedDimension` |
| aligned (parallel to a segment) | `AlignedDimension` |
| angular (3 points)              | `Point3AngularDimension` |
| angular (2 lines)               | `LineAngularDimension2` |
| radius                          | `RadialDimension` |
| diameter                        | `DiametricDimension` |
| arc-length                      | `ArcDimension` |
| ordinate (X or Y from origin)   | `OrdinateDimension` |

Each constructor takes its own argument layout (e.g. `RotatedDimension(rotation, point1, point2, dimLinePoint, text, dimStyleId)`). Do not paper over them with one super-DTO; expose each as its own MCP tool.

## 6. `DimensionStyleTableRecord` must be set explicitly

Newly constructed dimensions have `DimensionStyle = ObjectId.Null` and render with `Standard`. To use the user's intended style:

```csharp
var dst = (DimStyleTableRecord)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
ObjectId styleId = dst.Has(styleName) ? dst[styleName] : db.Dimstyle; // db.Dimstyle = current
dim.DimensionStyle = styleId;
```

## 7. Baseline / continued dimensions need a *prior* dimension as anchor

The traditional `_DIMBASELINE` and `_DIMCONTINUE` commands chain off the *previous* dimension. There is no `BaselineDimension` class - you build N `RotatedDimension`s with the same `point1` (baseline) or with `point1` = previous `point2` (continued), staggered by `Dimscale * Dimdli`. Treat this as a multi-entity batch tool, not one entity.

## 8. `MLeader` is not "MText with a line"

`MLeader` is its own beast: it carries a content (MText OR block), 1..N leader lines, dogleg geometry and a landing. The ergonomic pattern is:

```csharp
var ml = new MLeader();
ml.SetDatabaseDefaults();
int leaderIdx = ml.AddLeader();
int lineIdx   = ml.AddLeaderLine(leaderIdx);
ml.AddFirstVertex(lineIdx, vertex);
ml.MText = mtextContent;             // OR ml.BlockContentId = blockDefId
ml.TextAlignmentType = TextAlignmentType.LeftAlignment;
btr.AppendEntity(ml);
tr.AddNewlyCreatedDBObject(ml, true);
```

The MText sub-object passed in **must already have `SetDatabaseDefaults()` called or formatting silently breaks**.

## 9. `Table` rows/columns use `SetCellValue`, not `[row,col]=...`

```csharp
var t = new Table();
t.TableStyle = db.Tablestyle;     // current style; never ObjectId.Null
t.SetSize(rows, cols);
t.SetRowHeight(20);
t.SetColumnWidth(50);
t.Cells[r, c].TextString = "value"; // 2024+ accessor
// older builds: t.SetTextString(r, c, "value");
t.GenerateLayout();                  // MUST call before AppendEntity or cells render empty
btr.AppendEntity(t);
```

`GenerateLayout()` is mandatory and cheap. Forgetting it is the #1 reason a Table appears as an empty grid.

## 10. `AttachmentPoint` and justification cheat-sheet

For both `MText.Attachment` and `DBText.HorizontalMode/VerticalMode`, AutoCAD uses **9 anchor positions in row-major order**:

```
TopLeft   TopCenter   TopRight       (1,2,3)
MiddleLeft MiddleCenter MiddleRight  (4,5,6)
BottomLeft BottomCenter BottomRight  (7,8,9)
```

`MText` exposes them as the `AttachmentPoint` enum directly. `DBText` requires setting BOTH `HorizontalMode` and `VerticalMode` and then calling `db.TileMode`-aware `AdjustAlignment(db)` so the anchor takes effect:

```csharp
text.HorizontalMode = TextHorizontalMode.TextCenter;
text.VerticalMode   = TextVerticalMode.TextVerticalMid;
text.AlignmentPoint = anchor;       // becomes the actual screen position
text.AdjustAlignment(db);           // mandatory for non-Left/Base alignment
```

Skipping `AdjustAlignment` makes `Position` stay authoritative and the alignment fields become a no-op.

## When in doubt

1. Re-read this file before touching the involved class.
2. Add a one-line `// trap #N (rule 27)` comment on any line that survives a "looks weird" review.
3. If you discover a new trap, append it here in the same numbered format **before** committing the C# fix.
