using System.Diagnostics;
using System.Text;

namespace Banner.Generator;

public class Figlet
{
    // Horizontal layout / smushing rule bits, as defined by the FIGfont standard.
    private const int SmEqual = 1;
    private const int SmLowLine = 2;
    private const int SmHierarchy = 4;
    private const int SmPair = 8;
    private const int SmBigX = 16;
    private const int SmHardBlank = 32;
    private const int SmKern = 64;
    private const int SmSmush = 128;

    /**
     * Character codes stored, in order, immediately after the 95 required ASCII characters (32..126).
     */
    private static readonly int[] ExtraCodes = { 196, 214, 220, 228, 246, 252, 223 };

    private readonly char _hardBlank;
    private readonly int _height;
    private readonly int _smushMode;
    private readonly Dictionary<int, string[]> _glyphs;

    internal Dictionary<int, string[]> Glyphs => _glyphs;

    public int SmushMode => _smushMode;
    internal char HardBlank => _hardBlank;
    internal int Height => _height;
    internal int GlyphCount => _glyphs.Count;
    
    private Figlet(char hardBlank, int height, int smushMode, Dictionary<int, string[]> glyphs)
    {
        _hardBlank = hardBlank;
        _height = height;
        _smushMode = smushMode;
        _glyphs = glyphs;
    }
    
    /// <summary>A rendered banner together with per-cell ink ownership.</summary>
    internal sealed record RenderResult(string Banner, int[][] Owner);

    /// <summary>Parses a <c>.flf</c> font stream and renders <paramref name="text"/> as a single line.</summary>
    internal static string ConvertOneLine(Stream fontStream, string text)
    {
        return Parse(fontStream).Render(text);
    }

    /// <summary>Renders <paramref name="text"/> and reports which input character owns each output cell.</summary>
    internal static RenderResult RenderTracked(Stream fontStream, string text)
    {
        return Parse(fontStream).RenderTracked(text);
    }

    internal static Figlet Parse(Stream fontStream)
    {
        StreamReader reader = new StreamReader(fontStream, Encoding.GetEncoding(28591));
        
        string? header = reader.ReadLine();
        if (header == null || !header.StartsWith("flf2a") || header.Length < 6) {
            throw new IOException("Not a FIGlet .flf font (bad signature)");
        }
        char hardBlank = header[5];
        string[] headerParams = header.Substring(6).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (headerParams.Length < 5) {
            throw new IOException("Malformed .flf header: " + header);
        }
        int height = int.Parse(headerParams[0]);
        int oldLayout = int.Parse(headerParams[3]);
        int commentLines = int.Parse(headerParams[4]);
        // Full_Layout (index 6) is optional; when present it takes precedence over Old_Layout.
        int? fullLayout = headerParams.Length > 6 ? int.Parse(headerParams[6]) : null;
        int smushMode = EffectiveSmushMode(oldLayout, fullLayout);

        for (int i = 0; i < commentLines; i++) {
            if (reader.ReadLine() == null) {
                throw new IOException("Truncated .flf header comments");
            }
        }

        Dictionary<int, string[]> glyphs = new Dictionary<int, string[]>();
        // Required characters: ASCII 32..126, then the seven additional Deutsch characters.
        for (int code = 32; code <= 126; code++) {
            String[] glyph = ReadGlyph(reader, height);
            if (glyph.Length == 0) {
                throw new IOException("Truncated .flf: missing glyph for code " + code);
            }
            glyphs.Add(code, glyph);
        }
        foreach (var code in ExtraCodes) {
            string[] glyph = ReadGlyph(reader, height);
            if (glyph.Length == 0) {
                break; // some fonts omit the Deutsch characters; that is tolerated
            }
            glyphs.Add(code, glyph);
        }
        return new Figlet(hardBlank, height, smushMode, glyphs);
    }

    /// <summary>Derives the effective smush mode from Old_Layout and the optional Full_Layout.</summary>
    private static int EffectiveSmushMode(int oldLayout, int? fullLayout)
    {
        if (fullLayout.HasValue) {
            return fullLayout.Value;
        }

        return oldLayout switch
        {
            < 0 => 0,
            0 => SmKern,
            _ => (oldLayout & 63) | SmSmush
        };
    }

