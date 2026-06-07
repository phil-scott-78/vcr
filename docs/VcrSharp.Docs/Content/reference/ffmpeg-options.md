---
title: "How VCR# Uses FFmpeg"
description: "When and how VCR# drives FFmpeg (via FFMpegCore) to encode GIF, MP4, and WebM output"
uid: "docs.reference.ffmpeg-options"
order: 4400
---

## Overview

VCR# renders every frame **in-process**. The from-scratch VT500 engine parses the shell's output into a cell grid,
and `RasterRenderer` (built on [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) and SixLabors.Fonts,
running entirely on the CPU) draws each grid snapshot directly to a bitmap. There is no browser, no screenshot capture,
and no compositing of separate text and cursor layers — each frame is rasterized once, fully composited.

FFmpeg only enters the picture when an `Output` target is an **animated raster video**. `VideoWriter` drives
FFmpeg through the [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) .NET wrapper, which shells out to the
`ffmpeg` binary on your `PATH`. FFMpegCore builds the argument list; VCR# does not assemble hand-written command lines.

> [!IMPORTANT]
> **FFmpeg is required only for `.gif`, `.mp4`, and `.webm` output.** SVG, PNG, and the frame-directory output need
> no external binaries at all. If you only ever produce `.svg` (the recommended no-setup format), you do not need
> FFmpeg installed.

## What uses FFmpeg, and what doesn't

| Output | Produced by | Needs FFmpeg? |
|--------|-------------|---------------|
| `.svg` | `SvgRenderer` (text/XML, no font required) | No |
| `.png` | `RasterRenderer` → saved by ImageSharp | No |
| directory (extension-less path) | `RasterRenderer` → PNG sequence + a `frames.txt` concat manifest | No |
| `.gif` | `RasterRenderer` frames → FFmpeg (`palettegen`/`paletteuse`) | **Yes** |
| `.mp4` | `RasterRenderer` frames → FFmpeg (`libx264`) | **Yes** |
| `.webm` | `RasterRenderer` frames → FFmpeg (`libvpx-vp9`) | **Yes** |

For the GIF/MP4/WebM formats, VCR# renders the composited PNG frames in-process, then hands them to FFmpeg only for the
final encode. The frame directory itself uses the same PNG sequence plus a `frames.txt`
[concat-demuxer](https://ffmpeg.org/ffmpeg-formats.html#concat-1) manifest, but it performs **no encode** — the files
are written and left as-is.

> [!NOTE]
> A monospace font must be installed for raster output (GIF/MP4/WebM/PNG), since ImageSharp draws real glyphs. SVG
> needs no font: it emits text directly and draws box, block, and powerline glyphs as vector paths.

## GIF

GIF encoding uses FFmpeg's `palettegen`/`paletteuse` filters to build and apply a color palette in a single pass.
The palette size honors `MaxColors`, and the loop behavior honors `Loop`/`LoopCount`.

| Parameter | Value | Source |
|-----------|-------|--------|
| palette size | `MaxColors` (default 256, range 1–256) | `Set MaxColors` |
| looping | loop forever (default), play once, or `N` times | `Set Loop` / `Set LoopCount` |
| frame rate | `Framerate` (default 50) | `Set Framerate` |
| playback speed | `PlaybackSpeed` (default 1.0) | `Set PlaybackSpeed` |

```tape
Output demo.gif
Set MaxColors 128
Set Loop false        # play once and hold on the last frame
Type "echo hello"
Enter
```

## MP4

MP4 encoding uses the **`libx264`** H.264 encoder with the `yuv420p` pixel format for broad player and browser
compatibility. MP4 has no alpha channel, so `TransparentBackground` has no effect on `.mp4` output.

| Parameter | Value | Source |
|-----------|-------|--------|
| video codec | `libx264` (H.264) | Fixed |
| pixel format | `yuv420p` | Fixed |
| frame rate | `Framerate` | `Set Framerate` |
| playback speed | `PlaybackSpeed` | `Set PlaybackSpeed` |

## WebM

WebM encoding uses the **`libvpx-vp9`** VP9 encoder. When `Set TransparentBackground true` is set, VCR# uses the
`yuva420p` pixel format, preserving an alpha channel so the recording can sit over any page background.

| Parameter | Value | Source |
|-----------|-------|--------|
| video codec | `libvpx-vp9` (VP9) | Fixed |
| pixel format | `yuva420p` (alpha) when `TransparentBackground`, else `yuv420p` | `Set TransparentBackground` |
| frame rate | `Framerate` | `Set Framerate` |
| playback speed | `PlaybackSpeed` | `Set PlaybackSpeed` |

```tape
Output demo.webm
Set TransparentBackground true
Type "echo transparent"
Enter
```

## Settings that affect encoding

The renderer fixes most encoding parameters in code, but a handful of tape settings change the frames VCR# produces or
the FFmpeg pipeline it drives:

| Setting | Effect |
|---------|--------|
| `Framerate` | Output frame rate for all animated formats (GIF/MP4/WebM). Default 50, range 1–120. |
| `PlaybackSpeed` | Speeds up or slows down playback. Applied to GIF/MP4/WebM during encode; SVG applies the same multiplier to its frame timings without FFmpeg. |
| `MaxColors` | GIF palette size (`palettegen`). **GIF only.** Default 256, range 1–256. |
| `Loop` / `LoopCount` | GIF loop behavior — loop forever (default), play once (`Loop false`), or `N` times (`LoopCount N`). **GIF only.** |
| `TransparentBackground` | **WebM only** — switches the encode to the `yuva420p` alpha pixel format. (PNG and SVG handle transparency natively without FFmpeg.) |
| `Padding` | Adds blank space around the terminal in the rendered frame before encoding. |

For the full list of settings and their defaults, see the
[configuration options reference](xref:docs.reference.configuration-options).

## Error handling

FFmpeg encoding failures surface as exceptions from FFMpegCore, which VCR# rethrows with a
`Failed to render <FORMAT> output: <message>` prefix. VCR# does not branch on specific FFmpeg exit codes. If `.gif`,
`.mp4`, or `.webm` output fails with an FFmpeg error, confirm that `ffmpeg` is on your `PATH` and runnable.

> [!TIP]
> If you don't have FFmpeg installed and don't want to, produce `.svg` instead. SVG output is animated, scalable,
> searchable, and needs no external binaries or fonts. See
> [how to produce multiple output formats](xref:docs.how-to.multiple-formats) for choosing between formats.

## External documentation

- [FFmpeg official documentation](https://ffmpeg.org/documentation.html)
- [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore)
- [H.264 encoding guide](https://trac.ffmpeg.org/wiki/Encode/H.264)
- [VP9 encoding guide](https://trac.ffmpeg.org/wiki/Encode/VP9)
- [Palette generation filter](https://ffmpeg.org/ffmpeg-filters.html#palettegen)
