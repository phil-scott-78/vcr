---
title: "How to Choose and Apply Themes"
description: "Apply and customize color themes for your terminal recordings"
uid: "docs.how-to.themes"
order: 2400
---

## Overview

Control terminal colors using built-in themes.

## Getting Started

Use the `Set Theme` command in your tape file:

```tape
Output demo.gif

Set Theme Dracula       # Dark theme with purple accents
Set Cols 100
Set Rows 30

Type "echo 'Hello, World!'"
Enter
Wait
```

List all available themes:

```bash
vcr themes
```

This shows all 10 built-in themes with color previews.

## Built-in Themes

VCR# includes these themes:

- **Dracula** - Dark purple theme (popular default)
- **Monokai** - Dark warm colors
- **Nord** - Arctic blue-tinted dark theme
- **Gruvbox** - Retro warm dark theme
- **Solarized Dark** - Precision dark theme
- **Solarized Light** - Light variant of Solarized
- **GitHub Light** - GitHub's light theme
- **One Dark** - Atom's dark theme
- **Tokyo Night** - Dark with purple/blue accents
- **Catppuccin** - Pastel dark theme

## Choose Your Theme

| Use Case | Recommended Themes | Why |
|----------|-------------------|-----|
| **Documentation** | Dracula, GitHub Light | High contrast, widely readable |
| **Presentations** | Nord, Solarized Dark | Professional, calm colors |
| **Social Media** | Tokyo Night, Catppuccin | Vibrant, eye-catching |
| **Brand Colors** | Closest match | Override with CLI as needed |

## Testing Themes with CLI Overrides

Change the theme without editing the tape file:

```bash
vcr demo.tape --set Theme=Dracula -o dracula.gif
vcr demo.tape --set Theme=Monokai -o monokai.gif
```

**Generate multiple themed versions in a loop:**
```bash
for theme in Dracula Monokai Nord Gruvbox
do
  vcr demo.tape --set Theme=$theme -o "demo-$theme.gif"
done
```

CLI overrides take precedence over tape file settings.

## What Themes Control

Themes set:
- **Foreground** - Main text color
- **Background** - Terminal background color
- **ANSI colors** - 16 standard terminal colors (black, red, green, yellow, blue, magenta, cyan, white, and bright variants)

All other visual settings (font size, padding, border radius) are independent of themes.

## Transparent Backgrounds

Remove the background color while keeping text colors from the theme:

```tape
Set TransparentBackground true
Set Theme Dracula
```

**Use cases:**
- Embedding recordings in web pages with custom CSS backgrounds
- Creating videos with custom backgrounds
- Compositing multiple recordings together

## Example: Match Presentation Background

```tape
# For light backgrounds (Google Slides, PowerPoint with light theme)
Set Theme "GitHub Light"

# For dark backgrounds (Dark mode presentations)
Set Theme Dracula
```
