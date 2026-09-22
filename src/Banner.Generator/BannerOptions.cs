namespace Banner.Generator;

/// <summary>
/// The build-time configuration the banner is generated from. A record, so Roslyn's incremental
/// cache can compare two of these by value and skip regenerating when nothing has changed.
/// </summary>
internal sealed record BannerOptions(string Text, string Font, string FontColor, string Alignment, int LineSpacing, bool PoweredBy, bool AutoPrint);