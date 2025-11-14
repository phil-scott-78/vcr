---
title: "How VCR# Uses FFmpeg"
description: "Internal FFmpeg invocation reference - VCR#'s hardcoded encoding parameters"
uid: "docs.reference.ffmpeg-options"
order: 4300
---

## Overview

This document describes how VCR# internally uses FFmpeg to encode recordings. **These parameters are hardcoded in VCR# and not user-configurable.**

VCR# converts PNG frame sequences into GIF, MP4, and WebM formats using specific FFmpeg commands chosen for quality, compatibility, and file size balance.

**User-Controllable Settings:**
- `Framerate` - Set via `Set Framerate 50` in tape files
- `MaxColors` - Set via `Set MaxColors 256` in tape files (GIF only)
- `Width` and `Height` - Control output resolution indirectly

**All Other Parameters:** Hardcoded in VCR# (CRF values, codecs, presets, dithering, etc.)

**FFmpeg Requirement:** Version 4.0 or later must be available in system PATH.

## GIF Encoding

VCR# uses a two-pass approach for high-quality GIF encoding with optimized color palettes.

### Commands VCR# Invokes

**Pass 1: Palette Generation**
```bash
ffmpeg -y -i frames/%04d.png
  -vf "palettegen=max_colors=<MaxColors>:stats_mode=diff"
  palette.png
```

**Pass 2: GIF Creation**
```bash
ffmpeg -framerate <Framerate> -i frames/%04d.png -i palette.png
  -lavfi "paletteuse=dither=bayer:bayer_scale=5"
  -loop 0
  output.gif
```

### Parameters VCR# Uses

| Parameter | Value | Source | Rationale |
|-----------|-------|--------|-----------|
| `-framerate` | User's `Framerate` setting (default 50) | Tape file `Set Framerate` | Matches capture framerate for smooth playback |
| `max_colors` | User's `MaxColors` setting (default 256) | Tape file `Set MaxColors` | Allows quality/size trade-off control |
| `-loop` | `0` (infinite) | Hardcoded | Standard behavior for animated GIFs |
| `stats_mode` | `diff` | Hardcoded | Analyzes color differences for better palette selection |
| `dither` | `bayer` | Hardcoded | Bayer pattern provides smooth color transitions |
| `bayer_scale` | `5` | Hardcoded | Maximum dithering strength for best visual quality |

### Why These Choices?

**Two-pass encoding**: VCR# generates an optimized palette first (pass 1), then applies it (pass 2). This produces significantly better quality than single-pass GIF encoding.

**Bayer dithering at scale 5**: Maximum dithering strength creates smooth gradients even with limited color palettes. Essential for terminal recordings with subtle ANSI color variations.

**stats_mode=diff**: Analyzes color differences between frames rather than treating all frames equally. Better for terminal recordings where some frames have more color variation than others.

## MP4 Encoding

VCR# uses H.264 encoding for broad compatibility and good compression efficiency.

### Command VCR# Invokes

```bash
ffmpeg -framerate <Framerate> -pattern_type glob -i 'frames/*.png'
  -c:v libx264
  -pix_fmt yuv420p
  -crf 23
  -preset medium
  output.mp4
```

### Parameters VCR# Uses

| Parameter | Value | Source | Rationale |
|-----------|-------|--------|-----------|
| `-framerate` | User's `Framerate` setting (default 50) | Tape file `Set Framerate` | Matches capture framerate |
| `-c:v` | `libx264` | Hardcoded | Industry standard H.264 codec, universal compatibility |
| `-pix_fmt` | `yuv420p` | Hardcoded | Compatible with legacy players and mobile devices |
| `-crf` | `23` | Hardcoded | Balanced quality/size (visually lossless for most content) |
| `-preset` | `medium` | Hardcoded | Balanced encoding speed/compression efficiency |

### Why These Choices?

**CRF 23**: Constant Rate Factor 23 provides visually lossless quality for terminal content while keeping file sizes reasonable. Terminal recordings compress extremely well due to large solid-color areas and limited motion.

**yuv420p pixel format**: Required for compatibility with older players, iOS Safari, and embedded players. Modern alternatives (yuv444p) offer better quality but break playback on many platforms.

**libx264 over libx265**: H.265/HEVC provides better compression but has patent licensing concerns and limited browser support. H.264 works everywhere without licensing issues.

**preset=medium**: Balances encoding time (~5-15 seconds for typical recordings) with compression efficiency. Slower presets provide minimal size reduction for significantly longer encoding.

## WebM Encoding

VCR# uses VP9 encoding for modern web compatibility and excellent compression.

### Command VCR# Invokes

```bash
ffmpeg -framerate <Framerate> -pattern_type glob -i 'frames/*.png'
  -c:v libvpx-vp9
  -crf 30
  -b:v 0
  -speed 4
  output.webm
```

### Parameters VCR# Uses

