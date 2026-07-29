# `[McpTool]` is the contract

McpTool attribute requirements - Intent, Description, Category. Enforced by source generator.

The source generator `AcadMcp.SourceGen` SCANS for `[McpTool]` and emits the per-category `IToolCatalog`. Mistakes here = build error or invisible tool.

## Required fields

```csharp
[McpTool(
    name: "draw_circle",                       // snake_case, max 5 words, see rule 21
    description: "Draw a circle in current layer at given center point and radius. " +
                 "Honors INSUNITS for radius interpretation.",
    category: "geometry-2d",                   // MUST match folder name (rule 24)
    Intent = new[] {
        // 5+ examples MIN, mix PL and EN ~half/half. Powers MCPBank semantic search.
        "narysuj okrag o promieniu 100",
        "stworz kolo w punkcie 0,0",
        "dodaj okrag do rysunku",
        "wstaw kolo na warstwie A-WALL",
        "draw a circle at origin with radius 50",
        "create circle entity at coordinates",
        "make round shape on current layer",
        "add a circle to model space"
    },
    RequiresPlugin = true,        // false if pure local computation or works via COM
    ComFallback = false,          // true if you provide a COM-bridge variant
    ReadOnly = false,             // true if no DB writes
    Strategy = ExecutionStrategy.Plugin)]
public static DrawCircleResult DrawCircle(DrawCircleArgs args, CancellationToken ct = default) { ... }
```

## Hard rules

1. **`Intent` REQUIRED, MIN 5 entries.** Source generator emits `ACAD0001` if missing or fewer.
2. **At least 30% Polish, at least 30% English.** Hard to enforce automatically right now (Phase 6 will add detection); manually check.
3. **Description in English.** LLMs handle better, MCPBank semantic search is multilingual.
4. **`Description` includes the unit story** for any quantity (see rule 13).
5. **`Description` mentions side effects** (creates entity, modifies layer, deletes selection, ...).
6. **No emojis.** No marketing language. No "blazing fast". Just what it does.
7. **Tool method MUST be static** (rule 24). Source generator emits `ACAD0003` if not.
8. **One DTO `Args` record per tool** as the only non-CancellationToken parameter. No primitive arg explosions.

## Bad

```csharp
[McpTool("circle", "draws stuff", "geom")]   // missing Intent, vague description, fake category
public DrawResult DrawCircle(double x, double y, double r) { ... }  // not static, primitive args, no unit info
```

## Good

```csharp
[McpTool(
    name: "draw_circle",
    description: "Draw a circle on the active layer (or args.Layer if provided). " +
                 "Center and radius are in current drawing units (see INSUNITS). " +
                 "Returns the new entity's handle.",
    category: "geometry-2d",
    Intent = new[] { "narysuj okrag", "stworz kolo", "draw circle", "create circle entity", "add circle to drawing" },
    RequiresPlugin = true)]
public static DrawCircleResult DrawCircle(DrawCircleArgs args, CancellationToken ct = default)
    => CircleHelpers.DrawAsync(args, ct).GetAwaiter().GetResult();

public sealed record DrawCircleArgs(Point3dDto Center, double Radius, string? Layer = null);
public sealed record DrawCircleResult(EntityHandle Entity, string UnitsUsed);
```

## Why Intent matters

`mcpd_find` in MCPBank does cosine similarity between user query and `intent_examples` from manifest. The richer + more diverse + more multilingual your Intent, the easier the agent finds your tool.

A tool with weak Intent IS broken even if it works perfectly. Nobody will call it.
