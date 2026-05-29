# Demonstrates the Screenshot command capturing a static SVG mid-recording.
# Produces both an (unused) animated SVG and the screenshot we embed.
# Used in: docs/.../how--to/screenshots.md
Output "_screenshot-svg-recording.svg"

Set Cols 80
Set Rows 12
Set Theme "Nord"
Set TransparentBackground true

Type "dotnet --list-sdks"
Enter
Wait
Sleep 300ms
Screenshot "screenshot-svg.svg"
Sleep 2s
