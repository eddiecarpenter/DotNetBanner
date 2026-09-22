using Banner.Generator;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Banner.Generator.Tests;

[TestSubject(typeof(Figlet))]
public class FigletTest
{

    private readonly ITestOutputHelper _output;

    public FigletTest(ITestOutputHelper output) => _output = output;
    
    private static Stream OpenFont(string name)
    {
        var resource = $"Banner.Generator.Fonts.{name}.flf";

        return typeof(Figlet).Assembly.GetManifestResourceStream(resource)
               ?? throw new InvalidOperationException($"Font resource not found: {resource}");
    }


    /// <summary>
    /// Compares banners without caring about trailing whitespace, which editors strip and
    /// which carries no visual meaning in a rendered banner.
    /// </summary>
    private static string Normalize(string s) =>
        string.Join("\n", s.Split('\n').Select(line => line.TrimEnd())).TrimEnd();

    [Fact]
    public void Parse_StandardFont_ReadsHeader()
    {
        using var stream = OpenFont("standard");

        var figlet = Figlet.Parse(stream);

        Assert.Equal(6, figlet.Height);
        Assert.Equal('$', figlet.HardBlank);
        Assert.Equal(102, figlet.GlyphCount);
    }
    
    [Fact]
    public void Render_Hi()
    {
        using var stream = OpenFont("standard");

        var expected = """
             _   _ _ 
            | | | (_)
            | |_| | |
            |  _  | |
            |_| |_|_|
            """;
        var art = Figlet.ConvertOneLine(stream, "Hi");
        Assert.Equal(Normalize(expected), Normalize(art));
    }
    
    [Fact]
    public void AllFonts_Parse()
    {
        var fonts = typeof(Figlet).Assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".flf", StringComparison.Ordinal));

        foreach (var font in fonts)
        {
            using var stream = typeof(Figlet).Assembly.GetManifestResourceStream(font)!;
            var art = Figlet.ConvertOneLine(stream, "Banner");

            _output.WriteLine($"=== {font} ===");
            _output.WriteLine(art);
        }
    }
}