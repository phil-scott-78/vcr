---
title: "How to Generate Multiple Output Formats"
description: "Produce GIF, MP4, WebM, SVG, PNG, and frame-directory outputs from a single tape file"
uid: "docs.how-to.multiple-formats"
order: 2600
---

## Overview

Generate several output files from a single recording. VCR# renders the recorded terminal grid once and then
writes each declared `Output` in its own format — animated raster/video (GIF, MP4, WebM), vector SVG, a single-frame
PNG, or a directory of raw PNG frames.

> [!NOTE]
> **SVG, PNG, and the frame directory need no external tools** — they are written entirely in-process.
> FFmpeg (on your `PATH`) is required **only** for `.gif`, `.mp4`, and `.webm` output. See the
> [FFmpeg options reference](xref:docs.reference.ffmpeg-options) for details.

## Choosing a Format

| Format | Best For | Trade-offs |
|--------|----------|------------|
| **SVG** | Docs, READMEs, embedding | Lightweight, scalable, searchable text; no FFmpeg needed |
| **GIF** | GitHub READMEs, email | Maximum compatibility, but larger files and 256 colors max |
| **MP4** | Social media, presentations | Smaller files with better quality, requires a video player |
| **WebM** | Web embedding | Smallest files, supports transparency, but limited support in some apps |
| **PNG** | A single still frame | One image, no animation |
| **Frames dir** | Custom post-processing | Raw PNG sequence plus a manifest; you encode it yourself |

## Generate Multiple Formats

Add multiple `Output` commands to your tape file. Put every `Set` before the first action command:

```tape
Set Cols 100
Set Rows 30

Output demo.svg
Output demo.gif
Output demo.mp4
Output demo.webm

Type "echo 'Hello, World!'"
Enter
Wait
```

VCR# records once and writes all four files. The `.svg` is produced with no external tools; the `.gif`,
`.mp4`, and `.webm` are encoded with FFmpeg.

## Add Outputs from the CLI

Use the `--output` (`-o`) flag to add outputs without editing the tape file:

```bash
vcr demo.tape -o extra.mp4 -o extra.webm
```

CLI outputs are **appended** to the tape file's outputs.

## Other Output Formats

Beyond the video formats, the `Output` command also accepts:

- **`.svg`** — an animated, text-based SVG. Lightweight, scalable, and rendered without FFmpeg.
- **`.png`** — a single-frame PNG of the terminal.
- **A directory or extension-less path** (e.g. `Output frames/`) — writes the raw PNG frames plus a
  `frames.txt` manifest into that directory. No FFmpeg is involved.

```tape
Output demo.svg     # Animated SVG (no FFmpeg required)
Output final.png    # Single-frame PNG (no FFmpeg required)
Output frames/      # Raw PNG frames + manifest (no FFmpeg required)
```

> [!TIP]
> If you want output that works with zero extra installs, reach for **SVG** first — it needs neither FFmpeg
> nor a monospace font.

## Optimize File Sizes

### To reduce GIF size

```tape
Set MaxColors 128       # Reduce color palette, 1-256 (default: 256)
Set Cols 100            # Smaller dimensions = smaller file
Set Rows 30

Output demo.gif
```

Lower `MaxColors` values (64, 128) produce smaller GIFs but may reduce color fidelity.

### To control video quality

```tape
Set Framerate 30        # Lower framerate = smaller file (default: 50)
Set PlaybackSpeed 1.5   # Faster playback = shorter video = smaller file

Output demo.mp4
```

## Example: Multiple Distribution Channels

Generate several formats for different uses from one tape:

```tape
Set Cols 100
Set Rows 30
Set Theme "Dracula"

Output docs.svg             # For documentation embeds (no FFmpeg)
Output readme.gif           # For a GitHub README
Output social.mp4           # For social media
Output web.webm             # For website embedding

Type "npm install vcr"
Enter
Wait
```

## Format-Specific Settings

**GIF-only setting:**

- `MaxColors` — color palette size, 1–256 (default: 256).

**Timing settings (all animated formats — GIF, MP4, WebM, and SVG):**

- `Framerate` — recording frame rate, 1–120 (default: 50).
- `PlaybackSpeed` — playback speed multiplier (default: 1.0).

All other settings apply to every format unless noted otherwise. For the full list, see the
[configuration options reference](xref:docs.reference.configuration-options).