| Parameter | Value | Source | Rationale |
|-----------|-------|--------|-----------|
| `-framerate` | User's `Framerate` setting (default 50) | Tape file `Set Framerate` | Matches capture framerate |
| `-c:v` | `libvpx-vp9` | Hardcoded | Modern VP9 codec, native browser support |
| `-crf` | `30` | Hardcoded | Balanced quality for WebM (roughly equivalent to H.264 CRF 23) |
| `-b:v` | `0` (VBR mode) | Hardcoded | Variable bitrate for optimal quality/size balance |
| `-speed` | `4` | Hardcoded | Balanced encoding speed (WebM encodes slower than H.264) |

### Why These Choices?

**CRF 30**: VP9's CRF scale differs from H.264. CRF 30 provides similar visual quality to H.264 CRF 23 while producing smaller files. VP9 is more efficient than H.264 for terminal content.

**VBR mode (b:v 0)**: Variable bitrate allocates more bits to complex frames, fewer to simple frames. Terminal recordings have highly variable complexity (static prompt vs. scrolling output), making VBR ideal.

**speed=4**: VP9 encoding is CPU-intensive. Speed 4 balances encoding time (10-30 seconds typical) with compression efficiency. Slower speeds provide diminishing returns.

**libvpx-vp9 over AV1**: AV1 offers better compression but has limited browser support and much slower encoding. VP9 works in all modern browsers without transcoding.

## How User Settings Affect FFmpeg

### Framerate

User's `Framerate` setting (from tape file `Set Framerate X`) is passed directly to FFmpeg's `-framerate` parameter for all formats:

```tape
Set Framerate 24    # VCR# invokes FFmpeg with -framerate 24
Set Framerate 30    # VCR# invokes FFmpeg with -framerate 30
Set Framerate 50    # VCR# invokes FFmpeg with -framerate 50 (default)
```

### MaxColors (GIF Only)

User's `MaxColors` setting controls GIF palette size:

```tape
Set MaxColors 128   # VCR# invokes palettegen with max_colors=128
Set MaxColors 256   # VCR# invokes palettegen with max_colors=256 (default)
```

Has no effect on MP4 or WebM encoding.

### Resolution (Width × Height)

Determined by PNG frame dimensions captured during recording. VCR# captures frames at the terminal's pixel dimensions (controlled by `Width`/`Height` or `Cols`/`Rows` settings). FFmpeg reads these PNG files and performs no scaling.

### Output Format Detection

VCR# selects encoding parameters based on output file extension specified in `Output` command:

```tape
Output "demo.gif"   # Invokes GIF encoding commands
Output "demo.mp4"   # Invokes MP4 encoding commands
Output "demo.webm"  # Invokes WebM encoding commands
```

## Format Specifications

### GIF
- **Container**: GIF89a
- **Color depth**: 8-bit (1-256 colors)
- **Compression**: LZW
- **Transparency**: Supported (index-based)
- **Animation**: Supported (loop count configurable)

### MP4
- **Container**: MPEG-4 Part 14
- **Video codec**: H.264/AVC (Main profile)
- **Pixel format**: YUV 4:2:0
- **Audio**: Not supported (video only)

### WebM
- **Container**: WebM (Matroska-based)
- **Video codec**: VP9
- **Pixel format**: YUV 4:2:0
- **Audio**: Not supported (video only)

## VCR#'s Encoding Process

1. **Frame Capture**: VCR# captures frames as PNG files during recording (`frame0001.png`, `frame0002.png`, ...)
2. **Palette Generation** (GIF only): Temporary `palette.png` created via FFmpeg palettegen pass
3. **FFmpeg Invocation**: VCR# invokes FFmpeg with format-specific hardcoded command
4. **Output Writing**: Encoded file written to path specified in tape file's `Output` command
5. **Cleanup**: Temporary frames and palette files deleted

VCR# spawns FFmpeg as a subprocess and waits for completion. Encoding typically takes 5-30 seconds depending on format, recording length, and CPU speed.

## Exit Code Handling

VCR# monitors FFmpeg exit codes to detect encoding failures:

| Exit Code | Meaning | VCR# Behavior |
|-----------|---------|-------------------|
| 0 | Success | Continue to next `Output` format if specified |
| 1 | Error | Abort recording, display FFmpeg error message |
| Other | Error | Abort recording, display FFmpeg error message |

FFmpeg writes diagnostic output to stderr, which VCR# captures and displays to users when encoding fails.

## External Documentation

- [FFmpeg Official Documentation](https://ffmpeg.org/documentation.html)
- [H.264 Encoding Guide](https://trac.ffmpeg.org/wiki/Encode/H.264)
- [VP9 Encoding Guide](https://trac.ffmpeg.org/wiki/Encode/VP9)
- [Palette Generation Filter](https://ffmpeg.org/ffmpeg-filters.html#palettegen)
- [Palette Use Filter](https://ffmpeg.org/ffmpeg-filters.html#paletteuse)
