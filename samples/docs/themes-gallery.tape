# Same content rendered across every theme via --set Theme=NAME.
# Used in: docs/.../how--to/themes.md (one swatch per theme)
# Overrides the doc preset's transparent background so each swatch shows the
# theme's own background colour. Run with `Run` sugar (= Type + Enter + Wait).
Output "themes-gallery.svg"

Use doc
Set TransparentBackground false
Set Cols 70
Set Rows 12
Set TypingSpeed 25ms

Run "Get-ChildItem"
