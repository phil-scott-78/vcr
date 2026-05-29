# VcrSharp

> **⚠️ WORK IN PROGRESS**
> This project is in early development and the API will change rapidly. If you need a stable, production-ready terminal
> recorder, please use [VHS](https://github.com/charmbracelet/vhs) instead. VcrSharp is experimental and should be
> considered alpha quality.

A .NET terminal recorder that turns `.tape` files into GIFs and videos. Write your terminal demos as code, then render
them to video.

![VCR Install Demo](docs/VcrSharp.Docs/Content/vcr-install.svg)

## Installation

VCR# itself ships as a .NET global tool, but it drives a real terminal under the hood, so two extra binaries need to be
on your `PATH`:

- **[ttyd](https://github.com/tsl0922/ttyd)** (>= 1.7.2) — the terminal server VCR# scripts against.
- **[FFmpeg](https://ffmpeg.org/)** — used to encode GIF / MP4 / WebM output.

| OS      | Install line                                |
|---------|---------------------------------------------|
| Windows | `choco install ttyd ffmpeg`                 |
| macOS   | `brew install ttyd ffmpeg`                  |
| Linux   | `sudo apt install ttyd ffmpeg`              |

Then install VCR# itself:

```bash
dotnet tool install --global vcr
```

Verify everything is wired up (`vcr themes` lists the built-in themes, confirming VCR# runs):

```bash
vcr themes
ttyd --version
ffmpeg -version
```

The full walkthrough lives in the [Getting Started tutorial](https://phil-scott-78.github.io/vcr/tutorials/getting-started.html).

## Quick Example

Create a file called `demo.tape`:

```tape
Output demo.gif

Set Cols 80
Set Rows 20
Set Theme "Dracula"

Type "echo 'Hello, VCR#!'"
Enter
Sleep 1s
```

Then record it:

```bash
vcr demo.tape
```

## CLI at a glance

| Command                | What it does                                                   |
|------------------------|----------------------------------------------------------------|
| `vcr <tape>`           | Record a tape file to its declared `Output` target(s).         |
| `vcr record [tape]`    | Record a live shell session into a `.tape` file as you type — works in any shell. |
| `vcr validate <tape>`  | Parse a tape file and report syntax errors without recording.  |
| `vcr themes`           | List the 10 built-in themes with their background/foreground colors. |
| `vcr snap <command>`   | One-shot SVG screenshot of running `<command>` — no tape file. |
| `vcr capture <command>`| Animated SVG of `<command>` start to finish — no tape file.    |

## Documentation

Full docs, tutorials, and reference: **https://phil-scott-78.github.io/vcr/**

- [Getting Started Tutorial](https://phil-scott-78.github.io/vcr/tutorials/getting-started.html)
- [Command Reference](https://phil-scott-78.github.io/vcr/reference/tape-syntax.html)
- [Configuration Guide](https://phil-scott-78.github.io/vcr/how-to/cli-overrides.html)
- Sample tape files in [`samples/`](samples/)

## Attribution

This project is heavily inspired by [VHS](https://github.com/charmbracelet/vhs) by [Charm Bracelet](https://charm.sh/).
VcrSharp adds better Windows support, the ability to execute real commands via the `Exec` command, and an interactive
`record` mode that works across shells (VHS's is bash-only) — but VHS is still much more feature rich.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
