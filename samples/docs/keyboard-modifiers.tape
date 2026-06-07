# Demonstrates Ctrl+C interrupting a running command.
# Matches the "Use keyboard shortcuts" example in how--to/keyboard-input.md.
# Used in: how--to/keyboard-input.md and explanation/interactive-recording.md
Output "keyboard-modifiers.svg"

Use doc
Set Theme "Tokyo Night"
Set Cols 80
Set Rows 14

Type "ping 127.0.0.1"
Enter
Sleep 4s
Ctrl+C
Wait
