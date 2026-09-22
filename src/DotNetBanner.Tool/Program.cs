using Banner;
using Banner.Generator;

return args switch
{
    ["list-fonts"] => ListFonts(),
    ["list-colors"] or ["list-colours"] => ListColours(),
    ["preview", .. var rest] => Preview(rest),
    _ => Usage()
};

static int ListFonts()
{
    foreach (var font in FontCatalog.AvailableFonts().Order(StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine(font);
    }

    return 0;
}

static int ListColours()
{
    foreach (var (name, colour) in ResolvedColour())
    {
        // Print each name in its own colour, so the list doubles as a swatch.
        Console.WriteLine(UseColour() ? BannerRenderer.Colorize(name, colour) : name);
    }

    Console.WriteLine();
    Console.WriteLine("Any #rgb or #rrggbb hex colour also works, as does 'default'.");

    return 0;

    static IEnumerable<KeyValuePair<string, ResolvedColor>> ResolvedColour()
        => ResolvedColor.Named.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase);
}

static int Preview(string[] args)
{
    var font = "standard";
    var text = "Banner";
    var colour = "default";
    var alignment = "left";
    var spacing = 1;
    var poweredBy = true;

    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        switch (args[i])
        {
            case "--font": font = args[i + 1]; break;
            case "--text": text = args[i + 1]; break;
            case "--color" or "--colour": colour = args[i + 1]; break;
            case "--alignment": alignment = args[i + 1]; break;
            case "--spacing": spacing = int.TryParse(args[i + 1], out var n) ? n : 1; break;
            case "--powered-by": poweredBy = !bool.TryParse(args[i + 1], out var on) || on; break;
            default: return Fail($"Unknown option '{args[i]}'.");
        }
    }

    using var fontStream = FontCatalog.Open(font);
    if (fontStream is null)
    {
        return Fail($"Unknown font '{font}'. {FontCatalog.Hint(font)}");
    }

    var foreground = ResolvedColor.Parse(colour);
    if (foreground is null)
    {
        return Fail($"Unknown colour '{colour}'. Use a name, a #rrggbb hex colour, or default.");
    }

    if (!Enum.TryParse<Alignment>(alignment, ignoreCase: true, out var align)
        || !Enum.IsDefined(typeof(Alignment), align))
    {
        return Fail($"Unknown alignment '{alignment}'. Use left, center, or right.");
    }

    var rendered = BannerRenderer.RenderBanner(fontStream, text, foreground, align, spacing);

    // Compose through the runtime, so the preview is exactly what the application would print:
    // the same plain/coloured choice and the same "Powered by .NET" tagline.
    BannerRuntime.Register(rendered.Plain, rendered.Colored, poweredBy);

    Console.Write(BannerRuntime.Banner());

    return 0;
}

static int Usage()
{
    Console.WriteLine("""
        Usage: dotnet banner <command>

          list-fonts                 List the bundled FIGlet fonts.
          list-colors                List the named colours.
          preview [options]          Render a banner to the console.

        Preview options:

          --font <name>              Font to render with           (default: standard)
          --text <text>              Text, with {colour} markers    (default: Banner)
          --color <colour>           Colour for unmarked text       (default: default)
          --alignment <alignment>    left, center or right          (default: left)
          --spacing <rows>           Blank rows between lines       (default: 1)
          --powered-by <bool>        Show the "Powered by" tagline  (default: true)

        Example:

          dotnet banner preview --font doom --text "{red}My {bright-cyan}Service"
        """);

    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

// Never emit escape codes into a pipe or a file.
static bool UseColour()
    => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
       && !Console.IsOutputRedirected
       && !string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.Ordinal);
