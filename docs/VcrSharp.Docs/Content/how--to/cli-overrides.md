---
title: "How to Override Settings from CLI"
description: "Use command-line flags to override tape file settings without editing files"
uid: "docs.how-to.cli-overrides"
order: 2500
---

## Overview

Test variations of your recording without editing the tape file using command-line overrides.

## The --set Flag

Override any tape file setting from the command line:

```bash
vcr demo.tape --set Theme=Dracula
```

### Multiple Overrides

Chain multiple `--set` flags to override several settings:

```bash
vcr demo.tape --set Theme=Monokai --set Width=1200 --set Height=800
```

CLI overrides always take precedence over tape file settings.

## Common Override Scenarios

### To test different themes:

```bash
vcr demo.tape --set Theme=Dracula
vcr demo.tape --set Theme=Monokai
vcr demo.tape --set Theme=Gruvbox
```

### To generate different resolutions:

```bash
# Mobile version
vcr demo.tape --set Width=800 --set Height=600 -o demo-mobile.gif

# Desktop version
vcr demo.tape --set Width=1920 --set Height=1080 -o demo-desktop.gif
```

### To adjust video settings:

```bash
# Higher framerate
vcr demo.tape --set Framerate=60

# Slower playback
vcr demo.tape --set PlaybackSpeed=0.5
```

### To change appearance quickly:

```bash
vcr demo.tape --set FontSize=16 --set Padding=30 --set BorderRadius=10
```

## The --output Flag

Add additional output files from the command line:

```bash
vcr demo.tape -o extra-output.mp4
vcr demo.tape --output extra-output.webm
```

CLI outputs are **appended** to any `Output` commands in the tape file.

## Practical Examples

### Create themed variants without editing files:

```bash
for theme in Dracula Monokai Gruvbox Nord
do
  vcr demo.tape --set Theme=$theme -o "demo-$theme.gif"
done
```

### Generate multi-resolution outputs:

```bash
# Social media version (square)
vcr demo.tape --set Width=1080 --set Height=1080 -o demo-social.mp4

# Documentation version (standard)
vcr demo.tape --set Width=1200 --set Height=700 -o demo-docs.gif

# Presentation version (wide)
vcr demo.tape --set Width=1920 --set Height=1080 -o demo-presentation.mp4
```

### Quick iteration during development:

```bash
# Test with faster playback to iterate quickly
vcr demo.tape --set PlaybackSpeed=2.0 -o test-fast.gif

# Once satisfied, render at normal speed
vcr demo.tape -o final.gif
```

## When to Use CLI Overrides

**Use CLI overrides when:**
- Testing different themes or color schemes
- Generating multiple resolutions from one tape file
- Iterating quickly during development
- Creating variants without duplicating tape files

**Use tape file settings when:**
- Settings are permanent for that recording
- You want version-controlled configuration
- Settings are part of the recording's definition

## Overridable Settings

All `Set` commands can be overridden from the CLI. Common examples:

- `Theme` - Color scheme
- `Width` / `Height` - Terminal dimensions
- `FontSize` - Text size
- `Framerate` - Recording frame rate
- `PlaybackSpeed` - Playback multiplier
- `Padding` - Terminal padding
- `BorderRadius` - Corner rounding
- `TypingSpeed` - Typing animation speed

For a complete list, see the [Settings Reference](../reference/settings.md).
