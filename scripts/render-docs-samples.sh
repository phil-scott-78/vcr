#!/usr/bin/env bash
# Renders every tape under samples/docs/ to SVG and copies the results into
# docs/VcrSharp.Docs/Content/demos/ for embedding via <VcrTape>. Also exercises
# the `vcr snap` / `vcr capture` one-off commands and the landing-page hero.
#
# The docs tapes share a vcr.toml `doc` preset (samples/docs/vcr.toml); each tape
# pulls it in with `Use doc` and overrides only what it needs.
#
# Run from the repo root:   ./scripts/render-docs-samples.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SAMPLES="$REPO_ROOT/samples/docs"
CONTENT="$REPO_ROOT/docs/VcrSharp.Docs/Content"
DEMOS="$CONTENT/demos"
VCR=(dotnet run --project "$REPO_ROOT/src/VcrSharp.Cli" --no-build --)

mkdir -p "$DEMOS"
cd "$SAMPLES"

# Quick-capture commands (no tape file). Run first, while the directory holds only
# tapes + vcr.toml, so the `snap` listing stays clean.
echo ">>> snap (static SVG of a command's final output)"
"${VCR[@]}" snap "Get-ChildItem *.tape | Format-Table Name, Length -AutoSize" -o quick-snap.svg --theme "One Dark" --transparent-background --disable-cursor

echo ">>> capture (animated SVG of a command start-to-finish)"
"${VCR[@]}" capture "ping 127.0.0.1" -o quick-capture.svg --theme "One Dark" --transparent-background

# Single-shot tapes — render whatever Output the tape declares.
for tape in hello-world.tape typing-edits.tape exec-real-command.tape \
            wait-pattern.tape keyboard-modifiers.tape screenshot-svg.tape \
            static-widget.tape multiple-formats.tape sleeping.tape \
            vt-styles.tape; do
  echo ">>> $tape"
  "${VCR[@]}" "$tape"
done

# Themes gallery — same tape, one render per theme.
THEMES=(
  "Default"
  "Dracula"
  "Monokai"
  "Nord"
  "Solarized Dark"
  "Solarized Light"
  "One Dark"
  "Gruvbox Dark"
  "Tokyo Night"
  "Catppuccin Mocha"
)
for theme in "${THEMES[@]}"; do
  slug="$(echo "$theme" | tr ' ' '-')"
  echo ">>> themes-gallery.tape ($theme -> themes-$slug.svg)"
  "${VCR[@]}" themes-gallery.tape --set "Theme=$theme" -o "themes-$slug.svg"
done

# Landing-page hero — lives in the Content root, not demos/.
echo ">>> landing.tape (-> $CONTENT/vcr-install.svg)"
"${VCR[@]}" landing.tape
mv -f vcr-install.svg "$CONTENT/vcr-install.svg"
echo "    -> $CONTENT/vcr-install.svg"

# Move all rendered SVGs into the docs demos directory.
# Skip tapes themselves and any leading-underscore "private" outputs we don't embed.
for svg in *.svg; do
  case "$svg" in
    _*) continue ;;                  # internal recordings we don't ship
    themes-gallery.svg) continue ;;  # placeholder produced by Output line
  esac
  mv -f "$svg" "$DEMOS/$svg"
  echo "    -> $DEMOS/$svg"
done

# Clean up the placeholders we skipped.
rm -f _*.svg themes-gallery.svg

echo "Done."
