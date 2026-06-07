# Demonstrates Exec running a real command and capturing its output.
# Used in: tutorials/exec-commands.md and reference/tape-syntax.md
Output "exec-real-command.svg"

Use doc
Set Cols 80
Set Rows 8

Exec "dotnet --version"
Wait
