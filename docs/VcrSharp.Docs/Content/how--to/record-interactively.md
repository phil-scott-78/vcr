---
title: "How to Record a Session Interactively"
description: "Capture a real shell session as you type and turn it into a replayable tape file"
uid: "docs.how-to.record-interactively"
order: 2650
---

## Overview

The `record` command launches a real shell, captures everything you type, and writes it out as a
`.tape` file. Instead of hand-writing `Type`, `Enter`, and `Sleep` commands, you just *do* the demo
and let VCR# transcribe it.

This is ideal for:

- Drafting a tape quickly, then refining it by hand
- Capturing a workflow you already know how to perform
- Demos that are easier to perform than to script

Unlike `snap` and `capture`, `record` does not produce a GIF or SVG directly — it produces a tape file
you replay (or edit) afterward.

## Record a session

Run `record` with the path you want the tape written to:

```bash
vcr record demo.tape
```

VCR# launches a shell over an in-process pseudo-terminal and puts your console into raw pass-through
mode, so **the shell takes over your current terminal** — you see the live session and type into it
directly. Commands run for real. When you are done, end the session by typing `exit` (or pressing
`Ctrl+D`). VCR# then restores your console and writes `demo.tape`.

If you omit the path, the tape is written to `recording.tape`.

> [!NOTE]
> `record` captures your keystrokes (the input byte-stream), not the shell's output. That is why it
> works identically in every shell. For the full story, see
> <xref:docs.explanation.interactive-recording>.

## Replay the recording

The `record` command captures input only; it does not add an `Output` line. To render the tape, add
one and replay it. SVG output needs no extra tooling, so it is the easiest target to start with:

```tape
Output demo.svg
Type "echo hi"
Enter
Sleep 1.2s
```

```bash
vcr demo.tape
```

You will usually want to open the generated tape and tidy it up — adjust `Sleep` durations, add an
`Output`, or remove a stray keystroke — before rendering. See <xref:docs.reference.tape-syntax>.

## Choose a shell

By default `record` uses your platform shell (PowerShell on Windows, bash on Unix). Record in a
different shell with `--shell`:

```bash
vcr record demo.tape --shell bash
```

Supported shells: `pwsh`, `powershell`, `cmd`, `bash`, `zsh`, `fish`. When the shell differs from your
platform default, a `Set Shell "..."` line is added to the tape so playback uses the same shell.

## What gets captured

Your keystrokes are transcribed into tape commands:

| You type | Tape command |
|----------|--------------|
| Printable text | `Type "..."` (consecutive characters are merged) |
| Enter | `Enter` |
| Arrows, Home/End, PageUp/PageDown, Delete, … | `Up`, `Down`, `Home`, … (repeats grouped, e.g. `Up 3`) |
| `Ctrl+C`, `Alt+b`, `Shift+Tab`, … | `Ctrl+C`, `Alt+b`, `Shift+Tab` |
| A pause before your next keystroke | `Sleep <duration>` (based on how long you actually paused) |

Multi-byte keystrokes (such as an arrow key, which arrives as `ESC [ A`) reach VCR# in a single read,
so escape sequences stay intact and map to the right key. The trailing `exit` (or `Ctrl+D`) you use to
end the session is stripped automatically. For the full list of key names, see
<xref:docs.how-to.keyboard-input>.

## Options

| Option | Description | Example |
|--------|-------------|---------|
| `[output-tape]` | Path to write the tape (default: `recording.tape`) | `vcr record demo.tape` |
| `--shell` | Shell to record in | `--shell bash` |
| `--theme` | Terminal theme written to the tape | `--theme "Dracula"` |
| `--cols` | Terminal width, written as `Set Cols` | `--cols 80` |
| `--rows` | Terminal height, written as `Set Rows` | `--rows 24` |
| `--font-size` | Font size, written as `Set FontSize` | `--font-size 18` |
| `-v, --verbose` | Enable verbose logging | `-v` |

`--cols`, `--rows`, `--font-size`, and `--theme` configure the generated tape's `Set` header so that
playback reproduces the same dimensions and styling. For the full CLI surface, see
<xref:docs.reference.cli-commands>.

## When to use each command

| Use case | Command |
|----------|---------|
| Author a tape by doing the demo | `record` |
| One-off screenshot of a command's result | [`snap`](xref:docs.how-to.quick-capture) |
| Animated SVG of a command running | [`capture`](xref:docs.how-to.quick-capture) |
| Hand-crafted demo with precise control | a tape file you write yourself |

## Troubleshooting

**The tape is empty.** Make sure you actually typed something before ending the session. Keystrokes are
captured from the moment the shell starts until you exit.

**Replaying shows nothing.** `record` writes a tape with no `Output`. Add an `Output demo.svg` line
(and replay with `vcr demo.tape`) to render it.

**Cross-platform note.** Because `record` captures keystrokes (not shell output), it works the same in
PowerShell, cmd, bash, zsh, and fish. For why, see <xref:docs.explanation.interactive-recording>.
