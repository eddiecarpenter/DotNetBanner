using System.Runtime.InteropServices;

namespace Banner;

public static class BannerRuntime
{
    private static int _printed = 0;

    public static string? Plain { get; private set; }
    public static string? Colored { get; private set; }
    public static bool PoweredBy { get; private set; }

    public static void Register(string plain, string colored, bool poweredBy)
    {
        Plain = plain;
        Colored = colored;
        PoweredBy = poweredBy;
    }

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

    public static void PrintBanner()
    {
        if (Interlocked.Exchange(ref _printed, 1) == 0)
        {
            Console.Write(Banner());
        }
    }

    public static string Banner()
    {
        if (Plain is null || Colored is null)
        {
            return "";
        }

        // For now use the coloured version until we have a way to detect it
        var banner = SupportsColor() ? Colored : Plain;

        string tagline = "";
        if (PoweredBy)
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