using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Banner;

/// <summary>
/// Holds the banner rendered at build time and prints it.
/// <para>
/// The generated <c>[ModuleInitializer]</c> calls <see cref="Register"/> before <c>Main</c> runs, so the
/// banner is available from the moment the assembly loads. Print it with <c>builder.AddBanner()</c>, or
/// set <c>&lt;BannerAutoPrint&gt;true&lt;/BannerAutoPrint&gt;</c> to have the generator print it for you.
/// </para>
/// </summary>
public static class BannerRuntime
{
    private static int _printed = 0;

    /// <summary>The banner with no ANSI escape codes. <c>null</c> until <see cref="Register"/> is called.</summary>
    internal static string? Plain { get; private set; }

    /// <summary>The banner with ANSI colour codes. <c>null</c> until <see cref="Register"/> is called.</summary>
    internal static string? Colored { get; private set; }

    /// <summary>Whether to append the <c>Powered by .NET</c> tagline. Set by <c>&lt;BannerPoweredBy&gt;</c>.</summary>
    internal static bool PoweredBy { get; private set; }

    /// <summary>
    /// Supplies the banner rendered at build time.
    /// <para>
    /// Called by the generated module initializer, which lives in the consuming assembly — hence public,
    /// and hidden from IntelliSense. There is no reason to call it yourself.
    /// </para>
    /// </summary>
    /// <param name="plain">The banner without escape codes.</param>
    /// <param name="colored">The banner with ANSI colour codes.</param>
    /// <param name="poweredBy">Whether to append the <c>Powered by .NET</c> tagline.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(string plain, string colored, bool poweredBy)
    {
        Plain = plain;
        Colored = colored;
        PoweredBy = poweredBy;
    }

    /// <summary>
    /// Whether the console can render ANSI colour: <c>NO_COLOR</c> unset, output not redirected, and
    /// <c>TERM</c> not <c>dumb</c>.
    /// </summary>
    private static bool SupportsColor()
    {
        // The NO_COLOR convention (no-color.org): any non-empty value disables colour.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            return false;
        }

        // Piped or redirected to a file — the closest analogue to Java's System.console() == null.
        if (Console.IsOutputRedirected)
        {
            return false;
        }

        return !string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes the banner to the console. Does nothing on a second call, so the module-initializer and
    /// <c>AddBanner()</c> routes cannot print it twice.
    /// </summary>
    public static void PrintBanner()
    {
        if (Interlocked.Exchange(ref _printed, 1) == 0)
        {
            Console.Write(Banner());
        }
    }

    /// <summary>
    /// The banner as it would be printed: the coloured or plain variant according to the console, plus the
    /// <c>Powered by .NET</c> tagline when enabled. Empty if no banner has been registered.
    /// </summary>
    public static string Banner()
        => Banner(useColor: SupportsColor(), poweredBy: PoweredBy);

    /// <summary>
    /// The banner with the variant and tagline chosen explicitly, rather than detected from the console.
    /// <para>
    /// Prefer the parameterless <see cref="Banner()"/> unless you have a reason to override the detection — for
    /// example forcing colour when writing somewhere you know renders it, or dropping the tagline for one call.
    /// </para>
    /// </summary>
    /// <param name="useColor">Whether to return the ANSI-coloured variant rather than the plain one.</param>
    /// <param name="poweredBy">Whether to append the <c>Powered by .NET</c> tagline.</param>
    /// <returns>The banner, or an empty string if none has been registered.</returns>
    public static string Banner(bool useColor, bool poweredBy)
    {
        if (Plain is null || Colored is null)
        {
            return "";
        }

        var banner = useColor ? Colored : Plain;
        string tagline = "";
        if (poweredBy)
        {
            // Use the plain version. It has the same length as the coloured version without the escape codes
            var width = Plain.Split('\n').Max(line => line.Length);

            var powerByline = $"Powered by {RuntimeInformation.FrameworkDescription}";
            var padding = Math.Max(0, width - powerByline.Length);
            tagline = new string(' ', padding) + powerByline + "\n\n";
        }


        return banner + tagline;
    }
}