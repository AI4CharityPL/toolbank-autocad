// Smoke + regression test for the acad-data category.
// Asserts catalog completeness, snake_case names, RequiresPlugin/ReadOnly flags and
// Intent >= 5 examples per tool (rule 22).

using System.Linq;
using AcadMcp.Backend.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class DataTests
{
    private static readonly string[] ExpectedTools = new[]
    {
        // Phase 5.2, first tranche: xdata, dictionaries and xrecords. All ordinary Database work -
        // nothing here goes near the command line, which is what makes it buildable when 5.1 was
        // not (rule 26 SS15).
        "attach_xdata", "get_xdata", "delete_xdata",
        "register_app_name", "list_registered_apps",
        "create_extension_dictionary", "list_dictionaries",
        "get_dictionary_entry", "set_dictionary_entry", "delete_dictionary_entry",
        "create_xrecord", "read_xrecord", "update_xrecord",

        // Second tranche. The tagging and query tools sit on the xdata layer above rather than
        // inventing a second storage mechanism. The CSV pair is OWN WORK: Table.ExportToCsv and
        // Table.ImportFromCsv do not exist in the managed API - measured, not assumed.
        "tag_entities", "list_tagged_entities", "query_by_property",
        "export_table_to_csv", "import_csv_to_table",

        // Third tranche: data links. Database.DataLinkManagerId does NOT exist -
        // Database.DataLinkManager does - and DataLinkManager.UpdateDataLink is absent, so the
        // update runs through Table.UpdateDataLink. Listing walks the ACAD_DATALINK dictionary,
        // GetDataLink taking a name and offering no enumeration.
        "create_data_link", "list_data_links", "link_table_to_source",
        "unlink_table", "update_data_link",
    };

    private static ToolRegistry NewRegistry() => new(new NullLogger<ToolRegistry>());

    [Fact]
    public void Catalog_contains_all_expected_tools()
    {
        var tools = NewRegistry().ToolsFor("data").Select(t => t.Name).OrderBy(n => n).ToArray();
        var expected = ExpectedTools.OrderBy(n => n).ToArray();
        Assert.Equal(expected, tools);
    }

    [Fact]
    public void All_tool_names_are_snake_case_and_short()
    {
        foreach (var t in NewRegistry().ToolsFor("data"))
        {
            Assert.Matches(@"^[a-z][a-z0-9_]*$", t.Name);
            Assert.True(t.Name.Split('_').Length <= 5, $"{t.Name} > 5 words");
        }
    }

    [Fact]
    public void All_tools_require_plugin()
    {
        foreach (var t in NewRegistry().ToolsFor("data"))
        {
            Assert.True(t.RequiresPlugin, $"{t.Name} should require the plugin");
        }
    }

    [Fact]
    public void Read_only_tools_are_marked()
    {
        foreach (var t in NewRegistry().ToolsFor("data"))
        {
            if (t.Name.StartsWith("get_") || t.Name.StartsWith("list_"))
                Assert.True(t.ReadOnly, $"{t.Name} should be ReadOnly = true");
        }
    }

    [Fact]
    public void Every_tool_has_at_least_5_intents()
    {
        foreach (var t in NewRegistry().ToolsFor("data"))
        {
            Assert.True(t.Intent.Count >= 5,
                $"{t.Name} has only {t.Intent.Count} intents (need >= 5 PL+EN combined)");
        }
    }
}
