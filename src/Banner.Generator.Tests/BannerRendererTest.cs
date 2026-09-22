using Banner.Generator;
using JetBrains.Annotations;

namespace Banner.Generator.Tests;

[TestSubject(typeof(BannerRenderer))]
public class BannerRendererTest
{
    [Fact]
    public void ParseMarkup_StripsMarkersAndRecordsTransitions()
    {
        var markup = BannerRenderer.ParseMarkup("{red}Dot{cyan}net", ResolvedColor.Default);

        Assert.Equal("Dotnet", markup.CleanText);
        Assert.Equal(3, markup.Transitions.Count);
        Assert.Equal(3, markup.Transitions[2].Index);
    }

    [Fact]
    public void ParseMarkup_LeavesUnknownTokensVerbatim()
    {
        var markup = BannerRenderer.ParseMarkup("{nope}Hi", ResolvedColor.Default);

        Assert.Equal("{nope}Hi", markup.CleanText);
        Assert.Single(markup.Transitions);
    }

    [Fact]
    public void ColorForChar_AppliesEachTransitionFromItsOwnIndex()
    {
        var markup = BannerRenderer.ParseMarkup("{red}Dot{cyan}net", ResolvedColor.Default);

        var colors = BannerRenderer.ColorForChar(markup, markup.CleanText.Length);

        Assert.Equal("31", colors[0].Sgr); // D
        Assert.Equal("31", colors[2].Sgr); // t — last red character
        Assert.Equal("36", colors[3].Sgr); // n — first cyan character
        Assert.Equal("36", colors[5].Sgr); // t
    }

    [Fact]
    public void PaintRow_WrapsEachColourRunOnce()
    {
        var red = ResolvedColor.Parse("red")!;
        var cyan = ResolvedColor.Parse("cyan")!;

        // Cells 0-1 carry the first input character's ink, cells 2-3 the second's.
        var painted = BannerRenderer.PaintRow("ABCD", [0, 0, 1, 1], [red, cyan]);

        Assert.Equal("\u001b[31mAB\u001b[0m\u001b[36mCD\u001b[0m", painted);
    }

    [Fact]
    public void PaintRow_LeavesUnownedCellsUncoloured()
    {
        var red = ResolvedColor.Parse("red")!;

        // The middle cell is blank — no character's ink reaches it.
        var painted = BannerRenderer.PaintRow("A B", [0, -1, 0], [red]);

        Assert.Equal("\u001b[31mA\u001b[0m \u001b[31mB\u001b[0m", painted);
    }

    [Fact]
    public void RenderBanner_KeepsEscapeCodesOutOfThePlainVariant()
    {
        using var stream = FontCatalog.Open("standard")!;
        var rendered = BannerRenderer.RenderBanner(
            stream, "{red}A{cyan}B", ResolvedColor.Default, Alignment.Left, 1);

        Assert.DoesNotContain("\u001b", rendered.Plain, StringComparison.Ordinal);
        Assert.Contains("\u001b[31m", rendered.Colored, StringComparison.Ordinal);
        Assert.Contains("\u001b[36m", rendered.Colored, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBanner_CentresShorterLinesWithinTheWidest()
    {
        using var stream = FontCatalog.Open("standard")!;

        var rendered = BannerRenderer.RenderBanner(
            stream, @"Hello\nHi", ResolvedColor.Default, Alignment.Center, 1);

        var lines = rendered.Plain.Split('\n');

        // The "Hi" rows should be indented; the "Hello" rows should not.
        Assert.StartsWith(" ", lines[7], StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBanner_AlignmentChangesTheLayout()
    {
        using var l = FontCatalog.Open("doom")!;
        using var c = FontCatalog.Open("doom")!;

        var left = BannerRenderer.RenderBanner(l, @"My Service\nTesting", ResolvedColor.Default, Alignment.Left, 1);
        var centre = BannerRenderer.RenderBanner(c, @"My Service\nTesting", ResolvedColor.Default, Alignment.Center, 1);

        Assert.NotEqual(left.Plain, centre.Plain);
    }
}
