#!/usr/bin/env bash
#
# Builds both packages and proves they work from a consumer's point of view.
#
# Everything here is checked through a real NuGet install rather than a project
# reference, because the things that break in packaging -- the analyzers/ folder
# layout, the buildTransitive props, the tool manifest -- are invisible to an
# ordinary build.
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# A unique version every run. NuGet extracts a package into ~/.nuget/packages once
# and reuses it forever, so reusing a version would silently test a stale copy.
VERSION="0.0.0-test.$(date +%Y%m%d%H%M%S)"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"; rm -rf "$HOME/.nuget/packages/dotnetbanner/$VERSION" "$HOME/.nuget/packages/dotnetbanner.tool/$VERSION"' EXIT

FEED="$WORK/feed"
mkdir -p "$FEED"

pass() { printf '  \033[32mok\033[0m   %s\n' "$1"; }
fail() { printf '  \033[31mFAIL\033[0m %s\n' "$1"; exit 1; }

echo "Packing $VERSION"
dotnet pack "$ROOT/src/Banner"            -c Release -p:Version="$VERSION" -o "$FEED" --nologo -v:q >/dev/null
dotnet pack "$ROOT/src/DotNetBanner.Tool" -c Release -p:Version="$VERSION" -o "$FEED" --nologo -v:q >/dev/null

# ---------------------------------------------------------------- package layout

echo
echo "Package layout"

# Listed once into a variable: `unzip -l | grep -q` would leave unzip killed by
# SIGPIPE, and `set -o pipefail` reports that as a failed pipeline.
LAYOUT="$(unzip -l "$FEED/DotNetBanner.$VERSION.nupkg")"

for path in "lib/net10.0/Banner.dll" \
            "lib/net10.0/Banner.xml" \
            "analyzers/dotnet/cs/Banner.Generator.dll" \
            "buildTransitive/DotNetBanner.props" \
            "README.md"; do
    grep -qF "$path" <<<"$LAYOUT" \
        && pass "$path" || fail "$path missing from DotNetBanner.nupkg"
done

[ -f "$FEED/DotNetBanner.$VERSION.snupkg" ] \
    && pass "symbol package" || fail "no .snupkg produced"

# ---------------------------------------------------------------- the library

echo
echo "Library package"

CONSUMER="$WORK/Consumer"
dotnet new console -o "$CONSUMER" --force >/dev/null

cat > "$CONSUMER/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

dotnet add "$CONSUMER" package DotNetBanner --version "$VERSION" --no-restore >/dev/null 2>&1

# Only the settings -- no CompilerVisibleProperty. If the props file is missing or
# misnamed these are silently ignored and the banner renders as the default.
python3 - "$CONSUMER/Consumer.csproj" <<'EOF'
import pathlib, sys
p = pathlib.Path(sys.argv[1])
p.write_text(p.read_text().replace("</PropertyGroup>", """
    <BannerText>{red}Package {bright-cyan}Check</BannerText>
    <BannerFont>slant</BannerFont>
    <BannerAutoPrint>true</BannerAutoPrint>
  </PropertyGroup>""", 1))
EOF

OUT="$(dotnet run --project "$CONSUMER" 2>&1)"

grep -q "Powered by .NET" <<<"$OUT" \
    && pass "banner printed with no user code" || fail "no banner: $OUT"

# 'slant' was asked for; the default 'standard' would look different. A backslash
# in the art only appears once the requested font actually took effect.
grep -q '\\' <<<"$OUT" \
    && pass "requested font applied (props file reached the generator)" \
    || fail "banner rendered with the default font -- buildTransitive props not applied"

# Output is redirected here, so the plain variant must be chosen.
# if/else rather than `&& fail || pass`: with the latter, a grep that errors for
# an unrelated reason reports success. $'\x1b' is ANSI-C quoting, which BSD grep
# needs -- it has no -P.
if grep -q $'\x1b' <<<"$OUT"; then
    fail "escape codes leaked into redirected output"
else
    pass "no escape codes when redirected"
fi

# ---------------------------------------------------------------- diagnostics

echo
echo "Build-time diagnostics"

BAD="$(dotnet build "$CONSUMER" -p:BannerFont=dooom --nologo -v:q 2>&1 || true)"

grep -q "BAN001" <<<"$BAD" && pass "BAN001 on an unknown font" || fail "no BAN001: $BAD"
grep -q "doom"   <<<"$BAD" && pass "suggests the nearest font"  || fail "no suggestion"

# ---------------------------------------------------------------- the tool

echo
echo "Tool package"

TOOLS="$WORK/tools"
dotnet tool install --tool-path "$TOOLS" --add-source "$FEED" DotNetBanner.Tool --version "$VERSION" >/dev/null 2>&1

BANNER="$TOOLS/dotnet-banner"

[ -x "$BANNER" ] && pass "installs as dotnet-banner" || fail "tool not installed"

FONTS="$("$BANNER" list-fonts | wc -l | tr -d " ")"
[ "$FONTS" = "246" ] && pass "list-fonts lists 246 fonts" || fail "list-fonts gave $FONTS"

PREVIEW="$("$BANNER" preview --font doom --text Hi)"
grep -qF "Powered by .NET" <<<"$PREVIEW" \
    && pass "preview renders" || fail "preview produced nothing"

"$BANNER" --help >/dev/null 2>&1 && pass "--help exits 0" || fail "--help exited non-zero"

if "$BANNER" nonsense >/dev/null 2>&1; then
    fail "unknown command exited 0"
else
    pass "unknown command exits non-zero"
fi

echo
echo "All package checks passed ($VERSION)"
