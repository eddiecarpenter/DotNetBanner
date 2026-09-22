using Banner.Generator;
using JetBrains.Annotations;

namespace Banner.Generator.Tests;

[TestSubject(typeof(FontCatalog))]
public class FontCatalogTest
{
    
    [Fact]
    public void Suggest_FindsNearMisses()
    {
        Assert.Equal("doom", FontCatalog.Suggest("dooom"));
        Assert.Equal("colossal", FontCatalog.Suggest("colosal"));
        Assert.Null(FontCatalog.Suggest("zzzzzzzzzz"));
    }

    [Fact]
    public void Open_IsCaseInsensitive()
    {
        using var stream = FontCatalog.Open("DOOm");
        Assert.NotNull(stream);
    }
}