# Demonstrates Wait /pattern/ stopping the recording when matching text appears.
# Used in: docs/.../how--to/waiting.md
Output "wait-pattern.svg"

Set Cols 80
Set Rows 14
Set Theme "Dracula"
Set TransparentBackground true
Set TypingSpeed 20ms

Type "1..20 | ForEach-Object { Write-Host \"line $_\"; Start-Sleep -Milliseconds 120 }"
Enter
Wait /line 15/
Sleep 2s
