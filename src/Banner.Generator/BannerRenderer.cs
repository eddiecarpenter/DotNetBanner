using System.Text;
using System.Text.RegularExpressions;

namespace Banner.Generator;

/// <summary>
/// Renders text into a FIGlet ASCII-art banner using <see cref="Figlet"/> and one of the bundled fonts.
/// <para>
/// The text may contain inline colour markers of the form <c>{colour}</c> (for example
/// <c>{red}Dot{cyan}net</c>). A marker is a colour name, <c>orange</c>, a <c>#rrggbb</c> hex colour,
/// or <c>default</c>; an unrecognised <c>{token}</c> is left in the text verbatim. A global foreground
/// applies to any text not covered by an inline marker.
/// </para>
/// <para>
/// Every render produces both a colour and a plain version, so the runtime can pick whichever suits
/// the console and no ANSI codes ever leak into a log file.
/// </para>
/// </summary>
internal static class BannerRenderer
{
    private const string Esc = "\u001b";
    private const string Reset = Esc + "[0m";

    /// <summary>An inline colour marker: <c>{name}</c>, where the name looks like a colour.</summary>
    private static readonly Regex MarkerPattern = new(@"\{([A-Za-z0-9#-]+)\}");

    /// <summary>A line break in the banner text: an actual newline, or a literal backslash-n.</summary>
    private static readonly Regex LineBreakPattern = new(@"\r\n|\r|\n|\\n");

    /// <summary>A rendered banner in both its colour and plain forms.</summary>
    internal sealed record Rendered(string Plain, string Colored);

    /// <summary>One rendered line: its plain rows, its cell-coloured rows, and its width.</summary>
    private sealed record LineBlock(List<string> PlainRows, List<string> ColoredRows, int Width);

    /// <summary>The marker-free text and the colour transitions over its character indices.</summary>
    internal sealed record Markup(string CleanText, List<Transition> Transitions);

    /// <summary>A colour change starting at a clean-text character index.</summary>
    internal sealed record Transition(int Index, ResolvedColor Color);

    /// <summary>
    /// Renders <paramref name="text"/> — which may contain inline <c>{colour}</c> markers and
    /// <c>\n</c> line breaks — in both its plain and coloured forms.
    /// </summary>
    internal static Rendered RenderBanner(
        Stream fontStream,
        string text,
        ResolvedColor fontColor,
        Alignment alignment,
        int lineSpacing)
    {
        List<LineBlock> blocks = new List<LineBlock>();
        int maxWidth = 0;
        var figlet = Figlet.Parse(fontStream);
        foreach (string line in SplitLines(text))
        {
            LineBlock block = RenderLineBlock(figlet, line, fontColor);
            blocks.Add(block);
            maxWidth = Math.Max(maxWidth, block.Width);
        }

        int spacing = Math.Max(0, lineSpacing);
        String plain = Assemble(blocks, maxWidth, alignment, spacing, false);
        String colored = Assemble(blocks, maxWidth, alignment, spacing, true);
        return new Rendered(plain, colored);
    }

