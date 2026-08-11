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

        // FIXED 2026-08-11: run_command_sequence now goes through LispCommandBridge, a queued
        // [CommandMethod] that gives Editor.Command a genuine COMMAND context instead of the
        // APPLICATION context every other tool dispatches from.
        "run_command_sequence",

        // FIXED 2026-08-11, second pass: eval_lisp, load_lisp_file and list_loaded_lisp now go
        // through the same queued-SendStringToExecute mechanism, WITHOUT a [CommandMethod] - the
        // command line accepts LISP directly when typed, so the wrapper is queued as-is and the
        // user's expression travels through a request FILE rather than being embedded in it.
        "eval_lisp", "load_lisp_file", "list_loaded_lisp",

        // FIXED 2026-08-11, third pass, WITH THE USER'S EXPLICIT GO-AHEAD: netload_assembly -
        // dynamic assembly loading is a materially different risk from evaluating LISP or
        // queuing drawing commands, so this one was built only after being asked about
        // specifically. Uses the run_command_sequence [CommandMethod] bridge (_.NETLOAD is a
        // COMMAND, not a LISP form), with FILEDIA forced to 0 - the same precedent already fixed
        // for run_script_file's SCRIPT. "Loaded" is read back via
        // AppDomain.CurrentDomain.GetAssemblies(), since DynamicLinker does not see .NET
        // assemblies loaded this way.
        "netload_assembly",

        // STILL WITHDRAWN: run_script_file was built on the run_command_sequence bridge but even
        // a single CIRCLE inside a .scr draws nothing - two measured fix attempts, both wrong
        // (see LispTools.cs). It is the only one of the original six left unbuilt.

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
