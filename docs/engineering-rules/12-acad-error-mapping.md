# Map AutoCAD exceptions to AcadErrorCode

Translate AutoCAD-specific exceptions to AcadErrorCode. Never let raw stack traces cross the pipe.

The agent on the other side of the pipe doesn't know what `Autodesk.AutoCAD.Runtime.Exception ErrorStatus = eWasErased` means. It needs an actionable, structured error.

## Rule

Every `catch (Autodesk.AutoCAD.Runtime.Exception ex)` MUST translate `ex.ErrorStatus` to `AcadErrorCode` via `AcadErrorMap.Translate(ex.ErrorStatus)`. Then throw `AcadException(code, hint, ex)` and let the pipe layer serialize.

## Mandatory translations

| `ErrorStatus`          | AcadErrorCode               | Hint to include                                          |
| ---------------------- | --------------------------- | -------------------------------------------------------- |
| `eNoDocument`          | `NoActiveDocument`          | "Open a document first or call acad_status to verify."   |
| `eLockViolation`       | `DocumentLocked`            | "Another command/agent is editing. Retry shortly."       |
| `eWasErased`           | `EntityNotFound`            | "Entity was erased. Re-query selection."                 |
| `eKeyNotFound`         | `LayerNotFound`/`BlockNotFound` (context) | "Verify name spelling and case."           |
| `eInvalidInput`        | `InvalidArgument`           | "Check parameter ranges and types."                      |
| `eNotApplicable`       | `NotSupportedOnLT`          | "Operation requires full AutoCAD (not LT)."              |
| anything else          | `AcadException`             | Include the original ErrorStatus name in details         |

## Bad

```csharp
catch (Autodesk.AutoCAD.Runtime.Exception ex)
{
    return new ToolResponse(false, null, new ErrorInfo(AcadErrorCode.Unknown, ex.Message)); // no hint, no code
}
```

## Good

```csharp
catch (Autodesk.AutoCAD.Runtime.Exception ex)
{
    var code = AcadErrorMap.Translate(ex.ErrorStatus);
    var hint = AcadErrorMap.Hint(code);
    throw new AcadException(code, ex.Message, hint, ex)
        .WithDetail("errorStatus", ex.ErrorStatus.ToString());
}
```

## Forbidden

- Returning raw .NET stack traces in `ErrorInfo.Message`. Stacks go to logs, not to the agent.
- Using `AcadErrorCode.Unknown` if any of the table above applies.
- Swallowing exceptions silently (`catch { }`).
- Adding new `AcadErrorCode` values WITHOUT also extending the table above and a regression test in `tests/AcadMcp.Tests/ErrorMapTests.cs`.