    /// <summary>Reads one glyph (<paramref name="height"/> sub-lines), stripping end-marks.</summary>
    internal static string[] ReadGlyph(TextReader reader, int height)
    {
        var rows = new string[height];
        var width = 0;
        
        for (var i = 0; i < height; i++) {
            var line = reader.ReadLine();
            if (line == null) {
                return []; // no more glyph data in the stream
            }
            
            // Drop any trailing carriage return, then strip all trailing end-mark characters. The end-mark
            // is whatever the final character of the sub-line is (typically '@'); the last sub-line carries
            // two of them.
            if (line.EndsWith("\r")) {
                line = line.Substring(0, line.Length - 1);
            }
            if (line.Length != 0) {
                char endMark = line[line.Length - 1];
                int end = line.Length;
                while (end > 0 && line[end - 1] == endMark) {
                    end--;
                }
                line = line.Substring(0, end);
            }
            rows[i] = line;
            width = Math.Max(width, line.Length);
        }
        
        // Pad every sub-line to the glyph's width so all rows are rectangular.
        for (var i = 0; i < height; i++) {
            if (rows[i].Length < width) {
                rows[i] += new string(' ', width-rows[i].Length);
            }
        }
        return rows;
    }


    internal string Render(string text)
    {
        return RenderTracked(text).Banner;
    }

    internal RenderResult RenderTracked(string text)
    {
         StringBuilder[] outer = new StringBuilder[Height];
         
        // owner[r][c] is the index of the input character whose visible ink occupies cell (r, c), or -1. It
        // lets the banner be coloured per cell, which is what slanted/overlapping fonts need: a colour change
        // follows each character's actual ink rather than a single vertical boundary.
        int[][] owner = new int[Height][];
        for (var i = 0; i < Height; i++)
        {
            outer[i] = new StringBuilder();
            owner[i]  = [];
        }
        
        int outLen = 0;
        int prevWidth = 0; // width of the previously placed glyph, for the narrow-character smush guard

        for (int i = 0; i < text.Length; i++)
        {
            if (Glyphs.TryGetValue(text[i], out var glyph)) 
            {
                int charWidth = glyph[0].Length;
                if (charWidth == 0)
                {
                    continue; // empty glyph contributes nothing
                }

                int smush = SmushAmount(outer, outLen, glyph, charWidth, prevWidth);
                // The glyph is overlaid onto the accumulated output starting at this column. It is negative for
                // the first glyph, which slides it against the left margin and clips its leading blank columns.
                int offset = outLen - smush;
                int newLen = Math.Max(outLen, offset + charWidth);

                for (int r = 0; r < Height; r++)
                {
                    StringBuilder row = outer[r];
                    while (row.Length < newLen)
                    {
                        row.Append(' ');
                    }

                    if (owner[r].Length < newLen)
                    {
                        int from = owner[r].Length;

                        Array.Resize(ref owner[r], newLen);

                        for (var c = from; c < newLen; c++)
                        {
                            owner[r][c] = -1;
                        }
                    }

                    string gr = glyph[r];
                    for (int k = 0; k < charWidth; k++)
                    {
                        int x = offset + k;
                        if (x < 0)
                        {
                            continue; // clipped off the left edge
                        }

                        char glyphChar = gr[k];
                        char merged = Merge(row[x], glyphChar);
                        row[x] = merged != 0 ? merged : row[x];

                        // This character owns the cell where its glyph contributes visible ink; a later,
                        // overlapping character takes ownership of any cell it smushes into.
                        if (glyphChar != ' ' && glyphChar != HardBlank)
                        {
                            owner[r][x] = i;
                        }
                    }
                }

                outLen = newLen;
                prevWidth = charWidth;
            }
        }

        StringBuilder result = new StringBuilder();
        for (int r = 0; r < Height; r++) {
            String row = outer[r].ToString();
            if (HardBlank != ' ') {
                row = row.Replace(_hardBlank, ' ');
            }
            result.Append(row).Append('\n');
        }
        return new RenderResult(result.ToString(), owner);
    }

