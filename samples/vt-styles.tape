# Renders the SGR text-style sampler (samples/vt-styles.ps1) to an SVG so the reverse / dim /
# strikethrough / overline / conceal attributes can be eyeballed against the raster (GIF/PNG) output.
#
#   vcr samples/vt-styles.tape          # browserless: ConPTY/Unix PTY + VT engine, no FFmpeg for SVG
#
# Needs pwsh on PATH (the default shell on Windows; cross-platform elsewhere).
Output "vt-styles.svg"

Set FontSize 22
Set Cols 64
Set Rows 12
Set Theme "Default"

Exec "pwsh -NoProfile -File ./samples/vt-styles.ps1"
Wait
Sleep 500ms
