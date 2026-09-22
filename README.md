# DotNetBanner

[![NuGet](https://img.shields.io/nuget/v/DotNetBanner?logo=nuget&label=DotNetBanner&style=flat-square)](https://www.nuget.org/packages/DotNetBanner)
[![NuGet](https://img.shields.io/nuget/v/DotNetBanner.Tool?logo=nuget&label=dotnet-banner&style=flat-square)](https://www.nuget.org/packages/DotNetBanner.Tool)
[![Build](https://github.com/eddiecarpenter/DotNetBanner/actions/workflows/build.yml/badge.svg)](https://github.com/eddiecarpenter/DotNetBanner/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg?style=flat-square)](https://www.apache.org/licenses/LICENSE-2.0)

Generate a **colourful** [FIGlet](https://en.wikipedia.org/wiki/FIGlet) ASCII-art startup banner for your .NET
application — rendered at **build time** from a piece of text and a font, painted in the ANSI colours you choose, and
printed when your application starts.

A .NET port of the [Quarkus Banner extension](https://github.com/quarkiverse/quarkus-banner).

<p align="center">
  <img src="https://raw.githubusercontent.com/eddiecarpenter/DotNetBanner/main/docs/example.svg" alt="A startup banner reading DotNetBanner in cyan, white and magenta, with a Powered by .NET tagline" width="620">
</p>

```xml
<BannerText>{bright-cyan}Dot{bright-white}Net{bright-magenta}Banner</BannerText>
<BannerFont>slant</BannerFont>
```

### Highlights

- 🎨 **Colour, including multi-colour banners.** Named ANSI colours, `orange`, or any `#rrggbb` hex — colour parts of
  the text inline with `{colour}` markers (`{red}My {bright-cyan}Service`), painted per character so kerning is preserved.
- 🧱 **Multi-line banners** — split the text with `\n` and align each line `left`, `center` or `right`.
- 🖥️ **Console-aware.** Colour is only emitted when the terminal supports it; redirected output and `NO_COLOR` get a
  clean, plain banner.
- 🔤 **246 bundled fonts**, selectable by name and validated at build time.
- 📦 **Nothing ships at runtime.** The fonts and the renderer live in the analyzer, not in your application.

## How it works

At build time a [Roslyn incremental source generator](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview)
renders your text into ASCII art and emits it into your assembly as a `const string`. This means:

- your application contains a string constant — there is **no FIGlet code, no font files and no parsing at runtime**;
- changing the text or font requires a rebuild;
- a typo in a font name or colour is a **compile error**, not a surprise at start-up.

The 3 MB of fonts live in the analyzer assembly, which the compiler loads and your application never references.

## Installation

```bash
dotnet add package DotNetBanner
```

Or in your `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="DotNetBanner" Version="1.0.0-preview.1" />
</ItemGroup>
```

## Configuration lives in your `.csproj`

**This is the thing to know up front.** Because the banner is rendered during compilation, its settings are **MSBuild
properties**, not `appsettings.json`. The compiler never reads `appsettings.json`, so there is nothing for the generator
to consult at the point it runs.

```xml
<PropertyGroup>
    <BannerText>{red}My {bright-cyan}Service</BannerText>
    <BannerFont>doom</BannerFont>
    <BannerAlignment>center</BannerAlignment>
</PropertyGroup>
```

That is all you need. The package declares the corresponding `CompilerVisibleProperty` items for you,
through a `buildTransitive` props file that NuGet imports automatically.

<details>
<summary>Referencing the projects directly instead of the package?</summary>

MSBuild properties are invisible to a source generator unless declared with `CompilerVisibleProperty`,
and that declaration arrives with the NuGet package. If you are using `ProjectReference`, import the
same file the package ships:

```xml
<Import Project="..\Banner\buildTransitive\DotNetBanner.props" />
```

Without it the build still succeeds and every setting is silently ignored in favour of its default.
`obj/Debug/<tfm>/<Project>.GeneratedMSBuildEditorConfig.editorconfig` lists exactly what the compiler
was handed, which is the quickest way to check.

</details>

## Printing the banner

Two routes. Pick one.

**Explicit — the .NET way.** One line, and the banner prints when the host starts, above your application's own logs:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddBanner();

var app = builder.Build();
```

`AddBanner()` extends `IHostApplicationBuilder`, so it works for both `WebApplication` and `Host` builders. It registers
a hosted service, and touches nothing else — your logging configuration is left alone.

**Automatic — no host required.** Set `<BannerAutoPrint>true</BannerAutoPrint>` and the generator emits a
[`[ModuleInitializer]`](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/general#moduleinitializer-attribute)
that prints the banner **before `Main` runs**, with no code at all:

```xml
<BannerAutoPrint>true</BannerAutoPrint>
```

Useful for console tools that have no host. Be aware it fires whenever the assembly is loaded — including for
`--help`, `--version`, and test runs that reference your app.

Printing is idempotent, so enabling both is harmless.

## Configuration

All values are fixed at build time.

| Property             | Type     | Default    | Description                                                                             |
|----------------------|----------|------------|-----------------------------------------------------------------------------------------|
| `BannerText`         | `string` | `Banner`   | The text to render. Supports inline `{colour}` markers and `\n` line breaks.             |
| `BannerFont`         | `string` | `standard` | One of the 246 bundled fonts. Matched case-insensitively; an unknown font fails the build. |
| `BannerColor`        | `string` | `default`  | Foreground colour for text not covered by a marker. An unknown value fails the build.    |
| `BannerAlignment`    | `string` | `left`     | `left`, `center` or `right`. An unknown value fails the build.                            |
| `BannerLineSpacing`  | `int`    | `1`        | Blank rows between the lines of a multi-line banner.                                     |
| `BannerPoweredBy`          | `bool`   | `true`     | Append a right-aligned `Powered by .NET <version>` tagline.                              |
| `BannerAutoPrint`          | `bool`   | `false`    | Emit a module initializer that prints the banner before `Main`.                          |

## Colour

A colour can be set in two places, and both accept exactly the same values:

```xml
<!-- the whole banner -->
<BannerColor>bright-cyan</BannerColor>
```

```xml
<!-- or per section, with inline markers -->
<BannerText>{red}My {bright-cyan}Service</BannerText>
```

Markers are painted **per character**, so FIGlet's kerning is preserved and slanted fonts still tuck their letters
together across a colour change.

### Any RGB colour

You are not limited to a fixed palette. Any `#rrggbb` value works, in either place:

```xml
<BannerColor>#33ccff</BannerColor>
```

```xml
<BannerText>{#ff8800}Dot{#33ccff}Net</BannerText>
```

- Three-digit shorthand expands the usual way — `#f80` is `#ff8800`.
- Hex colours are emitted as **24-bit truecolor** escape sequences, so they need a truecolor-capable terminal.
  Most modern ones qualify; a bare TTY may not.

### Named colours

Sixteen standard ANSI colours, matched case-insensitively:

| Standard  | Bright           |
|-----------|------------------|
| `black`   | `bright-black`   |
| `red`     | `bright-red`     |
| `green`   | `bright-green`   |
| `yellow`  | `bright-yellow`  |
| `blue`    | `bright-blue`    |
| `magenta` | `bright-magenta` |
| `cyan`    | `bright-cyan`    |
| `white`   | `bright-white`   |

Plus `orange`, which the 16-colour palette has no slot for — it is emitted as truecolor, like a hex value.

These names are a convenience for the colours a terminal renders most reliably. For anything else, use hex.

### `default`

`default` leaves the terminal's own foreground colour untouched, emitting no escape codes at all. It is the default
value of `BannerColor`, and `{default}` returns to it mid-text:

```xml
<BannerText>{red}Warning{default} — see the log</BannerText>
```

A `{token}` that isn't a recognised colour is **left in the text verbatim** and rendered as ASCII art, rather than
silently disappearing.

### When colour is emitted

Both a coloured and a plain banner are produced at build time, and the runtime picks between them. Colour is used only
when all of these hold:

- [`NO_COLOR`](https://no-color.org) is unset or empty
- output is not redirected — `Console.IsOutputRedirected` is `false`
- `TERM` is not `dumb`

So piping to a file, or into `grep`, or running under a CI log collector gives you the plain banner. Escape codes never
reach a log file.

> **Windows:** ANSI escape sequences require virtual terminal processing to be enabled on the console handle. Windows
> Terminal generally has it on; the legacy console host does not, and this package does not enable it for you.

## Multiple lines

Split the text with `\n` — a literal backslash-n, which is what an MSBuild property delivers. Each line is rendered as
its own FIGlet block and the blocks are stacked:

```xml
<BannerText>{bright-white}DotNet\n{red}Banner</BannerText>
<BannerAlignment>center</BannerAlignment>
<BannerLineSpacing>1</BannerLineSpacing>
```

`BannerAlignment` positions each line within the width of the widest line. `BannerLineSpacing` keeps a descender like
`g` or `j` on one line from touching the line below.

## Fonts

`BannerFont` must be one of the **246 FIGlet fonts** bundled with this package — for example `standard`, `slant`,
`doom`, `big`, `colossal`, `banner3-D` or `3d_diagonal`. Arbitrary file paths and URLs are intentionally not supported,
and typos are caught at build time with a suggestion:

```
error BAN001: Unknown banner font 'dooom'. Did you mean 'doom'?
```

Run `dotnet banner list-fonts` to see them all (see [the tool](#the-dotnet-banner-tool)), or
browse the list with authors in [FIGLET-FONTS.md](FIGLET-FONTS.md).

> **Licensing note:** the bundled fonts originate from the FIGlet font collection and are authored by many individuals
> under varied terms. Each font's original header (including author credit) is preserved in the `.flf` file; they are
> redistributed on that basis and are **not** relicensed under this project's Apache-2.0 licence.

## Build-time errors

| ID       | Raised when                                                    |
|----------|----------------------------------------------------------------|
| `BAN001` | `BannerFont` is not a bundled font (suggests the nearest match) |
| `BAN002` | `BannerColor` is not a name, hex colour, or `default`           |
| `BAN003` | `BannerAlignment` is not `left`, `center` or `right`            |

## The `dotnet banner` tool

A companion .NET tool for choosing a font and colour without rebuilding your application.

```bash
dotnet tool install -g DotNetBanner.Tool
```

```bash
dotnet banner list-fonts     # the 246 bundled fonts, one per line
dotnet banner list-colors    # the named colours, each printed in its own colour
dotnet banner preview        # render a banner to the console
```

`preview` accepts the same values as the MSBuild properties:

```bash
dotnet banner preview --font doom --text "{red}My {bright-cyan}Service"
dotnet banner preview --font slant --text "Dot\nNet" --alignment center --spacing 2
```

| Option        | Default    |
|---------------|------------|
| `--font`      | `standard` |
| `--text`      | `Banner`   |
| `--color`     | `default`  |
| `--alignment` | `left`     |
| `--spacing`   | `1`        |
| `--powered-by`| `true`     |

It renders through the same code your build does, so what you see is what your application will
print — including the plain/coloured choice and the `Powered by .NET` tagline. An unknown font gives
the same suggestion as the build error:

```
$ dotnet banner preview --font dooom
Unknown font 'dooom'. Did you mean 'doom'?
```

`dotnet banner --help` lists everything; each command has its own `--help`.

## Rendering

Banners are drawn by a small, self-contained FIGlet renderer — an implementation of the public FIGfont v2
standard. It carries **no third-party rendering dependency**, and it never reaches your application: it runs inside the
compiler and only the resulting string is emitted.

## Licence

Apache License 2.0 — see [LICENSE](LICENSE). Bundled FIGlet fonts retain their original terms; see
[FIGLET-FONTS.md](FIGLET-FONTS.md).
