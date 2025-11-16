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

## Common Use Cases

**Test different themes:**
```bash
vcr demo.tape --set Theme=Dracula
vcr demo.tape --set Theme=Monokai
```

**Generate different resolutions:**
```bash
vcr demo.tape --set Width=800 --set Height=600 -o demo-mobile.gif
vcr demo.tape --set Width=1920 --set Height=1080 -o demo-desktop.gif
```

**Adjust video settings:**
```bash
vcr demo.tape --set Framerate=60
vcr demo.tape --set PlaybackSpeed=0.5
```

## The --output Flag

Add additional output files from the command line:

```bash
vcr demo.tape -o extra-output.mp4
vcr demo.tape --output extra-output.webm
```

CLI outputs are **appended** to any `Output` commands in the tape file.

## Example: Generate Variants in a Loop

Create themed variants without editing files:

```bash
for theme in Dracula Monokai Gruvbox Nord
do
  vcr demo.tape --set Theme=$theme -o "demo-$theme.gif"
done
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

For a complete list, see the [Settings Reference](../reference/settings).
