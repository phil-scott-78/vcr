---
title: "Complete Tape File Syntax Reference"
description: "Comprehensive reference for all tape file commands and syntax"
uid: "docs.reference.tape-syntax"
order: 4100
---

## Overview

Tape files (`.tape`) define terminal recording scripts using a simple, readable syntax. Each command appears on its own line, and commands execute sequentially from top to bottom.

## File Structure

A typical tape file consists of:
- Configuration settings (`Set`, `Output`)
- Commands to execute (`Type`, `Exec`, `Wait`, etc.)
- Comments prefixed with `#`

**Example:**
```tape
# Configure output
Output "demo.gif"
Set Theme "Dracula"

# Type and execute commands
Type "echo Hello"
Enter
Wait
```

## Output Commands

### Output

Specifies the output file path for the recording.

**Syntax:**
```tape
Output "path/to/file.gif"
Output "path/to/file.mp4"
Output "path/to/file.webm"
```

**Supported Formats:**
- `.gif` - Animated GIF with palette optimization
- `.mp4` - H.264 video
- `.webm` - WebM video

**Multiple Outputs:**
You can specify multiple Output commands to generate different formats simultaneously:
```tape
Output "demo.gif"
Output "demo.mp4"
Output "demo.webm"
```

## Configuration Commands

### Set

Configure recording settings. See [Configuration Options Reference](configuration-options.md) for detailed information about each setting.

**Syntax:**
```tape
Set <SettingName> <Value>
Set <SettingName> "<StringValue>"
Set <SettingName> 100ms
```

#### Terminal Settings

```tape
Set Cols 100            # Character columns (recommended)
Set Rows 30             # Character rows (recommended)
Set Width 1200          # Terminal width in pixels (alternative to Cols)
Set Height 600          # Terminal height in pixels (alternative to Rows)
Set FontSize 22         # Font size in pixels
Set FontFamily "monospace"
Set LetterSpacing 1.0   # Letter spacing multiplier
Set LineHeight 1.0      # Line height multiplier
```

#### Video Settings

```tape
Set Framerate 50        # Recording framerate (1-120 fps)
Set PlaybackSpeed 1.0   # Playback speed multiplier
Set LoopOffset 0        # Loop offset for GIFs (seconds)
Set MaxColors 256       # GIF palette colors (1-256)
```

#### Styling Settings

```tape
Set Theme "Dracula"     # Built-in theme name
Set Padding 10          # Padding around terminal (pixels)
Set Margin 20           # Margin around recording (pixels)
Set MarginFill "#000000" # Margin fill color or image path
Set WindowBarSize 30    # Window bar height (pixels)
Set BorderRadius 8      # Border corner radius (pixels)
Set CursorBlink true    # Enable/disable cursor blinking
Set TransparentBackground false
```

#### Behavior Settings

```tape
Set Shell "pwsh"                    # Shell to use
Set WorkingDirectory "C:\\Projects"  # Working directory
Set TypingSpeed 60ms                # Delay between keystrokes
Set WaitTimeout 15s                 # Wait command timeout
Set WaitPattern />\s*$/             # Regex for prompt detection
Set InactivityTimeout 5s            # Command completion detection
Set StartWaitTimeout 10s            # Wait for first activity
Set StartBuffer 500ms               # Time before first activity
Set EndBuffer 100ms                 # Time after last activity
```

## Input Commands

### Type

Simulates typing text character-by-character with realistic delay.

**Syntax:**
```tape
Type "hello world"
Type@100ms "slow typing"    # Override typing speed
```

The `@` modifier allows per-command speed override. Escape sequences (`\n`, `\t`, `\r`, `\\`, `\"`) are supported in double-quoted strings.

### Enter

Simulates pressing the Enter/Return key.

**Syntax:**
```tape
Enter           # Press once
Enter 3         # Press three times
```

### Backspace

Simulates pressing the Backspace key.

**Syntax:**
```tape
Backspace       # Press once
Backspace 5     # Press five times
```

### Tab

Simulates pressing the Tab key.

**Syntax:**
```tape
Tab             # Press once
Tab 2           # Press twice
```

### Escape

Simulates pressing the Escape key.

**Syntax:**
```tape
Escape
```

### Space

Simulates pressing the Space key.

**Syntax:**
```tape
Space           # Press once
Space 10        # Press ten times
```

## Navigation Commands

### Arrow Keys

Navigate using arrow keys.

