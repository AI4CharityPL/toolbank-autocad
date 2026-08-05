// Builds JSON Schema for a tool's inputs from its McpParameter metadata.
//
// Shared by CategoryServer (tools/list) and RouterServer (acad_load_category) so the two
// can never disagree about what a tool's contract looks like - that kind of drift is
// exactly what rule 31-toolbank-discovery-hygiene.md is about.
//
// Why this exists at all: an MCP client only ever learns how to call a tool from this
// schema. A bare {"type":"object"} for a Point2dDto tells the model nothing, and it will
// guess - usually wrong. So complex types are expanded structurally, arrays get an items
// schema, and enums get their permitted values.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Mcp;

public static class JsonSchemaBuilder
{
    // Deep enough for the nesting this codebase actually uses (args -> DTO -> point/colour),
    // shallow enough that a self-referencing DTO cannot blow the stack or the context window.
    private const int MaxDepth = 6;

    // Reflecting the same DTO on every tools/list call is pure waste: the shape never
    // changes for the lifetime of the process.
    private static readonly Dictionary<Type, JsonObject> _cache = new();
    private static readonly object _cacheLock = new();

    public static JsonObject BuildToolSchema(McpToolMetadata t)
    {
        var props = new JsonObject();
        var required = new JsonArray();

        foreach (var p in t.Parameters)
        {
            var node = ForType(p.ClrType, 0, new HashSet<Type>());
            if (!string.IsNullOrWhiteSpace(p.Description))
            {
                node["description"] = p.Description;
            }
            if (p.DefaultValue is not null)
            {
                node["default"] = JsonValue.Create(p.DefaultValue);
            }
            props[p.JsonName] = node;
            if (p.Required) required.Add(p.JsonName);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject ForType(Type type, int depth, HashSet<Type> seen)
    {
        // double? and string? both mean "may be omitted"; the schema describes the payload.
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string) || type == typeof(char) || type == typeof(Guid))
            return new JsonObject { ["type"] = "string" };
        if (type == typeof(bool))
            return new JsonObject { ["type"] = "boolean" };
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
            type == typeof(byte) || type == typeof(uint) || type == typeof(ulong))
            return new JsonObject { ["type"] = "integer" };
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return new JsonObject { ["type"] = "number" };
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new JsonObject { ["type"] = "string", ["format"] = "date-time" };

        if (type.IsEnum)
        {
            var values = new JsonArray();
            foreach (var n in Enum.GetNames(type)) values.Add(n);
            return new JsonObject { ["type"] = "string", ["enum"] = values };
        }

        // Dictionaries BEFORE collections. IReadOnlyDictionary<string,string> also implements
        // IEnumerable<KeyValuePair<string,string>>, so the collection branch below would
        // describe it as an array of {key, value} objects - which is not what
        // System.Text.Json binds. A caller following that schema sends a JSON array and
        // deserialization fails outright ("could not be converted to
        // IReadOnlyDictionary`2"), which is exactly what set_block_reference_attributes did.
        var valueType = DictionaryValueTypeOf(type);
        if (valueType is not null)
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = depth >= MaxDepth
                    ? new JsonObject { ["type"] = "object" }
                    : ForType(valueType, depth + 1, seen),
            };
        }

        var element = ElementTypeOf(type);
        if (element is not null)
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = depth >= MaxDepth
                    ? new JsonObject { ["type"] = "object" }
                    : ForType(element, depth + 1, seen),
            };
        }

        if (type == typeof(object) || depth >= MaxDepth)
            return new JsonObject { ["type"] = "object" };

        // A DTO that contains itself (directly or through a chain) would recurse forever.
        if (!seen.Add(type))
            return new JsonObject { ["type"] = "object" };

        try
        {
            if (depth == 0)
            {
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(type, out var hit)) return (JsonObject)hit.DeepClone();
                }
            }

            var built = BuildObjectSchema(type, depth, seen);

            if (depth == 0)
            {
                lock (_cacheLock) { _cache[type] = (JsonObject)built.DeepClone(); }
            }
            return built;
        }
        finally
        {
            seen.Remove(type);
        }
    }

    private static JsonObject BuildObjectSchema(Type type, int depth, HashSet<Type> seen)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        var nullability = new NullabilityInfoContext();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? CamelCase(prop.Name);

            var node = ForType(prop.PropertyType, depth + 1, seen);
            var summary = DescriptionOf(prop);
            if (summary is not null) node["description"] = summary;
            props[jsonName] = node;

            if (IsRequired(prop, nullability)) required.Add(jsonName);
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
        };
        if (required.Count > 0) schema["required"] = required;
        return schema;
    }

    private static bool IsRequired(PropertyInfo prop, NullabilityInfoContext ctx)
    {
        var t = prop.PropertyType;
        if (Nullable.GetUnderlyingType(t) is not null) return false;
        if (t.IsValueType) return true;

        // Nullable reference annotations survive into metadata, so "string?" really is optional.
        try { return ctx.Create(prop).ReadState == NullabilityState.NotNull; }
        catch { return false; }
    }

    private static string? DescriptionOf(PropertyInfo prop)
    {
        var d = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
        return string.IsNullOrWhiteSpace(d) ? null : d;
    }

    /// <summary>
    /// Value type of a string-keyed dictionary, or null when the type is not one.
    /// Only string keys map onto a JSON object; anything else has no JSON representation
    /// as an object and is better described as the array it really is.
    /// </summary>
    private static Type? DictionaryValueTypeOf(Type type)
    {
        foreach (var i in new[] { type }.Concat(type.GetInterfaces()))
        {
            if (!i.IsGenericType) continue;
            var def = i.GetGenericTypeDefinition();
            if (def != typeof(IDictionary<,>) && def != typeof(IReadOnlyDictionary<,>)) continue;

            var args = i.GetGenericArguments();
            if (args[0] == typeof(string)) return args[1];
        }
        return null;
    }

    /// <summary>Element type for arrays and generic collections; null for anything else.</summary>
    private static Type? ElementTypeOf(Type type)
    {
        if (type == typeof(string)) return null;
        if (type.IsArray) return type.GetElementType();
        if (!typeof(IEnumerable).IsAssignableFrom(type)) return null;

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(IEnumerable<>) || def == typeof(IReadOnlyList<>) ||
                def == typeof(IReadOnlyCollection<>) || def == typeof(IList<>) ||
                def == typeof(ICollection<>) || def == typeof(List<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        var iface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return iface?.GetGenericArguments()[0];
    }

    private static string CamelCase(string s)
        => string.IsNullOrEmpty(s) ? s
         : s.Length == 1 ? s.ToLowerInvariant()
         : char.ToLowerInvariant(s[0]) + s.Substring(1);
}
