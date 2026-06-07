# Demonstrates Wait /pattern/ stopping the recording when matching text appears.
# Used in: docs/.../how--to/waiting.md
Output "wait-pattern.svg"

Use doc
Set Cols 80
Set Rows 14
Set TypingSpeed 20ms

Type "1..20 | ForEach-Object { Write-Host \"line $_\"; Start-Sleep -Milliseconds 120 }"
Enter
Wait /line 15/
Ctrl+C
Wait
