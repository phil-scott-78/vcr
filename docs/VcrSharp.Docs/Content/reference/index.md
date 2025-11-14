---
title: "Reference"
description: "Complete technical reference for VCR# commands, settings, and options"
uid: "docs.reference.index"
order: 4000
---

## Overview

Comprehensive technical documentation for VCR# tape file syntax, configuration options, and external tool specifications.

## Available References

### [Tape File Syntax](tape-syntax.md)

Complete syntax specification for `.tape` files.

**Covers:**
- Output and configuration commands
- Input commands (Type, Enter, arrow keys, etc.)
- Navigation and modifier keys
- Control flow (Sleep, Wait, Hide/Show)
- Execution commands (Exec)
- Utility and environment commands
- String quoting and escape sequences
- Duration literals and regex patterns

### [Configuration Options](configuration-options.md)

All VCR# settings configurable via `Set` command or `--set` CLI flag.

**Covers:**
- Terminal settings (dimensions, fonts)
- Video settings (framerate, playback speed)
- Styling settings (themes, padding, margins)
- Behavior & timing settings (shell, typing speed, wait timeouts)

### [FFmpeg Options](ffmpeg-options.md)

FFmpeg encoding parameters for GIF, MP4, and WebM output generation.

**Covers:**
- GIF palette generation and encoding
- MP4 H.264 encoding
- WebM VP9 encoding
- Format specifications

### [ttyd Configuration](ttyd-options.md)

ttyd command-line options and process specifications.

**Covers:**
- Command-line options
- Process lifecycle
- Port binding and shell selection
- Platform defaults

## Quick Lookup

- [All available commands](tape-syntax.md#input-commands)
- [Built-in themes](configuration-options.md#theme)
- [Duration format syntax](tape-syntax.md#duration-literals)
- [Wait command scopes](tape-syntax.md#wait)
- [Setting precedence rules](configuration-options.md#setting-precedence)
