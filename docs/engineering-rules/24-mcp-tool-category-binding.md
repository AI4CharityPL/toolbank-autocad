# Category binding

Each tool is bound to one category. No cross-category references.

Each `[McpTool]` lives in `src/AcadMcp.Backend/Categories/<Folder>/`. The folder name is the category. Cross-category references are FORBIDDEN.

## Rules

1. **Tool method must be `static`** (source generator emits `ACAD0003` if not). State lives in DI services accessed via `[FromKeyedServices]` if needed.
2. **`category:` attribute argument MUST normalize to folder name** (kebab/snake/Pascal all OK after `[a-zA-Z0-9]` filtering and lowercasing). Source generator emits `ACAD0004` warning if mismatched.
3. **A tool in `Categories/Geometry2D/` must NOT reference a type in `Categories/Architecture/`** (no `using` line, no fully-qualified). Architecture test `NoCrossCategoryRefs` (NetArchTest, Phase 1) catches this.
4. **Cross-category helpers** (e.g. `UnitConversion`, `EntityHandleConverter`) live in `Categories/_Shared/` and ARE referenceable from any category.
5. **Cross-category business orchestration** (e.g., "draw wall AND auto-dimension it") lives in `Categories/Workflows/` (Phase 6) and explicitly invokes other categories' tools through a `IToolInvoker` service - it does NOT take a hard reference.

## Why no cross-refs

The system grows to 30 categories. Letting them couple = a refactor in `geometry-2d` breaks `architecture` six categories away. Architecture tests turn into archaeology.

## File layout pattern

```
Categories/Geometry2D/
├── _Helpers/                  ← category-private helpers (forbidden from elsewhere)
│   ├── CircleHelpers.cs
│   └── PolylineHelpers.cs
├── CircleTools.cs             ← static [McpTool] methods + Args/Result records
├── PolylineTools.cs
├── LineTools.cs
└── README.md                  ← category overview, tool count, design notes
```

## Bad

```csharp
// Categories/Architecture/WallTools.cs
using AcadMcp.Backend.Categories.Geometry2D;        // FORBIDDEN cross-category

[McpTool(name: "draw_wall", category: "architecture", ...)]
public static DrawWallResult DrawWall(DrawWallArgs args, CancellationToken ct)
{
    var line = LineTools.DrawLine(new(...), ct);    // direct call across categories
    // ...
}
```

## Good

```csharp
// Categories/Architecture/WallTools.cs
using AcadMcp.Backend.Categories._Shared;
using AcadMcp.Backend.Mcp;

[McpTool(name: "draw_wall", category: "architecture", ...)]
public static DrawWallResult DrawWall(DrawWallArgs args, CancellationToken ct)
{
    // Option A (preferred): use shared geometry helpers
    var ids = SharedDrawing.DrawWallSegments(args, ct);

    // Option B (when truly orchestrating): call via IToolInvoker
    var invoker = ServiceLocator.Get<IToolInvoker>();
    var lineResult = invoker.Invoke<DrawLineResult>("geometry-2d", "draw_line", new { start = ..., end = ... }, ct);
    // ...
}
```

## Workflow category exception

`Categories/Workflows/` IS the place for tools that compose other categories. The `IToolInvoker` indirection makes the dependency dynamic instead of compile-time, and survives a category being absent (returns structured "tool not loaded" error).
