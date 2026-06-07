# SGR text-style sampler (samples/vt-styles.ps1) rendered to SVG — reverse / dim /
# strikethrough / overline / underline / conceal plus bold / italic combinations.
# Shows the engine's SGR attribute model surviving into the browserless SVG output.
# Used in: explanation/browserless-engine.md
Output "vt-styles.svg"

Use doc
Set Theme "Default"
Set FontSize 22
Set Cols 64
Set Rows 12

Exec "pwsh -NoProfile -File ../vt-styles.ps1"
Wait
Sleep 500ms
