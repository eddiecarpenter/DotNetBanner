using System.Reflection;

namespace Banner.Generator;

/// <summary>
/// The FIGlet fonts embedded in this assembly. Knows how resource names map to font names,
/// so nothing else has to.
/// </summary>
internal static class FontCatalog
{
    private const string Suffix = ".flf";
    private const string FontPrefix = "Banner.Generator.Fonts.";

    private static string[] AvailableFonts() =>
        typeof(FontCatalog).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(FontPrefix, StringComparison.Ordinal)
                        && n.EndsWith(Suffix, StringComparison.Ordinal))
            .Select(n => n.Substring(FontPrefix.Length, n.Length - FontPrefix.Length - Suffix.Length))
            .ToArray();

    internal static string Hint(string name)
    {
        var suggestion = Suggest(name);

        return suggestion is null
            ? "See FIGLET-FONTS.md for the available fonts."
            : $"Did you mean '{suggestion}'?";
    }

    /// <summary>Opens the named font, matched case-insensitively, or null if there is no such font.</summary>
    internal static Stream? Open(string name)
    {
        var match = AvailableFonts()
            .FirstOrDefault(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? null
            : typeof(Figlet).Assembly.GetManifestResourceStream(FontPrefix + match + Suffix);
    }

    /// <summary>The closest font name to <paramref name="name"/>, or null if nothing is close.</summary>
    internal static string? Suggest(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var target = name.ToLowerInvariant();

        var best = AvailableFonts()
            .Select(font => (Font: font, Distance: Levenshtein(font.ToLowerInvariant(), target)))
            .OrderBy(candidate => candidate.Distance)
            .First();

        return best.Distance <= 3 ? best.Font : null;
    }

    /// <summary>
    /// The Levenshtein edit distance between two strings: the minimum number of single-character
    /// insertions, deletions or substitutions needed to turn <paramref name="a"/> into
    /// <paramref name="b"/>. Used only to suggest a font name after the user has already mistyped one,
    /// so clarity is worth more here than the usual two-row space optimisation.
    /// </summary>
    private static int Levenshtein(string a, string b)
    {
        // With one string empty the answer is just the length of the other: every character
        // is an insertion (or a deletion). Handled up front so the matrix is never degenerate.
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        // d[i, j] is the distance between the first i characters of a and the first j of b.
        // A rectangular array rather than a jagged one: the shape is fixed, so this is a single
        // contiguous allocation instead of one per row.
        var d = new int[a.Length + 1, b.Length + 1];

        // Seed the edges. Turning the first i characters of a into an empty string costs i
        // deletions, and the mirror case costs j insertions. Every other cell is derived from these.
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        // Fill the rest row by row. Each cell depends only on its left, upper and upper-left
        // neighbours, all of which are already final by the time we reach it.
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                // Matching characters are free; differing ones cost one substitution.
                // The indices are off by one because row/column 0 represent the empty prefix.
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, // drop a[i-1]
                        d[i, j - 1] + 1), // insert b[j-1]
                    d[i - 1, j - 1] + cost); // substitute one for the other
            }
        }

        // The bottom-right cell is the distance between the two complete strings.
        return d[a.Length, b.Length];
    }
}