using Microsoft.CodeAnalysis;

namespace Banner.Generator;

/// <summary>
/// Every diagnostic this generator can report.
/// <para>
/// The IDs are public API — users reference them in <c>.editorconfig</c> to suppress a rule or
/// change its severity — so they must stay unique and stable. Keeping them in one file is what
/// makes "which numbers are taken" answerable at a glance.
/// </para>
/// </summary>
internal static class Diagnostics
{
    // RS1032 wants a terminating period, but this message ends with a placeholder whose
    // content already carries its own punctuation.
#pragma warning disable RS1032
    internal static readonly DiagnosticDescriptor UnknownFont = new(
        id: "BAN001",
        title: "Unknown banner font",
        messageFormat: "Unknown banner font '{0}'. {1}",
        category: "Banner",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
#pragma warning restore RS1032

    internal static readonly DiagnosticDescriptor UnknownFontColor = new(
        id: "BAN002",
        title: "Unknown banner font color",
        messageFormat: "Unknown banner font color '{0}'",
        category: "Banner",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UnknownAlignment = new(
        id: "BAN003",
        title: "Unknown banner alignment",
        messageFormat: "Unknown banner alignment '{0}'",
        category: "Banner",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Reports a font name that is not one of the fonts embedded in this assembly.</summary>
    internal static Diagnostic UnknownFontDiagnostic(string name)
        => Diagnostic.Create(UnknownFont, Location.None, name, FontCatalog.Hint(name));

    /// <summary>Reports a colour that is not a name, a hex colour, or <c>default</c>.</summary>
    internal static Diagnostic UnknownFontColorDiagnostic(string fontColor)
        => Diagnostic.Create(UnknownFontColor, Location.None, fontColor);

    internal static Diagnostic UnknownAlignmentDiagnostic(string alignment)
        => Diagnostic.Create(UnknownAlignment, Location.None, alignment);
}