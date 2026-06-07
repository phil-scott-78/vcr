# Landing-page hero for docs/.../index.md. Regenerates the previously hand-authored
# vcr-install.svg as a checked-in tape so it stays in sync with the renderer.
# Safe by design: it only echoes taglines (it never runs a real install).
Output "vcr-install.svg"

Use doc
Set Cols 84
Set Rows 16
Set FontSize 20
Set TypingSpeed 45ms

Type "echo 'VCR# — record your terminal as code'"
Sleep 400ms
Enter
Wait
Sleep 800ms
Type "echo 'one .tape file -> SVG, GIF, MP4, WebM'"
Sleep 400ms
Enter
Wait
