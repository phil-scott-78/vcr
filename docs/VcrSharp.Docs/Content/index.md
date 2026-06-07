---
title: "Welcome"
description: "A browserless .NET terminal recorder that turns .tape files into SVGs, GIFs, and videos"
uid: "docs.index"
order: 0
---

A .NET terminal recorder that turns `.tape` files into SVGs, GIFs, and videos. Write your terminal demos as code, then render them to a recording.

<VcrTape src="vcr-install.svg" />

VCR# is **browserless** — it runs your shell over an in-process pseudo-terminal and renders frames with a from-scratch VT500 engine, so there is no ttyd, Chromium, or other runtime to install. SVG and PNG output need no external tools; only GIF, MP4, and WebM require [FFmpeg](xref:docs.reference.ffmpeg-options) on your PATH.

## Installation

Install VCR# as a global .NET tool:

```bash
dotnet tool install --global vcr
```

> [!NOTE]
> VCR# was rewritten substantially after `0.0.35`. If you depend on the pre-rewrite behavior and aren't ready to migrate, pin that version with `dotnet tool install --global vcr --version 0.0.35`.

## What you can do

- **Record tapes** — script a demo in a `.tape` file and render it to SVG, GIF, MP4, WebM, PNG, or a frame directory.
- **Snap or capture a one-off** — point `vcr snap` / `vcr capture` at any command for a quick static or animated SVG, no tape required.
- **Record interactively** — drive a real shell and let `vcr record` author the `.tape` for you from your keystrokes.
- **Share settings via presets** — hoist common configuration into a `vcr.toml` and pull it into tapes with `Use`.

## Getting Started

Ready to create your first terminal recording? Start here:

- **[Getting Started Tutorial](xref:docs.tutorials.getting-started)** — create your first recording in five minutes.
- **[Tape syntax reference](xref:docs.reference.tape-syntax)** — every command you can put in a `.tape` file.
- **[CLI commands reference](xref:docs.reference.cli-commands)** — the full set of `vcr` verbs and options.
