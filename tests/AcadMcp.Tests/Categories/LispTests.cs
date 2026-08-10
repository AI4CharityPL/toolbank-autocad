// Smoke + regression test for the acad-lisp category.
// Asserts catalog completeness, snake_case names, RequiresPlugin/ReadOnly flags and
// Intent >= 5 examples per tool (rule 22).

using System.Linq;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class LispTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        // Phase 5.1. Everything here runs on a SYNCHRONOUS route - Application.Invoke,
        // Editor.Command, Application.Get/SetSystemVariable - because SendStringToExecute queues
        // and a result that cannot be observed is not a result.
        "list_loaded_applications",
        "get_system_variable", "set_system_variable", "list_system_variables",
        "purge_regapps",

        // WITHDRAWN on one measured root cause: eval_lisp, load_lisp_file, list_loaded_lisp,
        // run_command_sequence, run_script_file and netload_assembly all need a COMMAND context.
        // Application.Invoke, Editor.Command and LoadModule each answer eInvalidInput from the
        // APPLICATION context this plugin dispatches in. The fix is
        // ExecuteInCommandContextAsync and an async runner - next tranche.

        // define_command_alias is struck: AutoCAD exposes no API for command aliases, and the only
        // route is editing the user's global acad.pgp and reloading it with RE_INIT - a permanent
        // change to the AutoCAD installation rather than to a drawing.
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var tools = NewRegistry().ToolsFor("lisp").Select(t => t.Name).OrderBy(n => n).ToArray();
        var expected = ExpectedTools.OrderBy(n => n).ToArray();
        Assert.Equal(expected, tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        foreach (var t in NewRegistry().ToolsFor("lisp"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5, $"{t.Name} > 5 words");
        }
    }

    [Fact]
    public void All_tools_require_plugin()
    {
        foreach (var t in NewRegistry().ToolsFor("lisp"))
        {
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
        }
    }

    [Fact]
    public void Read_only_tools_are_marked()
    {
        foreach (var t in NewRegistry().ToolsFor("lisp"))
        {
            if (t.Name.StartsWith("get_") || t.Name.StartsWith("list_"))
                Assert.True(t.ReadOnly, $"{t.Name} should be ReadOnly = true");
        }
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        foreach (var t in NewRegistry().ToolsFor("lisp"))
        {
            Assert.True(t.Intent.Count >= 5,
                $"{t.Name} has only {t.Intent.Count} intents (need >= 5 PL+EN combined)");
        }
    }
}
