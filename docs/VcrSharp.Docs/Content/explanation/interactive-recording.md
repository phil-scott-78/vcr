---
title: "How VCR# Records Your Keystrokes"
description: "Why interactive recording captures input the same way in every shell, and how keystrokes become tape commands"
uid: "docs.explanation.interactive-recording"
order: 3200
---

## Recording is playback in reverse

Most of VCR# sends input *into* a terminal: a tape's `Type` and `Key` commands travel through
Playwright, xterm.js, ttyd, and the PTY to reach the shell (see
[How VCR# Talks to ttyd](ttyd-interaction)). Interactive recording runs that pipeline backwards. Rather
than VCR# feeding keystrokes to the terminal, *you* type into the terminal and VCR# writes down what
you did.

The result is a `.tape` file you can replay, edit, and render — authored by performing the demo instead
of scripting it. The [how-to guide](../how--to/record-interactively) covers the workflow; this page
explains how the capture works and why it behaves the way it does.

## Listening at the input layer

When you run `record`, VCR# starts ttyd and opens a **headed** browser window — the terminal you type
into is the same xterm.js terminal VCR# normally automates, just visible and driven by your real
keyboard. Before handing the window to you, VCR# attaches a listener to xterm.js's `onData` event.

`onData` fires with the exact bytes the terminal would send to the shell for each keystroke: `a` for the
letter A, `\r` for Enter, `ESC[A` for the Up arrow, `0x03` for `Ctrl+C`. Each event is recorded with a
high-resolution timestamp. That stream of `(bytes, time)` pairs is the entire raw material of a
recording — VCR# never reads what the shell *printed back*, only what you *sent*.

## Why it works in every shell

This is the key design choice, and it is why `record` is not tied to one shell.

Keystrokes are universal. The byte a terminal sends when you press `Ctrl+C` is `0x03` whether the shell
behind it is PowerShell, cmd, bash, zsh, or fish. Shell *output* — prompts, colors, autosuggestions,
history redraws — differs wildly between shells and is genuinely hard to parse. By capturing the input
layer rather than the output layer, VCR# sidesteps all of that: the same capture-and-translate logic
produces correct tapes everywhere.

VHS, the project that inspired VCR#, records its sessions through a Unix pseudo-terminal with
cleanup tuned for bash, so its `record` command effectively only works there. VCR# already delegates
cross-platform terminal handling to ttyd, and reading `onData` is shell-agnostic by nature, so
interactive recording works the same on Windows PowerShell as it does on Linux bash.

## From keystrokes to tape commands

Once you end the session, VCR# translates the captured stream into tape commands:

- Runs of printable characters become a single `Type "..."`.
- Recognized control bytes and escape sequences become the matching `Key` or modifier command —
  `Enter`, `Up`, `Ctrl+C`, `Shift+Tab`, and so on. Identical consecutive keys are grouped (`Up 3`).
- Gaps between keystrokes become `Sleep` commands.
- The `exit` (or `Ctrl+D`) you used to end the session is recognized and dropped.

The exact mapping is part of the [tape syntax](../reference/tape-syntax); the
[how-to guide](../how--to/record-interactively) lists what each kind of keystroke produces.

### Timing comes from real pauses

Because every captured keystroke carries a timestamp, the `Sleep` commands reflect how long you actually
paused — a two-second pause to read output becomes `Sleep 2s`, a quick double-tap of the arrow key stays
gapless. This is more faithful than inserting a fixed delay between keystrokes, and it is why a recorded
tape replays at roughly the pace you performed it. Very long idle stretches are capped so an
accidental coffee break does not bake a minute-long pause into the tape.

## Why the window behaves like a normal terminal

Interactive recording deliberately does *not* lock the window to a fixed size or apply the recording
font. The window exists only for you to type into — it has no effect on the captured tape, since the
tape is built from keystrokes, not from what the window rendered. Letting ttyd size and fit the terminal
natively means the bottom line always renders fully and you can resize the window freely while you work.

Your `--cols`, `--rows`, and `--font-size` choices are still written into the generated tape's `Set`
header, so the *rendered* recording uses them later — they configure playback, not the live window.

## What this means in practice

- `record` produces a tape file, not a video. Add an `Output` line and replay with `vcr <tape>` to
  render it.
- Recording is interactive by nature: it opens a real window and waits for you, so it is not something
  to run unattended in CI.
- A recorded tape is a starting point. Because it captures exactly what you did — including typos and
  pauses — you will usually edit it afterward, which is exactly what tape files are good at.
