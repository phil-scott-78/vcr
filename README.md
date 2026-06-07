# VcrSharp

> **⚠️ WORK IN PROGRESS**
> This project is in early development and the API will change rapidly. If you need a stable, production-ready terminal
> recorder, please use [VHS](https://github.com/charmbracelet/vhs) instead. VcrSharp is experimental and should be
> considered alpha quality.

A .NET terminal recorder that turns `.tape` scripts into GIFs, videos, and SVGs. Write your terminal demos as code, then
render them to GIF, MP4, WebM, PNG, or (animated/static) SVG.

VCR# is **browserless**. It drives a real shell over an in-process pseudo-terminal (ConPTY on Windows, a Unix PTY on
Linux/macOS) and renders the output with its own VT500 terminal engine — **no ttyd, no Chromium, no Playwright**. SVG and
PNG output need no external binaries at all.

![VCR Install Demo](docs/VcrSharp.Docs/Content/vcr-install.svg)

## Installation

VCR# ships as a .NET global tool:

```bash
dotnet tool install --global vcr
```

> **Need the old behavior?** VCR# was rewritten substantially after `0.0.35`. If you depend on the pre-rewrite
> behavior and aren't ready to migrate, pin that version:
> ```bash
> dotnet tool install --global vcr --version 0.0.35
> ```

That's the whole install for SVG and PNG output. If you want to encode **GIF, MP4, or WebM**, also put
[FFmpeg](https://ffmpeg.org/) on your `PATH`:

| OS      | Install FFmpeg (video output only) |
|---------|------------------------------------|
| Windows | `choco install ffmpeg`             |
| macOS   | `brew install ffmpeg`              |
| Linux   | `sudo apt install ffmpeg`          |

**Requirements:** .NET 10. FFmpeg is required only for GIF/MP4/WebM. Raster output (GIF/MP4/WebM/PNG) also uses a
monospace font installed on your system — usually already present (Consolas/Cascadia on Windows, DejaVu Sans Mono on
Linux); SVG output needs none.

Verify it runs (`vcr themes` lists the built-in themes):

```bash
vcr themes
```

The full walkthrough lives in the [Getting Started tutorial](https://phil-scott-78.github.io/vcr/tutorials/getting-started.html).

## Quick Example

Create a file called `demo.tape`:

```tape
Output demo.svg

Set Cols 80
Set Rows 20
Set Theme "Dracula"

Type "echo 'Hello, VCR#!'"
Enter
Sleep 1s
```

Then record it (SVG output needs no extra tools):

```bash
vcr demo.tape
```

Change `Output demo.svg` to `demo.gif` to render a GIF instead (requires FFmpeg).

## CLI at a glance

| Command                 | What it does                                                                  |
|-------------------------|-------------------------------------------------------------------------------|
| `vcr <tape>`            | Record a tape file to its declared `Output` target(s), in-process (no browser). |
| `vcr validate <tape>`   | Parse a tape file and report syntax errors without recording.                 |
| `vcr themes`            | List the built-in themes with their background/foreground colors.             |
| `vcr snap <command>`    | One-shot static SVG screenshot of `<command>`'s final output — no tape file.  |
| `vcr capture <command>` | Animated SVG of `<command>` start to finish — no tape file.                   |
| `vcr record [tape]`     | Record a live shell session into a `.tape` file as you type — works in any shell. |
| `vcr migrate <dir>`     | Mine a directory of tapes into a shared `vcr.toml` preset (dry run by default). |

## Documentation

Full docs, tutorials, and reference: **https://phil-scott-78.github.io/vcr/**

- [Getting Started Tutorial](https://phil-scott-78.github.io/vcr/tutorials/getting-started.html)
- [Tape Syntax Reference](https://phil-scott-78.github.io/vcr/reference/tape-syntax.html)
- [Configuration Options](https://phil-scott-78.github.io/vcr/reference/configuration-options.html)
- Sample tape files in [`samples/`](samples/)

## Attribution

This project is heavily inspired by [VHS](https://github.com/charmbracelet/vhs) by [Charm Bracelet](https://charm.sh/).
VcrSharp adds first-class Windows support, a fully browserless rendering path (its own VT500 terminal engine, no ttyd or
Chromium), the ability to execute real commands via the `Exec` command, and an interactive `record` mode that works
across shells (VHS's is bash-only) — but VHS is still much more feature rich.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
