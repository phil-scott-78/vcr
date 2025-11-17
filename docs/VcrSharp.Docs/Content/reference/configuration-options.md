---
title: "Configuration Options Reference"
description: "Complete reference for all VCR# configuration settings"
uid: "docs.reference.configuration-options"
order: 4200
---

## Overview

VCR# provides extensive configuration options to customize your terminal recordings. Settings can be configured in two
ways:

1. **In tape files** using the `Set` command:
   ```tape
   Set Width 1200
   Set Theme "Dracula"
   ```

2. **Via CLI flags** using `--set`:
   ```bash
   vcr-sharp demo.tape --set Theme=Nord --set FontSize=24
   ```

CLI flags override tape file settings, allowing quick adjustments without modifying the tape file.

## Terminal Settings

### Width

**Type:** Integer
**Default:** `1200`
**Description:** Terminal width in pixels. Only used if `Cols` is not specified.

**Example:**

```tape
Set Width 1600
```

### Height

**Type:** Integer
**Default:** `600`
**Description:** Terminal height in pixels. Only used if `Rows` is not specified.

**Example:**

```tape
Set Height 800
```

### Cols

**Type:** Integer
**Default:** None
**Description:** Terminal width in character columns. When specified, overrides `Width` and auto-calculates viewport
dimensions based on font metrics.

**Example:**

```tape
Set Cols 80    # Classic 80-column terminal
```

### Rows

**Type:** Integer
**Default:** None
**Description:** Terminal height in character rows. When specified, overrides `Height` and auto-calculates viewport
dimensions.

**Example:**

```tape
Set Rows 24    # Classic 24-row terminal
```

### FontSize

**Type:** Integer
**Default:** `22`
**Description:** Font size in pixels.

**Example:**

```tape
Set FontSize 18
```

### FontFamily

**Type:** String
**Default:** `"monospace"`
**Description:** Font family name.

**Example:**

```tape
Set FontFamily "Fira Code"
Set FontFamily "Consolas"
```

### LetterSpacing

**Type:** Float
**Default:** `1.0`
**Description:** Letter spacing multiplier.

**Example:**

```tape
Set LetterSpacing 1.2    # Increase spacing
```

### LineHeight

**Type:** Float
**Default:** `1.0`
**Description:** Line height multiplier.

**Example:**

```tape
Set LineHeight 1.5
```

## Video Settings

### Framerate

**Type:** Integer
**Default:** `50`
**Range:** 1-120 fps
**Description:** Recording framerate in frames per second.

**Example:**

```tape
Set Framerate 60    # Smooth 60fps recording
Set Framerate 30    # Lower framerate for smaller files
```

### PlaybackSpeed

**Type:** Float
**Default:** `1.0`
**Description:** Playback speed multiplier. Values > 1.0 speed up playback, values < 1.0 slow it down.

**Example:**

```tape
Set PlaybackSpeed 1.5    # 1.5x speed
Set PlaybackSpeed 0.5    # Half speed (slow motion)
```

### LoopOffset

**Type:** Float
**Default:** `0`
**Description:** Loop offset in seconds for GIF animations. Adds delay before the GIF loops.

**Example:**

```tape
Set LoopOffset 2.0    # 2 second pause before loop
```

### MaxColors

**Type:** Integer
**Default:** `256`
**Range:** 1-256
**Description:** Maximum colors for GIF palette generation. GIF-specific setting.

**Example:**

```tape
Set MaxColors 128    # Reduce colors for smaller file
Set MaxColors 256    # Maximum quality
```

## Styling Settings

### Theme

**Type:** String
**Default:** `"Default"`
**Available Themes:**

- `Default` - VS Code Dark+
- `Dracula`
- `Monokai`
- `Nord`
- `SolarizedDark`
- `SolarizedLight`
- `OneDark`
- `GruvboxDark`
- `TokyoNight`
- `CatppuccinMocha`

**Description:** Color theme for the terminal.

**Example:**

```tape
Set Theme "Dracula"
Set Theme "Nord"
```

### Padding

**Type:** Integer
**Default:** `0`
**Description:** Padding in pixels around the terminal content.

**Example:**

```tape
Set Padding 20
```

### Margin

**Type:** Integer
**Default:** `0`
**Description:** Margin in pixels around the entire recording.

**Example:**

```tape
Set Margin 30
```

### MarginFill

**Type:** String
**Default:** None
**Description:** Margin fill color (hex code) or path to an image file.

**Example:**

```tape
Set MarginFill "#1a1b26"
Set MarginFill "background.png"
```

### WindowBarSize

**Type:** Integer
**Default:** `30`
**Description:** Window bar height in pixels (the decorative bar at the top).

**Example:**

```tape
Set WindowBarSize 40
Set WindowBarSize 0    # No window bar
```

### BorderRadius

**Type:** Integer
**Default:** `0`
**Description:** Border corner radius in pixels for rounded corners.

**Example:**

```tape
Set BorderRadius 8
```

