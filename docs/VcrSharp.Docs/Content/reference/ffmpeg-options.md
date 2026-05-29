---
title: "How VCR# Uses FFmpeg"
description: "How VCR# drives FFmpeg (via FFMpegCore) to encode GIF, MP4, WebM, and PNG output"
uid: "docs.reference.ffmpeg-options"
order: 4300
---

## Overview

VCR# encodes raster and video output (GIF, MP4, WebM, and single-frame PNG) with **FFmpeg**, driven through the
[FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) .NET library. It does **not** shell out hand-written `ffmpeg`
command lines — FFMpegCore builds the argument list. The commands shown below are the *equivalent* invocations so you can
see exactly what VCR# asks FFmpeg to do.

> SVG (`Output demo.svg`) and the raw frames directory (`Output frames/`) are produced **without** FFmpeg. Only `.gif`,
> `.mp4`, `.webm`, and `.png` use it.

Most encoding parameters are fixed in code, but a handful of tape settings change the FFmpeg pipeline — see
[Settings that affect encoding](#settings-that-affect-encoding).

**FFmpeg requirement:** FFmpeg must be available on the system `PATH`. VCR# checks only that the executable is present;
no specific version is enforced.

## How Frames Become a Video

During recording, VCR# captures **two PNG layers per frame** into a temporary directory:

- `frame-text-00001.png`, `frame-text-00002.png`, … — the terminal text
- `frame-cursor-00001.png`, `frame-cursor-00002.png`, … — the cursor layer

It then writes two FFmpeg **concat-demuxer manifests**, `frames-text.txt` and `frames-cursor.txt`. Each manifest lists its
frames with a per-frame `duration` derived from the actual capture timestamps, which is how VCR# supports variable frame
timing rather than a fixed image-sequence rate:

```text
file 'frame-text-00001.png'
duration 0.020000
file 'frame-text-00002.png'
duration 0.020000
...
file 'frame-text-00120.png'
```

Both manifests are passed as inputs. Input `[0:v]` is the text layer and `[1:v]` is the cursor layer; they are composited
with `overlay=0:0` and then encoded. The output frame rate is set by the `fps` filter (not by an input `-framerate`), and
`setpts=PTS/<PlaybackSpeed>` applies the playback-speed multiplier.

After encoding, the temporary frame directory and manifests are deleted. There is no intermediate `palette.png` file —
GIF palette generation happens inline in the filter graph.

## GIF

Single-pass encoding with an inline `palettegen`/`paletteuse` filter. Equivalent invocation (no padding):

```bash
ffmpeg -f concat -safe 0 -i frames-text.txt \
       -f concat -safe 0 -i frames-cursor.txt \
       -filter_complex "[0:v][1:v]overlay=0:0[merged];\
[merged]fps=<Framerate>,setpts=PTS/<PlaybackSpeed>[final];\
[final]split[s0][s1];\
[s0]palettegen=max_colors=<MaxColors>[p];\
[s1][p]paletteuse" \
       -f gif output.gif
```

| Parameter | Value | Source |
|-----------|-------|--------|
| `fps` | `Framerate` (default 50) | `Set Framerate` |
| `setpts` | `PTS/PlaybackSpeed` (default 1.0) | `Set PlaybackSpeed` |
| `palettegen=max_colors` | `MaxColors` (default 256) | `Set MaxColors` |

The palette is generated and applied within one pass via `split`. No `stats_mode`, `dither`, `bayer_scale`, or explicit
`-loop` argument is passed; the GIF loops by container default.

## MP4

H.264 (`libx264`). Equivalent invocation (no padding):

```bash
ffmpeg -f concat -safe 0 -i frames-text.txt \
       -f concat -safe 0 -i frames-cursor.txt \
       -filter_complex "[0:v][1:v]overlay=0:0[merged];\
[merged]fps=<Framerate>,setpts=PTS/<PlaybackSpeed>[speed];\
[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'" \
       -c:v libx264 -crf 20 -pix_fmt yuv420p -movflags +faststart output.mp4
```

| Parameter | Value | Source |
|-----------|-------|--------|
| `-c:v` | `libx264` | Fixed |
| `-crf` | `20` | Fixed |
| `-pix_fmt` | `yuv420p` | Fixed |
| `-movflags` | `+faststart` | Fixed |

No `-preset` is specified, so FFmpeg's default preset is used. The `scale='trunc(iw/2)*2':'trunc(ih/2)*2'` filter forces
even dimensions, which `yuv420p` requires.

## WebM

VP9 (`libvpx-vp9`), with optional transparency. Equivalent invocation (no padding, opaque):

```bash
ffmpeg -f concat -safe 0 -i frames-text.txt \
       -f concat -safe 0 -i frames-cursor.txt \
       -filter_complex "[0:v][1:v]overlay=0:0[merged];\
[merged]fps=<Framerate>,setpts=PTS/<PlaybackSpeed>[speed];\
[speed]scale='trunc(iw/2)*2':'trunc(ih/2)*2'" \
       -c:v libvpx-vp9 -crf 30 -b:v 0 -deadline good -cpu-used 1 -auto-alt-ref 1 output.webm
```

| Parameter | Value | Source |
|-----------|-------|--------|
| `-c:v` | `libvpx-vp9` | Fixed |
| `-crf` | `30` | Fixed |
| `-b:v` | `0` (constant-quality VBR) | Fixed |
| `-deadline` | `good` | Fixed |
| `-cpu-used` | `1` | Fixed |
| `-auto-alt-ref` | `1` | Fixed |

**Transparency:** when `Set TransparentBackground true` is used, the filter chain appends `format=yuva420p` and the output
adds `-pix_fmt yuva420p`, preserving an alpha channel. (When padding is also enabled, the pad color becomes
`0x00000000`.)

## PNG (single frame)

A single still frame, composited the same way and capped with `-frames:v 1`:

```bash
ffmpeg -f concat -safe 0 -i frames-text.txt \
       -f concat -safe 0 -i frames-cursor.txt \
       -filter_complex "[0:v][1:v]overlay=0:0" \
       -frames:v 1 output.png
```

No codec/CRF arguments are set — FFmpeg picks the PNG encoder from the `.png` extension.

## Padding and Background

When `Set Padding` is `0` (the default), VCR# uses the simplified filter chains shown above. When padding is greater than
`0`, every video/GIF chain instead scales the composited frame to the content area (`Width − 2·Padding` by
`Height − 2·Padding`, preserving aspect ratio), pads back out to the full `Width × Height` centered, and fills the border
with the theme's background color (`fillborders`). For WebM with a transparent background, the pad color is transparent
(`0x00000000`) instead of the theme color.

## Settings That Affect Encoding

| Setting | Effect on FFmpeg |
|---------|------------------|
| `Framerate` | Output `fps` filter value (all animated formats) |
| `PlaybackSpeed` | `setpts=PTS/<value>` for GIF/MP4/WebM (SVG applies the same multiplier by scaling its frame timings, without FFmpeg) |
| `MaxColors` | `palettegen=max_colors` — **GIF only** |
| `Padding` | Switches to the scale/pad/fillborders filter chain; border filled with `Theme.Background` |
| `TransparentBackground` | **WebM only** — `format=yuva420p` + transparent pad |
| `Width` / `Height` | Canvas size; the scale/pad target when padding is used |

## Error Handling

Encoding failures are raised by FFMpegCore as exceptions. `VideoEncoder` catches them and rethrows as
`Failed to render <FORMAT> output: <message>`. VCR# does not branch on specific FFmpeg numeric exit codes. Per-encoder
FFmpeg stderr is currently suppressed (the encoders attach a no-op error handler), so detailed FFmpeg diagnostics are not
surfaced in the CLI output.

## Format Specifications

### GIF
- **Container**: GIF89a
- **Color depth**: 8-bit (palette of up to `MaxColors`, 1–256)
- **Transparency**: index-based
- **Animation**: loops indefinitely (container default)

### MP4
- **Container**: MPEG-4 Part 14
- **Video codec**: H.264 / AVC
- **Pixel format**: `yuv420p`
- **Audio**: none

### WebM
- **Container**: WebM (Matroska-based)
- **Video codec**: VP9
- **Pixel format**: `yuv420p`, or `yuva420p` with `TransparentBackground`
- **Audio**: none

### PNG
- **Single still frame** of the composited terminal

## External Documentation

- [FFmpeg Official Documentation](https://ffmpeg.org/documentation.html)
- [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore)
- [H.264 Encoding Guide](https://trac.ffmpeg.org/wiki/Encode/H.264)
- [VP9 Encoding Guide](https://trac.ffmpeg.org/wiki/Encode/VP9)
- [Palette Generation Filter](https://ffmpeg.org/ffmpeg-filters.html#palettegen)
