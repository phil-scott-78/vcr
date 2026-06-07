---
title: "CLI Command Reference"
description: "Factual reference for every vcr command-line verb, its arguments, and its options."
uid: "docs.reference.cli-commands"
order: 4050
---

## Overview

The command-line application is `vcr`. It is a single .NET tool with one default command (no verb) and six named verbs. This page lists every command, its positional arguments, and its options.

| Command | Purpose |
| --- | --- |
| `vcr <tape-file>` | Record/play a tape to its `Output` target(s). |
| `vcr validate <tape-file>` | Parse a tape, resolve presets, and report command counts without recording. |
| `vcr themes` | List the built-in themes. |
| `vcr migrate <directory>` | Factor shared settings out of legacy tapes into a `vcr.toml` preset. |
| `vcr snap <command>` | Run a command and write a static SVG of its final output. |
| `vcr capture <command>` | Run a command and write an animated SVG of its full output. |
| `vcr record [output-tape]` | Interactively record keystrokes and author a `.tape` file. |

> [!NOTE]
> Only `.gif`, `.mp4`, and `.webm` outputs require FFmpeg on `PATH`. SVG, PNG, and frame-directory outputs need no external binaries, so SVG is the lightest-weight format for first runs.

## `vcr <tape-file>` (default)

Record or play a tape to the output target(s) declared by its `Output` lines.

**Positional argument**

- `<tape-file>` — path to the `.tape` file. A missing file exits with code 1 (`Error: Tape file not found: …`).

**Supported output targets** (chosen by the `Output` extension):

- `.svg` — animated, text-based SVG (no FFmpeg).
- `.gif`, `.mp4`, `.webm` — animated raster formats (require FFmpeg).
- `.png` — single-frame PNG (no FFmpeg).
- An extension-less path — a directory of PNG frames plus a manifest (no FFmpeg).

**Options**

| Option | Short | Value | Default | Meaning |
| --- | --- | --- | --- | --- |
| `--verbose` | `-v` | flag | off | Print detailed progress and diagnostics. |
| `--set` | | `KEY=VALUE` | none | Override a matching `Set` in the tape (key is case-insensitive). Repeatable. |
| `--output` | `-o` | `FILE` | none | Append an extra output file in addition to the tape's `Output` lines. Repeatable. |

See <xref:docs.how-to.cli-overrides> for using `--set` and `-o` to vary a recording without editing the tape.

## `vcr validate <tape-file>`

Parse a tape and resolve its presets, then print the resolved command counts. Nothing is recorded.

**Positional argument**

- `<tape-file>` — path to the `.tape` file.

This command takes no options. For the grammar it validates against, see <xref:docs.reference.tape-syntax>.

## `vcr themes`

List the built-in themes, printing each theme's name, background color, and foreground color.

This command takes no positional arguments and no options.

## `vcr migrate <directory>`

Mine a directory of legacy tapes into a shared `vcr.toml` preset and rewrite each tape to `Use` that preset. **Runs as a dry run by default** — pass `--write` to apply the changes.

**Positional argument**

- `<directory>` — directory containing the tapes to migrate.

**Options**

| Option | Short | Value | Default | Meaning |
| --- | --- | --- | --- | --- |
| `--write` | | flag | off (dry run) | Apply the migration instead of previewing it. |
| `--preset` | | `NAME` | `doc` | Name of the generated preset. |
| `--threshold` | | `FRACTION` | `0.6` | Fraction of tapes that must share a setting before it is hoisted into the preset. |
| `--recursive` | `-r` | flag | off | Recurse into subdirectories. |

See <xref:docs.how-to.presets> for working with the `vcr.toml` presets this command generates.

## `vcr snap <command>`

Run `<command>` and write a **static SVG** screenshot of its final output. Output is always SVG (the format is forced to `.svg`) and is cropped to its content.

**Positional argument**

- `<command>` — the shell command to run and capture.

**Options**

| Option | Short | Value | Default | Meaning |
| --- | --- | --- | --- | --- |
| `--output` | `-o` | `FILE` | `output.svg` | Output file path. |
| `--theme` | | theme name | session default | Color theme to render with. |
| `--cols` | | int | auto | Terminal column count. |
| `--rows` | | int | auto | Terminal row count. |
| `--font-size` | | int (px) | session default | Font size used for measurement and rendering. |
| `--disable-cursor` | | flag | off | Hide the cursor in the capture. |
| `--transparent-background` | | flag | off | Render with a transparent background. |
| `--end-buffer` | | duration | session default | Hold time after the command's final output. |
| `--verbose` | `-v` | flag | off | Print detailed progress and diagnostics. |

## `vcr capture <command>`

Run `<command>` and write an **animated SVG** of its full output. Output is always SVG.

**Positional argument**

- `<command>` — the shell command to run and capture.

**Options** — identical to `vcr snap`:

| Option | Short | Value | Default | Meaning |
| --- | --- | --- | --- | --- |
| `--output` | `-o` | `FILE` | `output.svg` | Output file path. |
| `--theme` | | theme name | session default | Color theme to render with. |
| `--cols` | | int | auto | Terminal column count. |
| `--rows` | | int | auto | Terminal row count. |
| `--font-size` | | int (px) | session default | Font size used for measurement and rendering. |
| `--disable-cursor` | | flag | off | Hide the cursor in the capture. |
| `--transparent-background` | | flag | off | Render with a transparent background. |
| `--end-buffer` | | duration | session default | Hold time after the command's final output. |
| `--verbose` | `-v` | flag | off | Print detailed progress and diagnostics. |

> [!NOTE]
> `snap` and `capture` are **SVG-only**. To produce GIF, MP4, WebM, PNG, or frame-directory output, use a tape with the default command.

See <xref:docs.how-to.quick-capture> for using `snap` and `capture` without authoring a tape.

## `vcr record [output-tape]`

Interactively record keystrokes in a real shell and author a `.tape` file. Finish the session with `exit` or Ctrl+D. This command produces a tape file only — it does not encode any video or SVG.

**Positional argument**

- `[output-tape]` — path of the tape to write. Optional; defaults to `recording.tape`.

**Options**

| Option | Short | Value | Default | Meaning |
| --- | --- | --- | --- | --- |
| `--shell` | | `pwsh`, `powershell`, `cmd`, `bash`, `zsh`, `fish` | platform shell | Shell to launch for the session. |
| `--theme` | | theme name | session default | Color theme used for the interactive session. |
| `--cols` | | int | auto | Terminal column count. |
| `--rows` | | int | auto | Terminal row count. |
| `--font-size` | | int (px) | session default | Font size used for the interactive session. |
| `--verbose` | `-v` | flag | off | Print detailed progress and diagnostics. |

See <xref:docs.how-to.record-interactively> for a walkthrough of authoring a tape from a live session.