    /// <summary>How many columns the incoming glyph may overlap the accumulated output.</summary>
    internal int SmushAmount(StringBuilder[] output, int outLen, string[] glyph, int charWidth, int prevWidth)
    {
        if ((SmushMode & (SmSmush | SmKern)) == 0) {
            return 0; // full width
        }
        // Smushing (overlapping two visible characters into one column) is only attempted when both the
        // previous and current characters are at least two columns wide; otherwise only fitting applies.
        bool allowSmush = prevWidth >= 2 && charWidth >= 2;
        int maxSmush = charWidth;
        for (int r = 0; r < Height; r++) {
            StringBuilder row = output[r];
            int trailing = 0; // blank columns at the right of the accumulated row
            int idx = outLen - 1;
            while (idx >= 0 && row[idx] == ' ') {
                trailing++;
                idx--;
            }
            char left = (char)(idx >= 0 ? row[idx] : 0);

            String gr = glyph[r];
            int leading = 0; // blank columns at the left of the incoming glyph row
            while (leading < charWidth && gr[leading] == ' ') {
                leading++;
            }
            char right = (char)(leading < charWidth ?  gr[leading] : 0);

            int amt = trailing + leading;
            // If both touching characters are visible and can be smushed into one, one more column overlaps.
            if (allowSmush && left != 0 && right != 0 && Merge(left, right) != 0) {
                amt++;
            }
            if (amt < maxSmush) {
                maxSmush = amt;
            }
        }
        return Math.Max(0, maxSmush);
    }

    /// <summary>Merges two overlapping sub-characters, or returns <c>'\0'</c> if they cannot smush.</summary>
    internal char Merge(char left, char right)
    {
        if (left == ' ') {
			return right;
		}
		if (right == ' ') {
			return left;
		}
		if ((SmushMode & SmSmush) == 0) {
			return (char)0x0; // kerning only: visible characters must not overlap
		}
		if ((SmushMode & 63) == 0) {
			// Universal smushing: a hardblank yields to a visible character, otherwise the later
			// (right-hand) character wins.
			if (left == HardBlank) {
				return right;
			}
			if (right == HardBlank) {
				return left;
			}
			return right;
		}
		if (left == HardBlank || right == HardBlank) {
			// Hardblanks are asymmetric under controlled smushing: a hardblank already in the output (on
			// the left) yields to the incoming character, but a hardblank at the start of the incoming
			// glyph (on the right) is protected and blocks smushing.
			if ((SmushMode & SmHardBlank) != 0 && left == HardBlank && right == HardBlank) {
				return HardBlank; // rule 6: two hardblanks
			}
			if ((SmushMode & SmHardBlank) != 0 && left == HardBlank) {
				return right;
			}
			return (char)0x0;
		}
		if ((SmushMode & SmEqual) != 0 && left == right) {
			return left;
		}
		if ((SmushMode & SmLowLine) != 0) {
            const string replacers = @"|/\[]{}()<>";
			if (left == '_' && replacers.IndexOf(right) >= 0) {
				return right;
			}
			if (right == '_' && replacers.IndexOf(left) >= 0) {
				return left;
			}
		}
		if ((SmushMode & SmHierarchy) != 0) {
			int l = HierarchyClass(left);
			int r = HierarchyClass(right);
			if (l > 0 && r > 0 && l != r) {
				return l > r ? left : right;
			}
		}
		if ((SmushMode & SmPair) != 0) {
			if ((left == '[' && right == ']') || (left == ']' && right == '[')) {
				return '|';
			}
			if ((left == '{' && right == '}') || (left == '}' && right == '{')) {
				return '|';
			}
			if ((left == '(' && right == ')') || (left == ')' && right == '(')) {
				return '|';
			}
		}
		if ((SmushMode & SmBigX) != 0) {
			if (left == '/' && right == '\\') {
				return '|';
			}
			if (left == '\\' && right == '/') {
				return 'Y';
			}
			if (left == '>' && right == '<') {
				return 'X';
			}
		}
		return (char)0x0;
    }

    /// <summary>The FIGfont hierarchy class (1..6) of a smushing character, or 0.</summary>
    private static int HierarchyClass(char c) => c switch
    {
        '|' => 1,
        '/' or '\\' => 2,
        '[' or ']' => 3,
        '{' or '}' => 4,
        '(' or ')' => 5,
        '<' or '>' => 6,
        _ => 0
    };
}