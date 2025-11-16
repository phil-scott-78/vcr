# VcrSharp

> **⚠️ WORK IN PROGRESS**
> This project is in early development and the API will change rapidly. If you need a stable, production-ready terminal
> recorder, please use [VHS](https://github.com/charmbracelet/vhs) instead. VcrSharp is experimental and should be
> considered alpha quality.

A .NET terminal recorder that turns `.tape` files into GIFs and videos. Write your terminal demos as code, then render
them to video.

![VCR Install Demo](docs/VcrSharp.Docs/Content/vcr-install.gif)

## Installation

Install VcrSharp as a global .NET tool:

```bash
dotnet tool install --global vcr
```

## Documentation

For complete documentation, tutorials, and examples, visit:

**https://phil-scott-78.github.io/vcr/**

The documentation includes:

- [Getting Started Tutorial](https://phil-scott-78.github.io/vcr/tutorials/getting-started.html)
- [Command Reference](https://phil-scott-78.github.io/vcr/reference/tape-syntax.html)
- [Configuration Guide](https://phil-scott-78.github.io/vcr/how-to/cli-overrides.html)
- Sample tape files and examples

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

## Attribution

This project is heavily inspired by [VHS](https://github.com/charmbracelet/vhs) by [Charm Bracelet](https://charm.sh/).
VcrSharp adds better Windows support and the ability to execute real commands via the `Exec` command, but VHS is still
much more feature rich.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