**Syntax:**
```tape
Up              # Press up arrow once
Down 5          # Press down arrow five times
Left
Right 3
```

### Page Navigation

Navigate pages in scrollable content.

**Syntax:**
```tape
PageUp
PageDown
```

### Line Navigation

Jump to line boundaries.

**Syntax:**
```tape
Home            # Jump to start of line
End             # Jump to end of line
```

### Character Operations

Insert or delete characters.

**Syntax:**
```tape
Delete          # Delete key
Insert          # Insert key
```

## Modifier Key Combinations

### Syntax

Combine modifier keys with other keys using the `+` operator.

**Format:**
```tape
Modifier+Key
Modifier+Modifier+Key
```

### Supported Modifiers

- `Ctrl` - Control key
- `Alt` - Alt key
- `Shift` - Shift key

### Common Combinations

```tape
Ctrl+C          # Interrupt command
Ctrl+D          # EOF / exit
Ctrl+Z          # Suspend (Unix) / Undo (Windows)
Alt+Enter       # Full screen toggle
Shift+Tab       # Reverse tab
Ctrl+Alt+Delete # System command
```

## Control Flow Commands

### Sleep

Pause execution for a fixed duration without waiting for output.

**Syntax:**
```tape
Sleep 1s        # Sleep for 1 second
Sleep 500ms     # Sleep for 500 milliseconds
Sleep 2m        # Sleep for 2 minutes
```

### Wait

Wait for terminal output to match a pattern or for the shell prompt to appear.

**Syntax:**
```tape
Wait                        # Wait for shell prompt (default)
Wait /pattern/              # Wait for regex pattern
Wait+Buffer /pattern/       # Search in persistent buffer (default scope)
Wait+Line /pattern/         # Search current line only
Wait+Screen /pattern/       # Search entire visible screen
Wait@10s /pattern/          # Override timeout
Wait+Screen@5s /Complete/   # Combine scope and timeout
```

**Scopes:**
- `Buffer` (default) - Maintains persistent buffer across Wait commands to catch fast-scrolling content
- `Line` - Search current line only
- `Screen` - Search entire visible terminal screen

### Hide

Stop capturing frames. Commands still execute but won't appear in the final recording.

**Syntax:**
```tape
Hide
```

### Show

Resume capturing frames after a `Hide` command.

**Syntax:**
```tape
Show
```

## Execution Commands

### Exec

Execute real shell commands and capture actual output. Unlike `Type`, which only simulates typing, `Exec` runs commands in the shell and waits for completion using inactivity detection.

**Syntax:**
```tape
Exec "npm install"
Exec "git status"
Exec "dotnet build"
```

Commands execute at the start of the recording session and run in the background. The recorder uses inactivity detection (configurable via `InactivityTimeout`) to determine when commands complete.

## Utility Commands

### Screenshot

Capture a single frame during recording.

**Syntax:**
```tape
Screenshot "output.png"
Screenshot "capture.jpg"
```

### Copy

Copy text to the system clipboard.

**Syntax:**
```tape
Copy "text to copy"
```

### Paste

Paste text from the system clipboard into the terminal.

**Syntax:**
```tape
Paste
```

## Environment Commands

### Env

Set environment variables for the terminal session.

**Syntax:**
```tape
Env API_KEY "secret123"
Env NODE_ENV "production"
```

### Require

Verify that required commands or dependencies are available before recording.

**Syntax:**
```tape
Require "git"
Require "node"
Require "dotnet"
```

### Source

Include commands from another tape file.

**Syntax:**
```tape
Source "common-setup.tape"
```

## String Quoting

### Double Quotes

Double-quoted strings support escape sequences.

**Example:**
```tape
Type "Hello\nWorld"     # Newline
Type "Tab\there"        # Tab character
Type "Quote: \"text\""  # Escaped quote
```

### Single Quotes

Single-quoted strings are literal - no escape sequence processing.

**Example:**
```tape
Type 'C:\Program Files\App'    # Backslashes are literal
```

### Backticks

Backtick-quoted strings are also literal.

**Example:**
```tape
Type `Hello\nWorld`     # \n is literal, not a newline
```

### Escape Sequences

Supported escape sequences in double-quoted strings:
- `\n` - Newline
- `\t` - Tab
- `\r` - Carriage return
- `\\` - Backslash
- `\"` - Double quote

## Duration Literals

### Format

Duration literals consist of a number followed by a unit suffix.

**Format:**
```tape
<number><unit>
```

### Supported Units