    /// <summary>Splits the banner text into lines on an actual newline or a literal backslash-n.</summary>
    private static List<string> SplitLines(string text)
    {
        List<string> lines = [.. LineBreakPattern.Split(text)];

        while (lines.Count > 1 && lines[lines.Count - 1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>Renders one line, keeping both its plain rows and its per-cell coloured rows.</summary>
    private static LineBlock RenderLineBlock(Figlet figlet, string line, ResolvedColor fontColor)
    {
        Markup markup = ParseMarkup(line, fontColor);
        Figlet.RenderResult rendered = figlet.RenderTracked(markup.CleanText);
        List<String> rows = SplitRows(rendered.Banner);
        int width = rows.Count == 0 ? 0 : rows[0].Length;
        ResolvedColor[] colorForChar = ColorForChar(markup, markup.CleanText.Length);
        int[][] owner = rendered.Owner;
        List<string> coloredRows = new List<string>(rows.Count);
        for (int r = 0; r < rows.Count; r++)
        {
            coloredRows.Add(PaintRow(rows[r], r < owner.Length ? owner[r] : [], colorForChar));
        }

        return new LineBlock(rows, coloredRows, width);
    }

    /// <summary>The colour that applies to each clean-text character, from the parsed transitions.</summary>
    internal static ResolvedColor[] ColorForChar(Markup markup, int length)
    {
        ResolvedColor[] colors = new ResolvedColor[length];
        List<Transition> transitions = markup.Transitions;
        int t = 0;
        for (int c = 0; c < length; c++)
        {
            while (t + 1 < transitions.Count && transitions[t + 1].Index <= c)
            {
                t++;
            }

            colors[c] = transitions[t].Color;
        }

        return colors;
    }

    /// <summary>
    /// Assembles the final banner: each block padded to <paramref name="maxWidth"/> per the alignment,
    /// stacked with <paramref name="lineSpacing"/> blank rows between lines.
    /// </summary>
    private static string Assemble(
        List<LineBlock> blocks,
        int maxWidth,
        Alignment alignment,
        int lineSpacing,
        bool colored)
    {
        StringBuilder banner = new StringBuilder();
        for (int b = 0; b < blocks.Count; b++)
        {
            LineBlock block = blocks[b];
            int left = LeadingPad(alignment, maxWidth, block.Width);
            int right = maxWidth - block.Width - left;
            List<string> rows = colored ? block.ColoredRows : block.PlainRows;
            foreach (string row in rows)
            {
                banner.Append(Pad(left)).Append(row).Append(Pad(right))
                    .Append('\n');
            }

            if (b < blocks.Count - 1)
            {
                for (int s = 0; s < lineSpacing; s++)
                {
                    banner.Append(Pad(maxWidth)).Append('\n');
                }
            }
        }

        return banner.ToString();
    }

    /// <summary>
    /// <paramref name="n" /> padding spaces. Guards the non-positive case, which the string
    /// constructor would otherwise throw on.
    /// </summary>
    private static string Pad(int n) => n <= 0 ? string.Empty : new string(' ', n);

    /// <summary>Leading padding for a block of <paramref name="width"/> within the widest line.</summary>
    private static int LeadingPad(Alignment alignment, int maxWidth, int width)
    {
        int slack = Math.Max(0, maxWidth - width);

        return alignment switch
        {
            Alignment.Left => 0,
            Alignment.Right => slack,
            Alignment.Center => slack / 2,
            _ => 0
        };
    }

    /// <summary>Paints one row: runs of cells sharing a colour are wrapped together.</summary>
    internal static string PaintRow(string row, int[] ownerRow, ResolvedColor[] colorForChar)
    {
        StringBuilder painted = new StringBuilder();
        int i = 0;
        int n = row.Length;
        while (i < n)
        {
            ResolvedColor color = ColorAt(ownerRow, i, colorForChar);
            int j = i + 1;
            while (j < n && ColorAt(ownerRow, j, colorForChar).Equals(color))
            {
                j++;
            }

            painted.Append(Colorize(row.Substring(i, j - i), color));
            i = j;
        }

        return painted.ToString();
    }

    /// <summary>The colour of a single cell: its ink owner's colour, or the default when blank.</summary>
    private static ResolvedColor ColorAt(int[] ownerRow, int column, ResolvedColor[] colorForChar)
    {
        int owner = column < ownerRow.Length ? ownerRow[column] : -1;
        return owner >= 0 && owner < colorForChar.Length ? colorForChar[owner] : ResolvedColor.Default;
    }

    /// <summary>Splits a rendered block into rows, dropping the empty tail left by the final newline.</summary>
    private static List<string> SplitRows(string block)
    {
        string[] parts = block.Split('\n');
        var rows = new List<string>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            // Every row is terminated with '\n', so the final split leaves an empty tail. Drop it,
            // but keep any other empty row — a blank line inside the banner is real.
            if (i == parts.Length - 1 && parts[i].Length == 0)
            {
                break;
            }

            rows.Add(parts[i]);
        }

        return rows;
    }

    /// <summary>Wraps text in the ANSI sequence for <paramref name="color"/>.</summary>
    internal static string Colorize(string text, ResolvedColor color)
    {
        String prefix = Sgr(color);
        return prefix.Length == 0 ? text : prefix + text + Reset;
    }

    /// <summary>The ANSI SGR prefix for a foreground, or <c>""</c> when it is the terminal default.</summary>
    private static string Sgr(ResolvedColor color)
    {
        if (color.IsDefault)
        {
            return "";
        }

        StringBuilder sgr = new StringBuilder(Esc).Append('[');
        sgr.Append(color.Sgr);

        return sgr.Append('m').ToString();
    }

    /// <summary>Splits text into its marker-free form and the colour transitions over its indices.</summary>
    internal static Markup ParseMarkup(string text, ResolvedColor defaultColor)
    {
        StringBuilder clean = new StringBuilder();
        List<Transition> transitions = [new Transition(0, defaultColor)];

        int last = 0;
        foreach (Match match in MarkerPattern.Matches(text))
        {
            ResolvedColor? color = ResolvedColor.Parse(match.Groups[1].Value);
            if (color == null)
            {
                continue; // not a colour: leave the "{token}" in the text verbatim
            }

            clean.Append(text, last, match.Index - last);
            transitions.Add(new Transition(clean.Length, color));
            last = match.Index + match.Length;
        }

        clean.Append(text, last, text.Length - last);
        return new Markup(clean.ToString(), transitions);
    }
}