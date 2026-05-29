---
title: "How to Record a Session Interactively"
description: "Capture a real shell session as you type and turn it into a replayable tape file"
uid: "docs.how-to.record-interactively"
order: 2650
---

## Overview

The `record` command opens a live terminal, captures everything you type, and writes it out as a
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

A terminal window opens. **Type your commands in that window** — it is a real shell, so commands run
for real. When you are done, end the session by typing `exit` (or pressing `Ctrl+D`), or simply close
the window. VCR# then writes `demo.tape`.

If you omit the path, the tape is written to `recording.tape`.

## Replay the recording

The `record` command captures input only; it does not add an `Output` line. To render the tape, add
one and replay it:

```tape
Output demo.gif
Type "echo hi"
Enter
Sleep 1.2s
```

```bash
vcr demo.tape
```

You will usually want to open the generated tape and tidy it up — adjust `Sleep` durations, add an
`Output`, or remove a stray keystroke — before rendering. See the
[Complete Tape File Syntax Reference](../reference/tape-syntax).

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

The trailing `exit` (or `Ctrl+D`) you use to end the session is stripped automatically. For the full
list of key names, see [How to Use Keyboard Shortcuts and Special Keys](keyboard-input).

## Options

| Option | Description | Example |
|--------|-------------|---------|
| `[output-tape]` | Path to write the tape (default: `recording.tape`) | `vcr record demo.tape` |
| `--shell` | Shell to record in | `--shell bash` |
| `--theme` | Terminal theme applied to the window, and written to the tape | `--theme "Dracula"` |
| `--cols` | Terminal width, written as `Set Cols` | `--cols 80` |
| `--rows` | Terminal height, written as `Set Rows` | `--rows 24` |
| `--font-size` | Font size, written as `Set FontSize` | `--font-size 18` |
| `-v, --verbose` | Enable verbose logging | `-v` |

`--cols`, `--rows`, and `--font-size` configure the generated tape's `Set` header (so playback uses
them); the live recording window itself fits your screen and can be resized freely while you work.

## When to use each command

| Use case | Command |
|----------|---------|
| Author a tape by doing the demo | `record` |
| One-off screenshot of a command's result | [`snap`](quick-capture) |
| Animated SVG of a command running | [`capture`](quick-capture) |
| Hand-crafted demo with precise control | a tape file you write yourself |

## Troubleshooting

**Typing does nothing.** Click the terminal window once to give it focus, then type.

**The tape is empty.** Make sure you typed in the window that `record` opened — not your original
terminal. Keystrokes are captured only from the recording window.

**Replaying shows nothing.** `record` writes a tape with no `Output`. Add an `Output demo.gif`
line (and replay with `vcr demo.tape`) to render it.

**Cross-platform note.** Because `record` captures keystrokes (not shell output), it works the same in
PowerShell, cmd, bash, zsh, and fish. For why, see
[How VCR# Records Your Keystrokes](../explanation/interactive-recording).
