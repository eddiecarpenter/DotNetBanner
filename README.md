# DotNetBanner

Generate a **colourful** [FIGlet](https://en.wikipedia.org/wiki/FIGlet) ASCII-art startup banner for your .NET
application — rendered at **build time** from a piece of text and a font, painted in the ANSI colours you choose, and
printed when your application starts.

A .NET port of the [Quarkus Banner extension](https://github.com/quarkiverse/quarkus-banner).

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

> Not yet published to NuGet. For now, reference the projects directly:

```xml
<ItemGroup>
    <ProjectReference Include="..\Banner\Banner.csproj" />
    <ProjectReference Include="..\Banner.Generator\Banner.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
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

<!-- Every property needs one of these, whether or not you set it above. -->
<ItemGroup>
    <CompilerVisibleProperty Include="BannerText" />
    <CompilerVisibleProperty Include="BannerFont" />
    <CompilerVisibleProperty Include="BannerColor" />
    <CompilerVisibleProperty Include="BannerAlignment" />
    <CompilerVisibleProperty Include="BannerLineSpacing" />
    <CompilerVisibleProperty Include="PoweredBy" />
    <CompilerVisibleProperty Include="AutoPrint" />
</ItemGroup>
```

The `CompilerVisibleProperty` items are what hand each property to the generator. **A property without one is invisible
to it** — the build succeeds, and your setting is silently ignored in favour of the default. Declare all seven up front
and you cannot hit that.

If a setting appears to have no effect, `obj/Debug/<tfm>/<Project>.GeneratedMSBuildEditorConfig.editorconfig` lists
exactly what the compiler was handed.

(A future NuGet package will declare these for you, via a `buildTransitive` props file.)

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

**Automatic — no host required.** Set `<AutoPrint>true</AutoPrint>` and the generator emits a
[`[ModuleInitializer]`](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/general#moduleinitializer-attribute)
that prints the banner **before `Main` runs**, with no code at all:

```xml
<AutoPrint>true</AutoPrint>
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
| `PoweredBy`          | `bool`   | `true`     | Append a right-aligned `Powered by .NET <version>` tagline.                              |
| `AutoPrint`          | `bool`   | `false`    | Emit a module initializer that prints the banner before `Main`.                          |

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

The full list and their authors is in [FIGLET-FONTS.md](FIGLET-FONTS.md).

> **Licensing note:** the bundled fonts originate from the FIGlet font collection and are authored by many individuals
> under varied terms. Each font's original header (including author credit) is preserved in the `.flf` file; they are
> redistributed on that basis and are **not** relicensed under this project's Apache-2.0 licence.

## Build-time errors

| ID       | Raised when                                                    |
|----------|----------------------------------------------------------------|
| `BAN001` | `BannerFont` is not a bundled font (suggests the nearest match) |
| `BAN002` | `BannerColor` is not a name, hex colour, or `default`           |
| `BAN003` | `BannerAlignment` is not `left`, `center` or `right`            |

## Differences from the Quarkus extension

| | Quarkus | .NET |
|---|---|---|
| Configuration | `application.properties` | MSBuild properties in the `.csproj` |
| Build-time step | Quarkus augmentation | Roslyn incremental source generator |
| Runtime hook | recorder + `TextBannerFormatter` | `[ModuleInitializer]` or `IHostedService` |
| Log integration | banner is a header on the log stream | banner is printed alongside; logging is untouched |
| Background colour | supported | **not supported** — a FIGlet block is mostly blank, so it renders poorly |
| Tagline version | Quarkus version, baked in at build | .NET version, read at **runtime** (the app may roll forward) |
| Live preview | Dev UI page | none |

## Rendering

Banners are drawn by a small, self-contained FIGlet renderer — a clean-room implementation of the public FIGfont v2
standard. It carries **no third-party rendering dependency**, and it never reaches your application: it runs inside the
compiler and only the resulting string is emitted.

## Licence

Apache License 2.0 — see [LICENSE](LICENSE). Bundled FIGlet fonts retain their original terms; see
[FIGLET-FONTS.md](FIGLET-FONTS.md).
