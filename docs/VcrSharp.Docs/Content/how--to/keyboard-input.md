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

## Special Keys

**Common keys:**
```tape
Enter           # Press Enter/Return
Backspace       # Delete previous character
Tab             # Tab key
Escape          # Escape key
Space           # Space bar
```

**Arrow keys (with optional repeat count):**
```tape
Up              # Move up one line
Down 5          # Press Down 5 times
Left 10         # Move left 10 characters
Right           # Move right one character
Down@100ms 3    # Press Down 3 times with 100ms delay between presses
```

**Navigation keys:**
```tape
PageUp          # Page up
PageDown        # Page down
Home            # Move to start of line
End             # Move to end of line
Delete          # Delete character at cursor
Insert          # Toggle insert mode
```

## Keyboard Shortcuts

Combine modifier keys with other keys:

**Common Ctrl combinations:**
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

**Other modifiers:**
```tape
Alt+Backspace   # Delete previous word
Shift+Tab       # Reverse tab
Ctrl+Alt+Delete # Multiple modifiers
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

## Examples

**Navigate an interactive TUI:**
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

**Edit a command with cursor movement:**
```tape
Type "echo 'Helo, World!'"
Sleep 300ms
Left 9          # Move cursor back to the typo
Backspace       # Delete 'o'
Type "ll"       # Type 'll' to make 'Hello'
Enter
Wait
```

**Use keyboard shortcuts:**
```tape
Type "ping google.com"
Enter
Sleep 2s
Ctrl+C          # Interrupt the ping
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
