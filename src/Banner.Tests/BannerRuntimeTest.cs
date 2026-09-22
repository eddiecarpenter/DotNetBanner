using Banner;

namespace Banner.Tests;

public class BannerRuntimeTest
{
    private const string Esc = "\u001b";

    /// <summary>
    /// The parameterless <c>Banner()</c> consults the console, which a test cannot control.
    /// The explicit overload is what makes the selection testable at all.
    /// </summary>
    private static void Given(string plain, string colored, bool poweredBy = false)
        => BannerRuntime.Register(plain, colored, poweredBy);

    [Fact]
    public void Banner_WithoutColour_ReturnsThePlainVariant()
    {
        Given("plain\n", $"{Esc}[31mcoloured{Esc}[0m\n");

        var banner = BannerRuntime.Banner(useColor: false, poweredBy: false);

        // The reason two variants exist: a redirected stream or a log file must never see escape codes.
        Assert.DoesNotContain(Esc, banner, StringComparison.Ordinal);
        Assert.Contains("plain", banner, StringComparison.Ordinal);
    }

    [Fact]
    public void Banner_WithColour_ReturnsTheColouredVariant()
    {
        Given("plain\n", $"{Esc}[31mcoloured{Esc}[0m\n");

        var banner = BannerRuntime.Banner(useColor: true, poweredBy: false);

        Assert.Contains(Esc, banner, StringComparison.Ordinal);
        Assert.Contains("coloured", banner, StringComparison.Ordinal);
    }

    [Fact]
    public void Banner_WithPoweredBy_AppendsTheRuntimeTagline()
    {
        Given("plain\n", "plain\n");

        var with = BannerRuntime.Banner(useColor: false, poweredBy: true);
        var without = BannerRuntime.Banner(useColor: false, poweredBy: false);

        Assert.Contains("Powered by .NET", with, StringComparison.Ordinal);
        Assert.DoesNotContain("Powered by", without, StringComparison.Ordinal);
    }

    [Fact]
    public void Banner_TaglineIsMeasuredAgainstThePlainWidth()
    {
        // The coloured variant is far longer in characters than it is wide in columns; measuring it
        // instead of the plain one would push the tagline off the right of the terminal.
        // The banner has to be wider than the tagline, or there is no padding to get wrong.
        var art = new string('x', 60);
        var plain = art + "\n";
        Given(plain, $"{Esc}[31m{art}{Esc}[0m\n");

        var banner = BannerRuntime.Banner(useColor: true, poweredBy: true);
        var tagline = banner.Split('\n').First(line => line.Contains("Powered by", StringComparison.Ordinal));

        // Padded to the banner's width, not the coloured string's length, and right-aligned.
        Assert.Equal(art.Length, tagline.Length);
        Assert.StartsWith(" ", tagline, StringComparison.Ordinal);
    }
}