- `ms` - Milliseconds
- `s` - Seconds
- `m` - Minutes

**Examples:**
```tape
Sleep 100ms
Set TypingSpeed 50ms
Wait@10s /pattern/
Set StartWaitTimeout 2m
```

## Regex Patterns

### Format

Regex patterns are enclosed in forward slashes.

**Format:**
```tape
/pattern/
```

### Usage

Regex patterns are primarily used in `Wait` commands and `Set WaitPattern`:

```tape
Wait />\s*$/                    # Wait for shell prompt
Wait /Complete/                 # Wait for "Complete" text
Wait /\d{3}\s+tests passed/     # Wait for test results
Set WaitPattern /\$\s*$/        # Configure default pattern
```

## Comments

### Syntax

Lines starting with `#` are comments and are ignored during parsing.

**Example:**
```tape
# This is a comment
Output "demo.gif"

# Configure terminal appearance
Set Theme "Dracula"
Set FontSize 24

# Type a command
Type "echo Hello"
```

## Parser Specifications

### Character Encoding

Tape files must use UTF-8 encoding. BOM (Byte Order Mark) is optional but not required.

### Line Endings

All line ending styles are supported:
- `LF` (Unix/Linux/macOS) - `\n`
- `CRLF` (Windows) - `\r\n`
- `CR` (legacy Mac) - `\r`

### String Literal Limits

| Limit | Value |
|-------|-------|
| Maximum string length | Unlimited |
| Maximum escape sequence length | 2 characters (`\n`, `\t`, etc.) |
| Nested quotes | Not supported |

### Regex Pattern Engine

VCR# uses .NET Regular Expressions (System.Text.RegularExpressions).

**Supported features:**
- Standard regex metacharacters (`.`, `*`, `+`, `?`, `^`, `$`, etc.)
- Character classes (`\d`, `\w`, `\s`, `[a-z]`, etc.)
- Quantifiers (`{n}`, `{n,m}`, `*`, `+`, `?`)
- Groups and captures (`(...)`, `(?:...)`)
- Lookahead/lookbehind (`(?=...)`, `(?!...)`, `(?<=...)`, `(?<!...)`)
- Alternation (`|`)

**Not supported:**
- Flags/modifiers inside pattern (`/pattern/i` style)
- Pattern compilation hints
- Named captures in Wait commands (captures are not extracted)

**Default behavior:**
- Case-sensitive matching
- Single-line mode (`.` does not match newlines)
- No timeout (patterns must match eventually or timeout via `WaitTimeout`)

### Duration Parsing

Duration literals are parsed using the format `<number><unit>`.

**Number format:**
- Integer: `100ms`, `5s`
- Decimal: `1.5s`, `0.5m`
- No spaces between number and unit

**Valid ranges:**
- Milliseconds: 0-9,223,372,036,854,775,807 ms
- Seconds: 0-9,223,372,036,854,775 s
- Minutes: 0-153,722,867,280,912 m

### Command Ordering

Commands execute sequentially in file order from top to bottom. `Set` and `Output` commands can appear anywhere but apply immediately when encountered.

**Example:**
```tape
Set FontSize 20      # Applies before recording starts
Type "hello"         # Uses FontSize 20
Set FontSize 24      # Changes font size mid-recording
Type "world"         # Uses FontSize 24
```

### Validation Rules

**Parser errors for:**
- Unknown command names
- Missing required arguments
- Invalid argument types (string where number expected)
- Malformed duration literals (`10mss`, `5 s`)
- Unclosed string quotes
- Invalid escape sequences in double-quoted strings
- Empty regex patterns (`//`)
- Modifier key combinations without target key (`Ctrl+`)

**Runtime errors for:**
- Invalid setting names in `Set` commands
- Invalid theme names
- Regex patterns that fail to compile
- File paths that cannot be written (Output, Screenshot)
- Commands referenced in `Require` that are not found
- Source files that do not exist or contain syntax errors

### Whitespace Handling

- Leading and trailing whitespace on lines is ignored
- Blank lines are ignored
- Whitespace between command name and arguments is required
- Whitespace within string literals is preserved

### Case Sensitivity

- Command names are case-insensitive (`Type`, `type`, `TYPE` all valid)
- Setting names are case-insensitive (`Set FontSize`, `Set fontsize` both valid)
- String values are case-sensitive (`Set Theme "Dracula"` ≠ `Set Theme "dracula"`)
- Regex patterns are case-sensitive by default
