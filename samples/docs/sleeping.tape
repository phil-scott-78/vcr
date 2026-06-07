# Demonstrates Sleep pacing: pause after typing, and pause between commands.
# Used in: how--to/sleeping.md
Output "sleeping.svg"

Use doc
Set Cols 90
Set Rows 16
Set TypingSpeed 70ms

Type "# pacing demo: type, pause, then run"
Enter
Sleep 1s
Type "echo 'first step'"
Sleep 600ms
Enter
Wait
Sleep 1s
Type "echo 'second step'"
Sleep 600ms
Enter
Wait
