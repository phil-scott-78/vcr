# Demonstrates Exec running a real command and capturing its output.
# Used in: docs/.../tutorials/exec-commands.md
Output "exec-real-command.svg"

Set Cols 90
Set Rows 18
Set Theme "Dracula"
Set TransparentBackground true

Exec "dotnet --version"
Wait
Sleep 2s
