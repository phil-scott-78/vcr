# Demonstrates the Screenshot command capturing a static SVG mid-recording.
# Produces both an (unused) animated SVG and the screenshot we embed.
# Used in: how--to/screenshots.md and reference/tape-syntax.md
Output "_screenshot-svg-recording.svg"

Use doc
Set Theme "Nord"
Set Cols 80
Set Rows 12

Type "dotnet --list-sdks"
Enter
Wait
Sleep 300ms
Screenshot "screenshot-svg.svg"
