// Roslyn source generator for [McpTool] methods.
//   - Scans for [McpTool] usages
//   - Emits ACAD0001..ACAD0099 diagnostics for malformed declarations
//   - Generates one IToolCatalog implementation per <Category> folder
//
// Diagnostics:
//   ACAD0001  [McpTool] missing Intent or fewer than 5 entries combined PL+EN
//   ACAD0002  Tool name does not match snake_case + max-5-words pattern
//   ACAD0003  Tool method must be static
//   ACAD0004  Tool category attr does not match folder name
//   ACAD0005  Two tools share the same Name within one category

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AcadMcp.SourceGen;

[Generator(LanguageNames.CSharp)]
public sealed class McpToolGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "AcadMcp.Shared.Mcp.McpToolAttribute";

    private static readonly Regex ToolNameRegex = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    private static readonly DiagnosticDescriptor MissingIntent = new(
        "ACAD0001",
        "[McpTool] missing Intent examples",
        "Tool '{0}' must declare Intent with at least 5 PL+EN example phrases (got {1}). " +
        "Intent powers MCPBank semantic discovery via mcpd_find. See rule 20-mcp-tool-attribute.md.",
        "AcadMcp.Mcp",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadToolName = new(
        "ACAD0002",
        "Invalid MCP tool name",
        "Tool name '{0}' must be snake_case, start with a letter, and contain at most 5 words separated by underscore. See rule 21-mcp-tool-naming.md.",
        "AcadMcp.Mcp",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotStatic = new(
        "ACAD0003",
        "MCP tool method must be static",
        "Method '{0}' marked [McpTool] must be static. Tool methods are stateless dispatchers; state lives in DI services. See rule 24-mcp-tool-category-binding.md.",
        "AcadMcp.Mcp",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CategoryFolderMismatch = new(
        "ACAD0004",
        "[McpTool] Category does not match folder",
        "Tool '{0}' declares Category='{1}' but lives in folder '{2}'. Category and folder name must match (kebab-case folder, kebab-or-snake-or-pascal in attribute is fine after normalization).",
        "AcadMcp.Mcp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateName = new(
        "ACAD0005",
        "Duplicate MCP tool name within category",
        "Tool name '{0}' is declared more than once in category '{1}'. Each name must be unique per category.",
        "AcadMcp.Mcp",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var toolMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => Capture(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        var grouped = toolMethods.Collect();

        context.RegisterSourceOutput(grouped, (spc, all) =>
        {
            if (all.IsDefaultOrEmpty) return;

            var byCategory = all
                .GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase);

            foreach (var group in byCategory)
            {
                var seenNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in group)
                {
                    if (!seenNames.Add(t.ToolName))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(DuplicateName, t.NameLocation, t.ToolName, group.Key));
                    }
                }

                EmitCatalogFor(spc, group.Key, group.ToImmutableArray());
            }

            foreach (var t in all)
            {
                ValidateAndDiagnose(spc, t);
            }
        });
    }

    private static ToolCapture? Capture(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;
        var attrData = ctx.Attributes.FirstOrDefault();
        if (attrData is null) return null;

        string toolName = attrData.ConstructorArguments.Length > 0
            ? attrData.ConstructorArguments[0].Value as string ?? ""
            : "";
        string description = attrData.ConstructorArguments.Length > 1
            ? attrData.ConstructorArguments[1].Value as string ?? ""
            : "";
        string category = attrData.ConstructorArguments.Length > 2
            ? attrData.ConstructorArguments[2].Value as string ?? ""
            : "";

        var intent = ImmutableArray<string>.Empty;
        bool readOnly = false, comFallback = false, requiresPlugin = false;
        int strategy = 0;

        foreach (var named in attrData.NamedArguments)
        {
            switch (named.Key)
            {
                case "Intent":
                    if (named.Value.Kind == TypedConstantKind.Array)
                    {
                        intent = named.Value.Values
                            .Select(v => v.Value as string ?? "")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToImmutableArray();
                    }
                    break;
                case "ReadOnly":      readOnly = named.Value.Value is bool b1 && b1; break;
                case "ComFallback":   comFallback = named.Value.Value is bool b2 && b2; break;
                case "RequiresPlugin": requiresPlugin = named.Value.Value is bool b3 && b3; break;
                case "Strategy":      strategy = named.Value.Value is int s ? s : 0; break;
            }
        }

        var location = attrData.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                       ?? method.Locations.FirstOrDefault()
                       ?? Location.None;

        var filePath = location.SourceTree?.FilePath ?? "";
        var folderName = ExtractCategoryFolder(filePath);

        var parameters = ExtractParameters(method);

        return new ToolCapture(
            Parameters: parameters,
            ToolName: toolName,
            Description: description,
            Category: category,
            FolderName: folderName,
            Intent: intent,
            ReadOnly: readOnly,
            ComFallback: comFallback,
            RequiresPlugin: requiresPlugin,
            Strategy: strategy,
            IsStatic: method.IsStatic,
            DeclaringTypeFullName: method.ContainingType.ToDisplayString(),
            MethodName: method.Name,
            ResultTypeFullName: method.ReturnType.ToDisplayString(),
            NameLocation: location,
            FilePath: filePath);
    }

    /// <summary>
    /// Pulls the tool's input contract out of its args record.
    ///
    /// Tool methods look like <c>(IPluginGateway gw, DrawLineArgs args, CancellationToken ct)</c>
    /// per rule 19-tool-implementation-pattern.md, so the args type is the one parameter that is
    /// neither the gateway interface nor the cancellation token. Its primary-constructor
    /// parameters are the tool's inputs, and the matching properties carry the
    /// [property: JsonPropertyName("...")] that decides the wire name.
    ///
    /// Without this the emitted catalog reports no parameters at all, every tool advertises
    /// an empty inputSchema, and no MCP client can work out how to call anything.
    /// </summary>
    private static ImmutableArray<ParamCapture> ExtractParameters(IMethodSymbol method)
    {
        var argsType = FindArgsType(method);
        if (argsType is null) return ImmutableArray<ParamCapture>.Empty;

        var ctor = argsType.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0)
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (ctor is null) return ImmutableArray<ParamCapture>.Empty;

        var props = argsType.GetMembers()
            .OfType<IPropertySymbol>()
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var builder = ImmutableArray.CreateBuilder<ParamCapture>();
        foreach (var p in ctor.Parameters)
        {
            props.TryGetValue(p.Name, out var prop);

            var jsonName = prop is not null ? ReadJsonPropertyName(prop) : null;
            jsonName ??= ReadJsonPropertyName(p) ?? CamelCase(p.Name);

            // A parameter is optional if it has a default, or if it is a nullable reference /
            // Nullable<T>. Everything else the caller genuinely has to supply.
            bool hasDefault = p.HasExplicitDefaultValue;
            bool nullable = p.Type.NullableAnnotation == NullableAnnotation.Annotated
                            || p.Type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T;

            builder.Add(new ParamCapture(
                Name: p.Name,
                JsonName: jsonName!,
                TypeOfExpression: TypeOfExpression(p.Type),
                Required: !hasDefault && !nullable,
                Description: prop is not null ? ReadSummary(prop) : null,
                DefaultLiteral: hasDefault ? LiteralOf(p.ExplicitDefaultValue) : "null"));
        }
        return builder.ToImmutable();
    }

    private static INamedTypeSymbol? FindArgsType(IMethodSymbol method)
    {
        foreach (var p in method.Parameters)
        {
            if (p.Type is not INamedTypeSymbol named) continue;
            if (named.Name == "CancellationToken") continue;
            // Gateways (IPluginGateway, IVisionGateway) are injected, not caller-supplied.
            if (named.TypeKind == TypeKind.Interface) continue;
            if (named.TypeKind != TypeKind.Class && named.TypeKind != TypeKind.Struct) continue;
            return named;
        }
        return null;
    }

    private static string? ReadJsonPropertyName(ISymbol symbol)
    {
        foreach (var a in symbol.GetAttributes())
        {
            if (a.AttributeClass?.Name is "JsonPropertyNameAttribute" or "JsonPropertyName"
                && a.ConstructorArguments.Length > 0
                && a.ConstructorArguments[0].Value is string s
                && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }
        return null;
    }

    /// <summary>First sentence of the XML doc summary, if the property has one.</summary>
    private static string? ReadSummary(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return null;
        var m = Regex.Match(xml, @"<summary>\s*(.*?)\s*</summary>", RegexOptions.Singleline);
        if (!m.Success) return null;
        var text = Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Fully-qualified typeof(...) expression, with the nullable annotation stripped.</summary>
    private static string TypeOfExpression(ITypeSymbol type)
    {
        var bare = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        return "typeof(" + bare.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")";
    }

    private static string LiteralOf(object? value) => value switch
    {
        null => "null",
        string s => "\"" + Escape(s) + "\"",
        bool b => b ? "true" : "false",
        double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "d",
        float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f",
        decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
        int or long or short or byte => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!,
        _ => "null",
    };

    private static string CamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length == 1) return s.ToLowerInvariant();
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    private static string ExtractCategoryFolder(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "";
        var parts = filePath.Replace('\\', '/').Split('/');
        var idx = Array.FindIndex(parts, p => string.Equals(p, "Categories", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && idx + 1 < parts.Length)
        {
            return parts[idx + 1];
        }
        return "";
    }

    private static void ValidateAndDiagnose(SourceProductionContext spc, ToolCapture t)
    {
        if (t.Intent.Length < 5)
        {
            spc.ReportDiagnostic(Diagnostic.Create(MissingIntent, t.NameLocation, t.ToolName, t.Intent.Length));
        }

        if (string.IsNullOrEmpty(t.ToolName) || !ToolNameRegex.IsMatch(t.ToolName) || t.ToolName.Split('_').Length > 5)
        {
            spc.ReportDiagnostic(Diagnostic.Create(BadToolName, t.NameLocation, t.ToolName));
        }

        if (!t.IsStatic)
        {
            spc.ReportDiagnostic(Diagnostic.Create(NotStatic, t.NameLocation, t.MethodName));
        }

        if (!string.IsNullOrEmpty(t.FolderName)
            && !string.IsNullOrEmpty(t.Category)
            && !string.Equals(NormalizeId(t.Category), NormalizeId(t.FolderName), StringComparison.OrdinalIgnoreCase))
        {
            spc.ReportDiagnostic(Diagnostic.Create(CategoryFolderMismatch, t.NameLocation, t.ToolName, t.Category, t.FolderName));
        }
    }

    private static string NormalizeId(string s)
        => new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static void EmitCatalogFor(SourceProductionContext spc, string category, ImmutableArray<ToolCapture> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> by AcadMcp.SourceGen");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using AcadMcp.Shared.Mcp;");
        sb.AppendLine();
        sb.AppendLine($"namespace AcadMcp.Backend.Categories.{Sanitize(category)}.Generated;");
        sb.AppendLine();
        sb.AppendLine($"internal sealed class {Sanitize(category)}Catalog : IToolCatalog");
        sb.AppendLine("{");
        sb.AppendLine($"    public string Category => \"{LowerKebab(category)}\";");
        sb.AppendLine();
        sb.AppendLine("    public IReadOnlyList<McpToolMetadata> Tools { get; } = new List<McpToolMetadata>");
        sb.AppendLine("    {");
        foreach (var t in tools)
        {
            var intentLiteral = t.Intent.Length == 0
                ? "System.Array.Empty<string>()"
                : "new string[] { " + string.Join(", ", t.Intent.Select(s => "\"" + Escape(s) + "\"")) + " }";
            sb.AppendLine("        new McpToolMetadata(");
            sb.AppendLine($"            Name: \"{Escape(t.ToolName)}\",");
            sb.AppendLine($"            Description: \"{Escape(t.Description)}\",");
            sb.AppendLine($"            Category: \"{LowerKebab(t.Category)}\",");
            sb.AppendLine($"            Intent: {intentLiteral},");
            sb.AppendLine($"            ReadOnly: {(t.ReadOnly ? "true" : "false")},");
            sb.AppendLine($"            ComFallback: {(t.ComFallback ? "true" : "false")},");
            sb.AppendLine($"            RequiresPlugin: {(t.RequiresPlugin ? "true" : "false")},");
            sb.AppendLine($"            Strategy: (ExecutionStrategy){t.Strategy},");
            sb.AppendLine($"            Parameters: {ParametersLiteral(t.Parameters)},");
            sb.AppendLine($"            ResultType: typeof({t.ResultTypeFullName.Replace("?", "")}),");
            sb.AppendLine($"            DeclaringTypeFullName: \"{Escape(t.DeclaringTypeFullName)}\",");
            sb.AppendLine($"            MethodName: \"{Escape(t.MethodName)}\"),");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");

        spc.AddSource($"{Sanitize(category)}Catalog.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string ParametersLiteral(ImmutableArray<ParamCapture> ps)
    {
        if (ps.IsDefaultOrEmpty) return "Array.Empty<McpParameter>()";
        var items = ps.Select(p =>
            "new McpParameter(" +
            $"Name: \"{Escape(p.Name)}\", " +
            $"ClrType: {p.TypeOfExpression}, " +
            $"JsonName: \"{Escape(p.JsonName)}\", " +
            $"Required: {(p.Required ? "true" : "false")}, " +
            $"Description: {(p.Description is null ? "null" : "\"" + Escape(p.Description) + "\"")}, " +
            $"DefaultValue: {p.DefaultLiteral})");
        return "new McpParameter[] { " + string.Join(", ", items) + " }";
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Unknown";
        var chars = s.Where(c => char.IsLetterOrDigit(c)).ToArray();
        if (chars.Length == 0) return "Unknown";
        var first = chars[0];
        if (!char.IsLetter(first)) chars[0] = '_';
        return new string(chars);
    }

    private static string LowerKebab(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))))
            {
                sb.Append('-');
            }
            if (c == '_' || c == ' ') sb.Append('-');
            else sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Trim('-');
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed record ParamCapture(
        string Name,
        string JsonName,
        string TypeOfExpression,
        bool Required,
        string? Description,
        string DefaultLiteral);

    private sealed record ToolCapture(
        ImmutableArray<ParamCapture> Parameters,
        string ToolName,
        string Description,
        string Category,
        string FolderName,
        ImmutableArray<string> Intent,
        bool ReadOnly,
        bool ComFallback,
        bool RequiresPlugin,
        int Strategy,
        bool IsStatic,
        string DeclaringTypeFullName,
        string MethodName,
        string ResultTypeFullName,
        Location NameLocation,
        string FilePath);
}
