---
title: "Getting Started"
description: "Record your first terminal recording with VCR# — no browser, no extra tools."
uid: "docs.tutorials.getting-started"
order: 1000
---

In this tutorial we'll create our first terminal recording with VCR#. We'll install the tool, write a short tape
file, and run it to produce an animated SVG of a shell command. It takes about five minutes, and every step succeeds.

By the end we'll have an SVG showing `Hello, VCR#!` appearing in a terminal.

> [!NOTE]
> This first recording needs **no browser and no extra tools**. VCR# runs the shell in-process and renders SVG
> directly, so all we need is the .NET runtime. FFmpeg comes later, only if we want a GIF or video.

## Install VCR#

VCR# ships as a .NET global tool. We need the **.NET 10** runtime installed, then:

```bash
dotnet tool install --global vcr
```

That's everything we need for SVG output. (FFmpeg is only required later for GIF, MP4, or WebM — see the final
step.)

Let's verify the install by listing the built-in themes:

```bash
vcr themes
```

We should see a table of theme names with their background and foreground colors, including `Dracula`, which we'll
use next. If `vcr` isn't found, confirm the .NET tools directory is on our `PATH` and try a new shell.

## Write the tape

A *tape* is a small script that tells VCR# what to type and record. Create a file named `hello.tape` with this
content:

```tape
Output hello.svg

Set Cols 80
Set Rows 20
Set Theme "Dracula"

Type "echo 'Hello, VCR#!'"
Enter
Sleep 1s
```

Each line, briefly:

- `Output hello.svg` — the file VCR# will write (SVG, so no FFmpeg needed).
- `Set Cols 80` / `Set Rows 20` — the terminal size: 80 columns by 20 rows.
- `Set Theme "Dracula"` — the color scheme.
- `Type "echo 'Hello, VCR#!'"` — types the command, character by character.
- `Enter` — presses Enter to run it.
- `Sleep 1s` — pauses one second so the output stays on screen.

## Run it

Now run VCR# against the tape:

```bash
vcr hello.tape
```

VCR# launches a real shell in-process, types our commands, records the output, and writes `hello.svg`. This
finishes quickly — there's no browser to start and nothing to encode for SVG.

## View the result

Open `hello.svg` in any browser or image viewer. We'll see the command typed out and its result appear, like this:

<VcrTape src="../demos/hello-world.svg" />

The recording plays back in three beats: an empty terminal, the command typed character by character, and then the
`Hello, VCR#!` output. That character-by-character typing is what gives the recording its live, terminal-session
feel.

## Next: render a GIF

To produce a GIF instead, change one line in `hello.tape`:

```tape
Output hello.gif
```

Then run `vcr hello.tape` again to write `hello.gif`.

> [!NOTE]
> GIF, MP4, and WebM output is encoded with **FFmpeg**, so it must be installed and on our `PATH` for those
> formats. SVG and PNG need nothing extra.

## Where to next

- <xref:docs.tutorials.typing-demo> — build a richer demo with navigation and editing.
- <xref:docs.tutorials.exec-commands> — record the output of a real command with `Exec`.
- <xref:docs.reference.tape-syntax> — the full list of tape commands.
- <xref:docs.how-to.quick-capture> — record a one-off command without writing a tape.
