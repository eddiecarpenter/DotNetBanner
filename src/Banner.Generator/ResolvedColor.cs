using System.Text.RegularExpressions;

namespace Banner.Generator;

internal sealed record ResolvedColor(string? Sgr)
{
    /// <summary>The terminal default: no colour codes are emitted.</summary>
    internal static readonly ResolvedColor Default = new((string?)null);

    /// <summary>
    ///     The named colours: the eight standard ANSI colours, their bright variants, and orange.
    ///     Standard colours use the 16-colour codes; orange has no 16-colour slot, so it uses a
    ///     24-bit truecolor sequence.
    /// </summary>
    internal static readonly Dictionary<string, ResolvedColor> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new ResolvedColor("30"),
        ["red"] = new ResolvedColor("31"),
        ["green"] = new ResolvedColor("32"),
        ["yellow"] = new ResolvedColor("33"),
        ["blue"] = new ResolvedColor("34"),
        ["magenta"] = new ResolvedColor("35"),
        ["cyan"] = new ResolvedColor("36"),
        ["white"] = new ResolvedColor("37"),
        ["orange"] = new ResolvedColor("38;2;255;165;0"),
        ["bright-black"] = new ResolvedColor("90"),
        ["bright-red"] = new ResolvedColor("91"),
        ["bright-green"] = new ResolvedColor("92"),
        ["bright-yellow"] = new ResolvedColor("93"),
        ["bright-blue"] = new ResolvedColor("94"),
        ["bright-magenta"] = new ResolvedColor("95"),
        ["bright-cyan"] = new ResolvedColor("96"),
        ["bright-white"] = new ResolvedColor("97")
    };

    private static readonly Regex HexPattern = new(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");

    internal bool IsDefault => Sgr is null;

    /// <summary>
    ///     Resolves a colour token to its ANSI parameter. A null or blank token, or <c>default</c>,
    ///     gives <see cref="Default" />; an unrecognised token gives <c>null</c>, meaning
    ///     "this is not a colour" — which callers treat differently from "the terminal default".
    /// </summary>
    internal static ResolvedColor? Parse(string? token)
    {
        if (token is null) return Default;

        var value = token.Trim();

        if (value.Length == 0 || string.Equals(value, "default", StringComparison.OrdinalIgnoreCase)) return Default;

        var hex = HexPattern.Match(value);
        if (hex.Success) return Rgb(hex.Groups[1].Value);

        return Named.TryGetValue(value, out var named) ? named : null;
    }

    private static ResolvedColor Rgb(string hex)
    {
        // #rgb is shorthand for #rrggbb: each digit doubles.
        if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);

        return new ResolvedColor($"38;2;{r};{g};{b}");
    }
}