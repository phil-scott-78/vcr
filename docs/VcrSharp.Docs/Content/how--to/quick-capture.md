---
title: "Quick Capture Without Tape Files"
description: "Capture terminal screenshots and recordings directly from the command line without creating tape files"
uid: "docs.how-to.quick-capture"
order: 2600
---

## Overview

The `snap` and `capture` commands let you record terminal output directly from the command line without creating a tape file. These are ideal for:

- Quick one-off screenshots of command output
- CI/CD pipelines generating documentation
- Scripted generation of terminal visuals

Both commands execute a single shell command and produce SVG output.

## Snap Command

Captures a **static SVG screenshot** of the terminal after your command completes.

### Basic Usage

```bash
vcr snap "echo Hello World" -o hello.svg
```

The command waits for your shell command to finish and terminal output to stabilize, then captures the final state.

### Example: Documentation Screenshot

```bash
vcr snap "dotnet run --project .\Example\ canvas" -o canvas.svg --cols 82 --rows 16
```

### Example: Styled Screenshot

```bash
vcr snap "ls -la" -o files.svg --theme "Dracula" --disable-cursor --transparent-background
```

## Capture Command

Records an **animated SVG** of the entire command execution.

### Basic Usage

```bash
vcr capture "npm install" -o install.svg
```

The command records frames while your shell command runs, then renders an animated SVG.

### Example: Build Process Recording

```bash
vcr capture "dotnet build" -o build.svg --cols 100 --rows 30
```

### Example: Themed Recording

```bash
vcr capture "git status && git log --oneline -5" -o git.svg --theme "One Dark" --disable-cursor
```

## Common Options

Both commands support the same options:

| Option | Description | Example |
|--------|-------------|---------|
| `-o, --output` | Output file path (default: output.svg) | `-o demo.svg` |
| `--theme` | Terminal color theme | `--theme "Dracula"` |
| `--cols` | Terminal width in columns | `--cols 80` |
| `--rows` | Terminal height in rows | `--rows 24` |
| `--font-size` | Font size in pixels | `--font-size 18` |
| `--disable-cursor` | Hide the cursor in output | `--disable-cursor` |
| `--transparent-background` | Use transparent background | `--transparent-background` |
| `--end-buffer` | Buffer time after last activity | `--end-buffer 500ms` |
| `-v, --verbose` | Enable verbose logging | `-v` |

### Theme Examples

```bash
# Dracula theme
vcr snap "echo Hello" -o hello.svg --theme "Dracula"

# One Dark theme with transparent background
vcr snap "echo Hello" -o hello.svg --theme "One Dark" --transparent-background
```

Use `vcr themes` to see all available themes.

## When to Use Each

| Use Case | Command | Why |
|----------|---------|-----|
| Final output only | `snap` | Smaller file, just the result |
| Show the process | `capture` | Animated, shows execution |
| Documentation images | `snap` | Clean, static images |
| Demo recordings | `capture` | Shows real-time behavior |
| CI/CD screenshots | `snap` | Fast, deterministic |

## Snap vs Capture vs Tape Files

| Feature | `snap` | `capture` | Tape File |
|---------|--------|-----------|-----------|
| Output | Static SVG | Animated SVG | Any format |
| Commands | Single exec | Single exec | Multiple commands |
| Typing simulation | No | No | Yes |
| Setup required | None | None | Create .tape file |
| Best for | Quick screenshots | Quick recordings | Complex demos |

**Use tape files when you need:**
- Simulated typing with `Type` command
- Multiple commands with precise timing
- Complex workflows with `Wait`, `Sleep`, `Hide`/`Show`
- Non-SVG output formats (GIF, MP4, WebM)

## Troubleshooting

**If output captures too early:**
Use `--end-buffer` to wait longer after the last output:
```bash
vcr snap "slow-command" -o output.svg --end-buffer 2s
```

**If terminal dimensions are wrong:**
Specify explicit dimensions:
```bash
vcr snap "command" -o output.svg --cols 100 --rows 30
```

**If the command fails silently:**
Add verbose logging to see what's happening:
```bash
vcr snap "command" -o output.svg -v
```
