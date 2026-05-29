# Demonstrates Ctrl+C interrupting a running command.
# Used in: docs/.../how--to/keyboard-input.md
Output "keyboard-modifiers.svg"

Set Cols 80
Set Rows 14
Set Theme "Tokyo Night"
Set TransparentBackground true

Type "ping 127.0.0.1"
Enter
Sleep 4s
Ctrl+C
Wait
Sleep 2s
