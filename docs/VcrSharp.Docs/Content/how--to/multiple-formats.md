---
title: "How to Generate Multiple Output Formats"
description: "Create GIF, MP4, and WebM outputs from a single tape file"
uid: "docs.how-to.multiple-formats"
order: 2600
---

## Overview

Generate multiple output files from a single recording. This guide focuses on the animated raster/video formats —
GIF, MP4, and WebM — but VCR# also supports SVG, PNG, and a raw frames directory (see [Other Output Formats](#other-output-formats) below).

## Choosing a Format

| Format | Best For | Trade-offs |
|--------|----------|------------|
| **GIF** | GitHub READMEs, documentation, email | Maximum compatibility, but larger files and 256 colors max |
| **MP4** | Social media, presentations | Smaller files with better quality, requires video player |
| **WebM** | Web embedding | Smallest files, but limited support in some browsers/apps |

## Generate Multiple Formats

Add multiple `Output` commands to your tape file:

```tape
Output demo.gif
Output demo.mp4
Output demo.webm

Set Cols 100
Set Rows 30

Type "echo 'Hello, World!'"
Enter
Wait
```

VCR# will generate all three files from a single recording.

## Add Outputs from CLI

Use the `--output` or `-o` flag to add outputs without editing the tape file:

```bash
vcr demo.tape -o extra.mp4 -o extra.webm
```

CLI outputs are **appended** to tape file outputs.

## Optimize File Sizes

### To reduce GIF size:

```tape
Output demo.gif

Set MaxColors 128       # Reduce color palette (default: 256)
Set Cols 100            # Smaller dimensions = smaller file
Set Rows 30
```

Lower `MaxColors` values (64, 128) produce smaller GIFs but may reduce quality.

### To control video quality:

```tape
Output demo.mp4

Set Framerate 30        # Lower framerate = smaller file (default: 50)
Set PlaybackSpeed 1.5   # Faster playback = shorter video = smaller file
```

## Example: Multiple Distribution Channels

Generate all three formats for different uses:

```tape
Output readme.gif           # For GitHub README
Output social.mp4           # For Twitter/LinkedIn
Output web.webm             # For website embedding

Set Cols 100
Set Rows 30
Set Theme "Dracula"

Type "npm install VCR#"
Enter
Wait
```

## Other Output Formats

Beyond GIF/MP4/WebM, the `Output` command also accepts:

- **`.svg`** — an animated, text-based SVG. Lightweight, scalable, and rendered without FFmpeg.
- **`.png`** — a single-frame PNG of the terminal.
- **A directory or extension-less path** (e.g. `Output frames/`) — writes the raw PNG frames plus a manifest into that directory.

```tape
Output demo.svg     # Animated SVG (no FFmpeg required)
Output final.png    # Single-frame PNG
Output frames/      # Raw PNG frames + manifest
```

## Format-Specific Settings

**GIF-only setting:**
- `MaxColors` - Color palette size, 1–256 (default: 256)

**Timing settings (all animated formats — GIF, MP4, WebM, and SVG):**
- `Framerate` - Recording frame rate, 1–120 (default: 50)
- `PlaybackSpeed` - Playback speed multiplier (default: 1.0; also affects SVG)

All other settings apply to every format unless noted otherwise.
