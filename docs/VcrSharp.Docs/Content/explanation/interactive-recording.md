---
title: "How VCR# Records Your Keystrokes"
description: "Why interactive recording captures input the same way in every shell, and how keystrokes become tape commands"
uid: "docs.explanation.interactive-recording"
order: 3200
---

## Recording is playback in reverse

Most of VCR# sends input *into* a terminal: a tape's `Type` and `Key` commands travel over a
pseudo-terminal to reach a real shell, whose output is parsed back into a cell grid and rendered (see
<xref:docs.explanation.browserless-engine>). Interactive recording runs that pipeline backwards. Rather
than VCR# feeding keystrokes to the terminal, *you* type into the terminal and VCR# writes down what you
did.

The result is a `.tape` file you can replay, edit, and render — authored by performing the demo instead
of scripting it. The [how-to guide](xref:docs.how-to.record-interactively) covers the workflow; this page
explains how the capture works and why it behaves the way it does.

## Listening at the input layer

When you run `vcr record`, the `InteractiveRecorder` launches your shell over an in-process
pseudo-terminal — the same kind of PTY the playback engine uses, just driven by your keyboard instead of
a tape. It then puts the **host console** into raw, VT pass-through mode (console-mode flags on Windows;
`stty raw -echo < /dev/tty` on Unix) so that every byte you type goes straight through to the shell
without the console swallowing, echoing, or line-buffering it.

From there, two pump threads run side by side:

- **Output pump** — reads the shell's output from the PTY and writes it to your real stdout. This is what
  makes the live session feel completely normal: you see the prompt, your typing echoed back, colors,
  autosuggestions, TUI redraws — everything, in real time, because it is the actual shell running in front
  of you.
- **Input pump** — reads your keystrokes from stdin and forwards them to the PTY so the shell receives
  them. As it does, it records each read chunk as a timestamped `InputEvent`.

That stream of `(bytes, time)` events is the entire raw material of a recording. VCR# never inspects what
the shell *printed back* — only what you *sent*.

## Why multi-byte keystrokes stay intact

A single keypress is not always a single byte. The Up arrow sends `ESC [ A`, `Alt+x` sends `ESC x`,
`Ctrl+C` sends the single byte `0x03`. Because the input pump records **one read chunk per keystroke**, a
multi-byte sequence like an arrow key or an `Alt`-chord arrives whole, in one event. The escape sequence
is never split across two recordings, so the converter that runs afterward always sees a complete, valid
sequence to interpret. This is the difference between recording a clean `Up` and recording three confused
fragments.

## Why it works in every shell

This is the key design choice, and it is why `vcr record` is not tied to one shell.

Keystrokes are universal. The byte a terminal sends when you press `Ctrl+C` is `0x03` whether the shell
behind it is PowerShell, cmd, bash, zsh, or fish. Shell *output* — prompts, colors, autosuggestions,
history redraws — differs wildly between shells and is genuinely hard to parse. By capturing the input
layer rather than the output layer, VCR# sidesteps all of that: the same capture-and-translate logic
produces correct tapes everywhere.

VHS, the project that inspired VCR#, records its sessions through a Unix pseudo-terminal with cleanup
tuned for bash, so its `record` command effectively only works there. Because VCR# records the input
byte-stream — which is identical across shells — interactive recording works the same on Windows
PowerShell as it does on Linux bash, with nothing shell-specific in the capture path.

## From keystrokes to tape commands

Once you end the session (with `exit` or `Ctrl+D`), `InputToTapeConverter` translates the captured stream
into tape commands:

- Runs of printable characters become a single `Type "..."`.
- Recognized control bytes and escape sequences become the matching `Key` or modifier command —
  `Enter`, `Up`, `Ctrl+C`, `Shift+Tab`, and so on. Identical consecutive keys are grouped (`Up 3`).
- Gaps between keystrokes become `Sleep` commands.
- The `exit` (or `Ctrl+D`) you used to end the session is recognized and dropped.

The converter is a pure function over the `InputEvent` stream, with no knowledge of which shell produced
the keystrokes — which is what keeps the whole feature shell-agnostic.

<VcrTape src="../demos/typing-edits.svg" />

### Timing comes from real pauses

Because every captured keystroke carries a timestamp, the `Sleep` commands reflect how long you actually
paused — a two-second pause to read output becomes `Sleep 2s`, a quick double-tap of the arrow key stays
gapless. This is more faithful than inserting a fixed delay between keystrokes, and it is why a recorded
tape replays at roughly the pace you performed it.

### Modifiers and special keys

Modifier chords come straight from their byte forms: `Ctrl+C` from `0x03`, `Shift+Tab` from `ESC [ Z`,
`Alt+Enter` from an `ESC`-prefixed sequence. The converter maps these to the same chord syntax you would
write by hand, so a recorded tape reads like one you authored.

<VcrTape src="../demos/keyboard-modifiers.svg" />

## What this means in practice

- `vcr record` produces a tape file, not a video. Add an `Output` line and replay with `vcr <tape>` to
  render it.
- Recording is interactive by nature: it runs a live shell and waits for you, so it is not something to
  run unattended in CI. For non-interactive capture of a single command, reach for `vcr snap` or
  `vcr capture` instead.
- Your `--cols`, `--rows`, `--font-size`, and `--theme` choices are written into the generated tape's
  `Set` header, so the *rendered* recording uses them later — they configure playback, not the live
  session.
- A recorded tape is a starting point. Because it captures exactly what you did — including typos and
  pauses — you will usually edit it afterward, which is exactly what tape files are good at.
