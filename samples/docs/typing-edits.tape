# Demonstrates Type + arrow keys + Backspace + Home (the typo-correction pattern).
# Used in: docs/.../tutorials/typing-demo.md
Output "typing-edits.svg"

Set Cols 90
Set Rows 12
Set Theme "Monokai"
Set TransparentBackground true

Type "echo 'Helo, World!'"
Sleep 500ms
Left 9
Sleep 300ms
Backspace
Sleep 100ms
Type "ll"
Sleep 500ms
Home
Sleep 300ms
Enter
Sleep 2s
