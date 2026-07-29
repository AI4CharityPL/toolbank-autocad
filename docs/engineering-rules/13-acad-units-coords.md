# Coordinate systems, units, angles

Coordinate systems, units, and angles. Inputs are explicit; outputs are explicit.

The agent doesn't know if `(10, 20)` is mm, m, inches, or kilometers. It also doesn't know if `45` is degrees or radians. Be explicit at every boundary.

## Rules

1. **All angles in API: degrees** (LLM-friendly). Convert to radians inside the plugin via `MathHelpers.DegToRad`.
2. **All coordinates: drawing units** of the active document (whatever `INSUNITS` says). Tools MUST NOT assume mm.
3. **Tools that need a specific unit** (e.g. line weights in mm per ISO 128) take a parameter `unit` (default to mm) and convert.
4. **Tool result includes `unitsUsed` field** when there's any ambiguity (`{"x": 10, "y": 20, "unitsUsed": "mm"}`).
5. **Coordinate system: WCS by default.** UCS-relative tools must accept `coordinateSystem: "wcs" | "ucs"` (default `"wcs"`).
6. **Z = 0 default for 2D entities** but always emitted explicitly so 3D consumers don't choke.

## Bad

```csharp
public static DrawCircleResult DrawCircle(double x, double y, double radius)
{
    // What units? What CS?
}
```

## Good

```csharp
[McpTool(name: "draw_circle", description: "Draw a circle in current layer at given center and radius.",
    category: "geometry-2d",
    Intent = new[] {
        "narysuj okrag o promieniu",
        "stworz kolo w punkcie",
        "draw a circle at point",
        "create circle entity",
        "make circle on current layer"
    })]
public static DrawCircleResult DrawCircle(DrawCircleArgs args, CancellationToken ct = default)
{
    // args.Center is Point3dDto in drawing units (WCS).
    // args.Radius is in drawing units (defaults to mm-equivalent if INSUNITS=mm).
    // No angle param here, but if there were, it would be degrees.
}

public sealed record DrawCircleArgs(
    Point3dDto Center,
    double Radius,
    string? Layer = null,
    string CoordinateSystem = "wcs");
```

## INSUNITS gotcha

If a tool computes geometry from a real-world spec (e.g. ISO 128 line weight 0.13 mm), it MUST inspect `Database.Insunits` and convert. Helper: `_Shared/UnitConversion.cs` (Phase 1).
