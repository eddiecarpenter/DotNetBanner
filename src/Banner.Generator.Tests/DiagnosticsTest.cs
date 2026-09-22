using JetBrains.Annotations;

namespace Banner.Generator.Tests;

[TestSubject(typeof(Diagnostics))]
public class DiagnosticsTest
{
    [Fact]
    public void UnknownFontDiagnostic_CarriesTheIdAndASuggestion()
    {
        var d = Diagnostics.UnknownFontDiagnostic("dooom");

        Assert.Equal("BAN001", d.Id);
        Assert.Contains("doom", d.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownFontColorDiagnostic_CarriesTheIdAndTheOffendingValue()
    {
        var d = Diagnostics.UnknownFontColorDiagnostic("banana");

        Assert.Equal("BAN002", d.Id);
        Assert.Contains("banana", d.GetMessage(), StringComparison.Ordinal);
    }
}
