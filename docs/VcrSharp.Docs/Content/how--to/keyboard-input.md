---
title: "How to Use Keyboard Shortcuts and Special Keys"
description: "Master keyboard input simulation including modifier keys, arrow keys, and special keys"
uid: "docs.how-to.keyboard-input"
order: 2700
---

## Overview

Simulate keyboard input including typing, special keys, arrow keys, and keyboard shortcuts. VCR# writes the standard raw terminal byte sequence for every key and modifier chord, so input reaches your shell or TUI exactly as if you had typed it.

For the complete grammar, see the [tape syntax reference](xref:docs.reference.tape-syntax).

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

Chords can stack any combination of `Ctrl`, `Alt`, and `Shift` in any order, ending in a special key or letter (for example `Ctrl+Alt+Shift+Tab`). Each chord is emitted as the terminal escape sequence the application expects — `Ctrl+Right`, for instance, sends `ESC[1;5C`.

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
Set TypingSpeed 50ms                  # Slightly faster than the 60ms default

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
Type "lo"       # Type 'lo' to make 'Hello'
Enter
Wait
```

**Use keyboard shortcuts:**
```tape
Type "ping 127.0.0.1"
Enter
Sleep 4s
Ctrl+C          # Interrupt the ping
Wait
```

<VcrTape src="../demos/keyboard-modifiers.svg" />

## Troubleshooting

Key encoding is deterministic: every key and modifier chord is written as the standard terminal byte sequence (for example `Backspace` sends the usual delete byte, and `Ctrl+Right` sends `ESC[1;5C`). You do not need to substitute alternate keys to coax a particular code out of the terminal.

**If a TUI doesn't react to a key:**
The application may not have finished redrawing. VCR# already paces keys roughly 24ms apart, but a busy or slow TUI can need more time between presses. Add a brief `Sleep`, or `Wait` for the screen to update, before the next key:

```tape
Down            # Move the selection
Sleep 100ms     # Give the TUI time to redraw
Enter           # Confirm
```

**If arrow keys don't work in TUI applications:**
Make sure the application has fully started before sending arrow keys. Use `Wait` to block until its prompt or menu appears:

```tape
Exec "npm create vite@latest"
Wait /Project name/   # Wait for the prompt
Down 2                # Now arrow keys land in the running app
```

**If special characters don't appear correctly:**
Use escape sequences in double quotes or switch to single quotes:

```tape
Type "Path: C:\\Windows\\System32"    # Escaped backslashes
Type 'Path: C:\Windows\System32'      # Or single quotes
```
