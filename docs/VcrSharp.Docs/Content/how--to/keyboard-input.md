---
title: "How to Use Keyboard Shortcuts and Special Keys"
description: "Master keyboard input simulation including modifier keys, arrow keys, and special keys"
uid: "docs.how-to.keyboard-input"
order: 2700
---

## Overview

Simulate keyboard input including typing, special keys, arrow keys, and keyboard shortcuts.

## Basic Typing

Simulate character-by-character typing:

```tape
Type "echo 'Hello World'"
```

## Common Keys

```tape
Enter           # Press Enter/Return
Backspace       # Delete previous character
Tab             # Tab key
Escape          # Escape key
Space           # Space bar
```

**Example:**
```tape
Type "ls"
Space
Type "-la"
Enter
Wait
```

## Arrow Keys

### To move the cursor:

```tape
Up              # Move up one line
Down            # Move down one line
Left            # Move left one character
Right           # Move right one character
```

### To repeat arrow keys:

```tape
Down 5          # Press Down 5 times
Left 10         # Move left 10 characters
```

### To control arrow key speed:

```tape
Down@100ms 3    # Press Down 3 times with 100ms delay between presses
```

**Example - Navigate and edit:**
```tape
Type "echo 'Helo, World!'"
Sleep 300ms
Left 9          # Move cursor back to the typo
Backspace       # Delete 'o'
Type "ll"       # Type 'll' to make 'Hello'
Enter
Wait
```

## Navigation Keys

```tape
PageUp          # Page up
PageDown        # Page down
Home            # Move to start of line
End             # Move to end of line
Delete          # Delete character at cursor
Insert          # Toggle insert mode
```

**To navigate to line start and delete:**
```tape
Type "wrong command here"
Home            # Jump to start
Delete 5        # Delete first 5 characters
```

## Keyboard Shortcuts

### To send Ctrl combinations:

```tape
Ctrl+C          # Interrupt/cancel
Ctrl+D          # EOF/exit
Ctrl+Z          # Suspend
Ctrl+L          # Clear screen
Ctrl+A          # Jump to line start
Ctrl+E          # Jump to line end
Ctrl+W          # Delete word
Ctrl+U          # Delete to line start
```

**Example - Cancel a command:**
```tape
Type "ping google.com"
Enter
Sleep 2s
Ctrl+C          # Interrupt the ping
Wait
```

### To send Alt combinations:

```tape
Alt+Enter       # Alt+Enter
Alt+F           # Alt+F
Alt+Backspace   # Delete previous word
```

### To send Shift combinations:

```tape
Shift+Tab       # Reverse tab
```

### To send multiple modifiers:

```tape
Ctrl+Alt+Delete
Ctrl+Shift+T
```

## String Quoting

### Double quotes support escape sequences:

```tape
Type "First line\nSecond line"        # \n = newline
Type "Tab\there"                      # \t = tab
Type "Quote: \"Hello\""               # \" = literal quote
Type "Backslash: \\"                  # \\ = literal backslash
```

### Single quotes and backticks are literal:

```tape
Type 'No escape sequences: \n \t'     # Types literally
Type `Also literal: \n`               # Types literally
```

### For Windows paths, use single quotes or escape backslashes:

```tape
Type 'C:\Users\Alice\Documents'       # Single quotes: literal
Type "C:\\Users\\Alice\\Documents"    # Double quotes: escape backslashes
```

## Custom Typing Speed

### To override typing speed for one Type command:

```tape
Set TypingSpeed 50ms                  # Default speed

Type "Normal speed text"
Enter

Type@10ms "Fast typing here"          # Override: type very fast
Enter

Type@200ms "Slow deliberate typing"   # Override: type slowly
Enter
```

**Use slow typing for emphasis:**
```tape
Type "# Watch this carefully:"
Enter
Sleep 1s
Type@150ms "rm -rf /tmp/old-files"   # Slow typing draws attention
Sleep 500ms
Enter
Wait
```

## Practical Examples

### To navigate an interactive TUI:

```tape
Exec "npm create vite@latest"
Wait /Project name/
Type "my-app"
Enter
Wait /framework/
Down 2          # Navigate to React
Enter
Wait /variant/
Down 1          # Navigate to TypeScript
Enter
Wait
```

### To edit a command with cursor movement:

```tape
Type "git commit -m 'Fix bug in prodction'"
Sleep 500ms
Left 1          # Position at the typo
Left 1
Left 1
Backspace       # Delete 'c'
Type "u"        # Type 'u' to make 'production'
Home            # Jump to start
Enter
Wait
```

### To demonstrate shell shortcuts:

```tape
Type "long command here that we don't want"
Sleep 500ms
Ctrl+U          # Delete entire line
Sleep 300ms
Type "echo 'Starting fresh'"
Enter
Wait
```

### To combine different input types:

```tape
Type "vim example.txt"
Enter
Wait
Type "i"        # Insert mode
Type "Hello from VCR#!"
Escape          # Normal mode
Type ":wq"      # Save and quit
Enter
Wait
```

## Best Practices

**For realistic editing:** Add short sleeps between navigation and typing actions:
```tape
Type "comand"
Sleep 300ms     # Pause (noticing the typo)
Left 4          # Navigate
Sleep 200ms     # Pause (positioning)
Type "m"        # Fix
Sleep 300ms     # Pause (reviewing)
```

**For TUI navigation:** Use repeating arrow keys instead of multiple individual commands:
```tape
Down 5          # Instead of: Down, Down, Down, Down, Down
```

**For keyboard shortcuts:** Add brief sleeps after interrupts to let output settle:
```tape
Exec "long-running-command"
Sleep 3s
Ctrl+C
Sleep 500ms     # Let the interrupt message appear
Wait
```

## Troubleshooting

**If Backspace doesn't work as expected:**
The terminal may use different backspace behavior. Try `Ctrl+H` instead:
```tape
Ctrl+H          # Alternative backspace
```

**If arrow keys don't work in TUI applications:**
Ensure the application has fully started before sending arrow keys:
```tape
Exec "npm create vite@latest"
Wait /Project name/   # Wait for the prompt
Down 2                # Now arrow keys will work
```

**If special characters don't appear correctly:**
Use escape sequences in double quotes or switch to single quotes:
```tape
Type "Path: C:\\Windows\\System32"    # Escaped backslashes
Type 'Path: C:\Windows\System32'      # Or single quotes
```
