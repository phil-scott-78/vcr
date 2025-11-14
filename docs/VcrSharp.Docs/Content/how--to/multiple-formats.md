---
title: "How to Generate Multiple Output Formats"
description: "Create GIF, MP4, and WebM outputs from a single tape file"
uid: "docs.how-to.multiple-formats"
order: 2600
---

## Overview

Generate GIF, MP4, and WebM files from a single recording.

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

```tape
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
Set Theme Dracula

Type "npm install VCR#"
Enter
Wait
```

## Format-Specific Settings

**GIF-only settings:**
- `MaxColors` - Color palette size (default: 256)
- `LoopOffset` - Delay before loop starts

**Video-only settings (MP4/WebM):**
- `Framerate` - Recording frame rate (default: 50)
- `PlaybackSpeed` - Playback multiplier (default: 1.0)

All settings apply to all formats unless noted otherwise.
