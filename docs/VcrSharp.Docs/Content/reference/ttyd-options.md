---
title: "How VCR# Uses ttyd"
description: "How VCR# launches and drives ttyd: the real command line, shell setup, lifecycle, and timeouts"
uid: "docs.reference.ttyd-options"
order: 4400
---

## Overview

[ttyd](https://github.com/tsl0922/ttyd) is a terminal server that exposes a shell over HTTP/WebSocket. VCR# launches one
ttyd process per recording, points a headless browser at it (so xterm.js — the same emulator used in VS Code — renders the
terminal), and captures the result. Most ttyd parameters are fixed in code; a few tape settings change the invocation.

**User-controllable:**

- `Shell` — `Set Shell "bash"` (or platform default)
- `WorkingDirectory` — `Set WorkingDirectory "/path"`
- Terminal appearance (`Cols`, `Rows`, `Width`, `Height`, `FontSize`, `Theme`, …) — applied to xterm.js via the browser

**Requirement:** ttyd must be on the system `PATH`. VCR# checks only that the executable is present; it does not enforce a
version (the project recommends ttyd ≥ 1.7.2).

## The Command VCR# Invokes

For a tape with no `Exec` commands, VCR# starts ttyd roughly like this:

```bash
ttyd --port=<port> \
     --interface 127.0.0.1 \
     -w <working-directory> \
     -t rendererType=canvas \
     -t disableResizeOverlay=true \
     -t enableSixel=true \
     -t customGlyphs=true \
     --writable \
     <shell> <shell-args...>
```

| Option | Value | Source |
|--------|-------|--------|
| `--port=<port>` | An OS-assigned ephemeral port | Fixed (see [Port selection](#port-selection)) |
| `--interface 127.0.0.1` | Localhost-only binding | Fixed |
| `-w <dir>` | `WorkingDirectory`, or the current directory if unset (always passed) | `Set WorkingDirectory` |
| `-t rendererType=canvas` | xterm.js canvas renderer | Fixed |
| `-t disableResizeOverlay=true` | Hide the resize overlay | Fixed |
| `-t enableSixel=true` | Enable Sixel graphics | Fixed |
| `-t customGlyphs=true` | Custom box-drawing glyphs | Fixed |
| `--writable` | Allow keyboard/paste input from the browser | Fixed |
| `<shell> <args...>` | The shell and its startup flags | `Set Shell` / platform default |

A few notes on what's *not* here: there is no `-p`/`-W` short-form (VCR# uses `--port=`/`--writable`), and `--writable`
enables input — it has nothing to do with whether ttyd waits for a browser. The `-t` flags configure xterm.js client
options directly on the ttyd command line; the remaining appearance (theme, fonts, dimensions) is applied later through
the browser.

## Shell Selection and Startup

When `Set Shell` is not specified, VCR# picks a platform default:

| Platform | Detection priority |
|----------|--------------------|
| Windows  | `pwsh` → `powershell` → `cmd` |
| Linux / macOS | `bash` |

VCR# launches each shell with **profile/rc files disabled** and a minimal `> ` prompt, so recordings are reproducible and
don't pick up your personal aliases, functions, or prompt customizations:

| Shell | Key startup flags | Prompt |
|-------|-------------------|--------|
| `pwsh` | `-Login -NoLogo -NoExit -NoProfile` | `function prompt { '> ' }` |
| `powershell` | `-NoLogo -NoExit -NoProfile` | `function prompt { '> ' }` |
| `bash` | `--noprofile --norc` | `PS1='> '` |
| `zsh` | `--histnostore --no-rcs` | `PROMPT='> '` |
| `fish` | `--login --no-config --private` | custom `fish_prompt` |
| `cmd` | `/k prompt=$G$S` | `> ` |

> **Your `.bashrc` / `$PROFILE` does not run.** Because profile/rc loading is suppressed, anything you'd normally rely on
> from your shell startup (aliases, functions, PATH tweaks, custom prompts) will **not** appear in a recording. Use
> explicit `Exec` or `Env` commands in the tape to set up the environment you want to show.

The default prompt-detection pattern used by `Wait` (when you don't supply your own) is the regex `>\s*$`, which matches
the forced `> ` prompt above.

### When a tape uses `Exec`

If the tape contains `Exec` commands, VCR# doesn't append an interactive shell. Instead it builds a small **startup
script** — a short delay (`StartupDelay`, default 3.5s) followed by the `Exec` commands joined together — and launches the
shell to run that script (`-File` for PowerShell, `/c` for CMD, `-c` for Unix shells). The shell exits when the commands
finish. This is why all `Exec` commands run together at startup rather than being typed one at a time.

## Port Selection

VCR# asks the OS for a free port by binding a socket to port `0` on `127.0.0.1`, then hands that port to ttyd. The result
is an arbitrary ephemeral port (not a fixed range), which lets multiple recordings run concurrently without collisions.
The specific number doesn't matter because the browser connects programmatically.

## Process Lifecycle

### Startup

1. VCR# obtains a free ephemeral port and spawns ttyd with the arguments above.
2. ttyd binds the port and launches the shell.
3. VCR# polls the port with TCP connection attempts until it accepts connections (a hardcoded readiness wait of about
   5 seconds — 250 attempts, 20 ms apart).
4. If ttyd exits before becoming ready, VCR# aborts with `ttyd process exited unexpectedly with code <N>`. If it never
   becomes ready, VCR# raises a timeout for that port.
5. Playwright launches headless Chromium, navigates to `http://127.0.0.1:<port>`, and waits for xterm.js and the shell
   prompt.

> The ttyd readiness wait is fixed (~5s). The `StartWaitTimeout` setting governs how long VCR# waits for the *first
> terminal activity* once recording begins — it is **not** wired to ttyd process startup.

### Shutdown

When recording finishes, VCR# kills the ttyd process tree, waits up to 5 seconds for it to exit, and deletes any
temporary startup-script file. Killing ttyd terminates the shell.

## Environment Variables

VCR# starts ttyd with a merged environment: any shell-specific variables and your `Env` commands first, then the rest of
the current process environment (existing values are not overwritten). VCR# does **not** set `TERM` itself — the value
the shell sees comes from the inherited environment.

```tape
Env NODE_ENV "production"
Env API_KEY "secret123"
```

These reach the shell (and the commands it runs), not ttyd's own configuration.

## xterm.js Configuration

ttyd has no config file, so beyond the `-t` flags on its command line, VCR# configures the terminal at runtime through
Playwright: it injects theme colors, sets xterm.js options, and applies CSS.

| Setting | Value | Source |
|---------|-------|--------|
| Columns / Rows | From `Cols`/`Rows`, or derived from `Width`/`Height` and font metrics | `Set Cols`/`Rows`/`Width`/`Height` |
| `fontSize` | `FontSize` (default 22) | `Set FontSize` |
| `fontFamily` | `FontFamily` (default `monospace`) | `Set FontFamily` |
| `letterSpacing` | `LetterSpacing` (default 1.0) | `Set LetterSpacing` |
| `lineHeight` | `LineHeight` (default 1.0) | `Set LineHeight` |
| `cursorBlink` | `CursorBlink` (default true) | `Set CursorBlink` |
| `allowTransparency` | Set to `true` when `TransparentBackground` is enabled | `Set TransparentBackground` |
| Theme colors | All 16 ANSI colors + fg/bg/cursor | `Set Theme` |

VCR# does not set an xterm.js `cursorStyle`; the cursor uses xterm.js's default. `Set DisableCursor true` hides the cursor
via injected CSS rather than a terminal option.

## Security Model

- **Localhost-only binding** (`--interface 127.0.0.1`): ttyd's HTTP server is unauthenticated, so VCR# binds it to
  loopback only — it is not reachable from the local network or internet.
- **Per-recording isolation**: each recording starts and stops its own ttyd process. There are no persistent terminal
  servers and no shared state between recordings.

## External Resources

- [ttyd GitHub Repository](https://github.com/tsl0922/ttyd)
- [ttyd Command-Line Options](https://github.com/tsl0922/ttyd/blob/main/README.md#command-line-options)
- [xterm.js API Documentation](https://xtermjs.org/docs/api/terminal/)
