using JetBrains.Annotations;

namespace Banner.Generator.Tests;

[TestSubject(typeof(ResolvedColor))]
public class ResolvedColorTest
{
    [Fact]
    public void TestResolvedColor()
    {
        Assert.Same(ResolvedColor.Default, ResolvedColor.Parse("default"));
        Assert.Equal("31", ResolvedColor.Parse("RED")!.Sgr);
        Assert.Equal("38;2;51;204;255", ResolvedColor.Parse("#33ccff")!.Sgr);
        Assert.Equal("38;2;51;204;255", ResolvedColor.Parse("#3cf")!.Sgr);
        Assert.Null(ResolvedColor.Parse("banana"));
    }
}