#!/usr/bin/env bash
# Renders every tape under samples/docs/ to SVG and copies the results
# into docs/VcrSharp.Docs/Content/_demos/ for embedding via <VcrTape>.
#
# Run from the repo root:   ./scripts/render-docs-samples.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SAMPLES="$REPO_ROOT/samples/docs"
DEMOS="$REPO_ROOT/docs/VcrSharp.Docs/Content/demos"
VCR=(dotnet run --project "$REPO_ROOT/src/VcrSharp.Cli" --no-build --)

mkdir -p "$DEMOS"
cd "$SAMPLES"

# Single-shot tapes — render whatever Output the tape declares.
for tape in hello-world.tape typing-edits.tape exec-real-command.tape \
            wait-pattern.tape keyboard-modifiers.tape screenshot-svg.tape; do
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

# Move all rendered SVGs into the docs _demos directory.
# Skip tapes themselves and any leading-underscore "private" outputs we don't embed.
for svg in *.svg; do
  case "$svg" in
    _*) continue ;;          # internal recordings we don't ship
    themes-gallery.svg) continue ;;  # placeholder produced by Output line
  esac
  mv -f "$svg" "$DEMOS/$svg"
  echo "    -> $DEMOS/$svg"
done

# Clean up the placeholders we skipped.
rm -f _*.svg themes-gallery.svg

echo "Done."
