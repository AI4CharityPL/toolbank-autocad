// Catalogue test for acad-annotations.

using System.Linq;
using AcadMcp.Backend.Mcp;
using AcadMcp.Shared.Mcp;
using Xunit;

namespace AcadMcp.Tests.Categories;

public class AnnotationsTests
{
    // Was Assert.NotEmpty, which passes at 15 tools, at 12, and at one - it could not tell a
    // category that grew from one that had been gutted. Named and counted, like every other
    // category's catalogue test.
    [Fact]
    public void Catalog_contains_all_twentysix_annotation_tools()
    {
        var registry = new ToolRegistry();
        var tools = registry.ToolsFor("annotations");
        Assert.Equal(26, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        Assert.Contains("add_dbtext", names);
        Assert.Contains("update_dbtext", names);
        Assert.Contains("add_mtext", names);
        Assert.Contains("update_mtext", names);
        Assert.Contains("add_mleader_text", names);
        Assert.Contains("add_mleader_block", names);
        Assert.Contains("add_table", names);
        Assert.Contains("set_table_cell", names);
        Assert.Contains("create_text_style", names);
        Assert.Contains("set_current_text_style", names);
        Assert.Contains("list_text_styles", names);
        Assert.Contains("delete_text_style", names);
        // Roadmap 3.3, first tranche. All three read the SIX places text lives, not the two
        // obvious ones: text, mtext, mleaders, block attributes, table cells and dimension
        // text overrides.
        Assert.Contains("list_text_by_pattern", names);
        Assert.Contains("find_replace_text", names);
        Assert.Contains("export_text_content", names);
        // Second tranche. All three would report success while leaving the text somewhere
        // else, so each measures the position or size back off the entity.
        Assert.Contains("set_text_justification", names);
        Assert.Contains("text_fit", names);
        Assert.Contains("scale_text_in_place", names);
        // Third tranche. A mask cannot be measured on the entity at all - it is drawn behind
        // the text and changes no extent - so that one is proved on an exported image, and the
        // columns one is proved by the text reflowing wider and shorter.
        Assert.Contains("background_mask_mtext", names);
        Assert.Contains("mtext_column_settings", names);
        // Fourth tranche. Both write control codes, and they need different proofs: a symbol
        // inserted as a CHARACTER can be read back off the entity, one inserted as a %% code
        // cannot - DBText hands back the code. A stacked fraction renders identically to an
        // unstacked one, so it is proved on the drawn extent.
        Assert.Contains("insert_symbol", names);
        Assert.Contains("stack_fraction", names);
        // Fifth tranche. Inverses, so they are verified as a round trip - and the lines are
        // created out of reading order, because combining by handle order shuffles the
        // sentences while every count in the result stays correct.
        Assert.Contains("text_to_mtext", names);
        Assert.Contains("explode_mtext_to_text", names);
        // Sixth tranche. set_mtext_frame was built alongside these two and WITHDRAWN: the only
        // frame property the API has accepts the assignment and draws nothing, so it is asserted
        // absent rather than present.
        Assert.Contains("set_paragraph_format", names);
        Assert.Contains("mtext_bullets_numbering", names);
        Assert.DoesNotContain("set_mtext_frame", names);
    }
}
