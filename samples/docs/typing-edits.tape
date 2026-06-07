# Demonstrates Type + arrow keys + Backspace + Home (the typo-correction pattern).
# Matches the "Complete Code" block in tutorials/typing-demo.md (Monokai, 100x30).
# Used in: tutorials/typing-demo.md and explanation/interactive-recording.md
Output "typing-edits.svg"

Use doc
Set Theme "Monokai"
Set Cols 100
Set Rows 30

Type "echo 'Helo, World!'"
Sleep 500ms
Left 9
Sleep 300ms
Backspace
Sleep 100ms
Type "lo"
Sleep 500ms
Home
Sleep 300ms
Enter
