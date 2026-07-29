# Tests required for every tool

Required tests for every new tool. Unit + contract + (when feasible) E2E.

A tool without a test = bug-in-waiting. Source generator can't verify your AutoCAD logic; tests can.

## Per-tool test layers

| Layer       | Required?                          | What it tests                                                |
| ----------- | ---------------------------------- | ------------------------------------------------------------ |
| Unit        | Yes for tools with helpers/parsing | Pure logic in `_Helpers/` (no AutoCAD dep)                   |
| Contract    | Yes, always                        | `[McpTool]` attribute valid; tool name regex; intent count   |
| Manifest    | Yes, always (auto)                 | `pwsh scripts/check-manifests.ps1` syncs manifest ↔ tool list |
| Integration | When mockable                      | Tool method routed correctly; arg deserialization            |
| E2E         | Yes for new categories             | Real AutoCAD; only runs on dev box with `[Trait("Acad","real")]` |

## Unit tests

Helpers in `Categories/<X>/_Helpers/` MUST have unit tests. Pure functions (math, parsing, name normalization) must be 100% line covered.

```csharp
public class UnitConversionTests
{
    [Theory]
    [InlineData(0.13, "mm", "in", 0.005118110236)]
    [InlineData(1.0, "in", "mm", 25.4)]
    public void Mm_To_Inch_Round_Trip(double v, string from, string to, double expected)
    {
        Assert.Equal(expected, UnitConversion.Convert(v, from, to), precision: 6);
    }
}
```

## Contract tests

One generic test that loops over every `IToolCatalog` and asserts:

- `Name` matches `^[a-z][a-z0-9_]*$` and ≤ 5 words
- `Intent.Count >= 5`
- `Description` is non-empty and ≥ 30 chars
- `Category == folder name` (normalized)
- Method exists, is static, accepts `Args` record, returns `Result` record (or `Task<Result>`)

This test lives in `tests/AcadMcp.Tests/ContractTests.cs`. NEW tools are covered automatically - no per-tool boilerplate.

## E2E tests

Live in `tests/e2e/`. Tagged `[Trait("Acad", "real")]`. Skipped in CI (no AutoCAD there). Run locally before merging a new category:

```powershell
dotnet test tests/AcadMcp.Tests --filter "Acad=real"
```

E2E test pattern:
1. Start `AcadMcp.Backend.exe --category geometry-2d` as subprocess
2. Send MCP `initialize` → `tools/list` → `tools/call`
3. Inspect AutoCAD via plugin's introspection tool that returns entity counts/handles
4. Cleanup with `acad_restore_checkpoint`

## Architecture tests (NetArchTest)

Live in `tests/AcadMcp.Tests/ArchitectureTests.cs`. Run on every build. Examples:

```csharp
[Fact]
public void Categories_Must_Not_Reference_Other_Categories()
{
    var result = Types.InAssembly(typeof(Program).Assembly)
        .That().ResideInNamespace("AcadMcp.Backend.Categories.Geometry2D")
        .ShouldNot().HaveDependencyOnAny("AcadMcp.Backend.Categories.Architecture", /* etc */)
        .GetResult();
    Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
}

[Fact]
public void Shared_Must_Not_Reference_AutoCAD()
{
    var result = Types.InAssembly(typeof(Shared.PipeProtocol).Assembly)
        .ShouldNot().HaveDependencyOnAny("Autodesk.AutoCAD")
        .GetResult();
    Assert.True(result.IsSuccessful);
}
```

## Skipping tests

Tests CAN'T be `[Skip]` without an issue link in the skip reason. Pre-commit hook greps for `Skip = "..."` without `(#NN)`.