### CursorBlink

**Type:** Boolean
**Default:** `true`
**Description:** Enable or disable cursor blinking.

**Example:**

```tape
Set CursorBlink false
```

### TransparentBackground

**Type:** Boolean
**Default:** `false`
**Description:** Use a transparent terminal background instead of the theme's background color.

**Example:**

```tape
Set TransparentBackground true
```

## Behavior & Timing Settings

### Shell

**Type:** String
**Default:** Platform-specific

- Windows: `pwsh` (or `powershell`, or `cmd` if PowerShell not available)
- Unix/macOS: `bash`

**Description:** Shell to use for the terminal session.

**Example:**

```tape
Set Shell "bash"
Set Shell "zsh"
Set Shell "cmd"
```

### WorkingDirectory

**Type:** String
**Default:** Current directory
**Description:** Working directory for the terminal session.

**Example:**

```tape
Set WorkingDirectory "C:\\Projects\\MyApp"
Set WorkingDirectory "/home/user/projects"
```

### TypingSpeed

**Type:** Duration
**Default:** `60ms`
**Description:** Delay between each keystroke when using the `Type` command.

**Example:**

```tape
Set TypingSpeed 100ms    # Slower typing
Set TypingSpeed 30ms     # Faster typing
```

### WaitTimeout

**Type:** Duration
**Default:** `15s`
**Description:** Maximum time to wait for a pattern match in `Wait` commands before timing out.

**Example:**

```tape
Set WaitTimeout 30s
```

### WaitPattern

**Type:** Regex
**Default:** Shell-specific prompt pattern
**Description:** Default regex pattern for detecting shell prompt completion in `Wait` commands.

**Example:**

```tape
Set WaitPattern />\s*$/       # PowerShell prompt
Set WaitPattern /\$\s*$/      # Bash prompt
```

### InactivityTimeout

**Type:** Duration
**Default:** `5s`
**Description:** How long to wait for terminal inactivity before considering a command complete. Used by `Exec` command.

**Example:**

```tape
Set InactivityTimeout 3s
```

### StartWaitTimeout

**Type:** Duration
**Default:** `10s`
**Description:** Maximum time to wait for first terminal activity before starting recording.

**Example:**

```tape
Set StartWaitTimeout 15s
```

### StartBuffer

**Type:** Duration
**Default:** `500ms`
**Description:** Amount of blank time to include before the first activity. Frames before (FirstActivity - StartBuffer)
are trimmed.

**Example:**

```tape
Set StartBuffer 1s
```

### EndBuffer

**Type:** Duration
**Default:** `100ms`
**Description:** Amount of time to include after the last detected activity before ending the recording.

**Example:**

```tape
Set EndBuffer 500ms
```

### StartupDelay

**Type:** Duration
**Default:** `3.5s`
**Description:** Delay before executing `Exec` commands at startup. Allows time for browser and terminal to fully
initialize. This setting uses shell-specific sleep commands to ensure cross-platform compatibility (PowerShell, CMD,
Bash, etc.).

**Example:**

```tape
Set StartupDelay 5s      # Wait 5 seconds before running Exec commands
Set StartupDelay 2.5s    # Shorter delay for faster systems
```

## Setting Precedence

When the same setting is configured in multiple places, the following precedence order applies (highest to lowest):

1. **CLI flags** (`--set` option)
2. **Tape file** (`Set` command)
3. **Default values**

**Example:**

```tape
# demo.tape
Set Theme "Dracula"
Set FontSize 22
```

```bash
# Override theme while keeping FontSize
vcr-sharp demo.tape --set Theme=Nord
```

## Complete Example

Here's a tape file demonstrating all major settings:

```tape
# Output configuration
Output "demo.gif"
Output "demo.mp4"

# Terminal dimensions
Set Cols 100
Set Rows 30
Set FontSize 22
Set FontFamily "monospace"

# Video settings
Set Framerate 50
Set PlaybackSpeed 1.0
Set MaxColors 256

# Styling
Set Theme "Dracula"
Set Padding 20
Set Margin 10
Set WindowBarSize 30
Set BorderRadius 8
Set CursorBlink true

# Behavior
Set Shell "pwsh"
Set TypingSpeed 60ms
Set WaitTimeout 15s
Set InactivityTimeout 5s
Set StartBuffer 500ms
Set EndBuffer 100ms
Set StartupDelay 3.5s

# Commands
Type "echo Hello VCR#"
Enter
Wait
```

## Setting Validation

VCR# validates settings and provides clear error messages for invalid values:

**Valid Ranges:**

- `Framerate`: 1-120 fps
- `MaxColors`: 1-256
- `Padding`, `Margin`, `BorderRadius`: ≥ 0
- `Width`, `Height`, `FontSize`, `WindowBarSize`: > 0
- Durations: Must be positive

**Error Example:**

```
Error: Framerate must be between 1 and 120 (got: 150)
Error: WorkingDirectory does not exist: C:\NonExistent
```
