---
title: "How VCR# Uses ttyd"
description: "Internal ttyd invocation reference - VCR#'s hardcoded process management"
uid: "docs.reference.ttyd-options"
order: 4400
---

## Overview

This document describes how VCR# internally uses ttyd for terminal emulation. **Most ttyd parameters are hardcoded in
VCR# and not user-configurable.**

ttyd is a terminal server that exposes terminal sessions over HTTP/WebSocket. VCR# uses ttyd version 1.7.2 or later to
provide browser-based terminal rendering via xterm.js (the same terminal emulator used in VS Code).

**User-Controllable Settings:**

- `Shell` - Set via `Set Shell "bash"` in tape files
- `WorkingDirectory` - Set via `Set WorkingDirectory "/path"` in tape files
- Terminal dimensions (`Cols`, `Rows`, `Width`, `Height`, `FontSize`) - Affect xterm.js configuration

**All Other Parameters:** Hardcoded in VCR# (port selection, binding, timeouts, flags)

**Requirement:** ttyd must be available in system PATH.

## Command VCR# Invokes

```bash
ttyd -p <port> -W -i 127.0.0.1 [-w <directory>] <shell-command>
```

## Parameters VCR# Uses

| Option    | Value                                    | Source                                 | Rationale                                                       |
|-----------|------------------------------------------|----------------------------------------|-----------------------------------------------------------------|
| `-p`      | Random port (8000-9000 range)            | Hardcoded                              | Avoids port conflicts, allows concurrent recordings             |
| `-W`      | Always set                               | Hardcoded                              | Enables non-interactive startup (ttyd doesn't wait for browser) |
| `-i`      | `127.0.0.1`                              | Hardcoded                              | Security: localhost-only binding prevents network exposure      |
| `-w`      | User's `WorkingDirectory` or current dir | Tape file `Set WorkingDirectory`       | Allows users to control shell start location                    |
| `<shell>` | User's `Shell` or platform default       | Tape file `Set Shell` or auto-detected | Supports user's preferred shell                                 |

### Why These Choices?

**Random port selection**: VCR# requests an available port from the OS in the 8000-9000 range. This prevents conflicts
if multiple VCR# instances run concurrently and avoids colliding with common development servers (3000, 8080, etc.).

**`-W` flag**: Without this flag, ttyd waits for a browser connection before starting the shell. VCR# needs the shell to
start immediately so Playwright can connect on its own schedule.

**Localhost-only binding (`-i 127.0.0.1`)**: Security measure. ttyd's HTTP server is unauthenticated by default. Binding
to localhost ensures the terminal session isn't exposed to the local network or internet.

**Working directory from user**: Different recordings often need different start locations (project root, specific
subdirectory, etc.). Making this user-controllable via `Set WorkingDirectory` is essential.

**Shell from user or auto-detect**: Users have shell preferences (Bash vs Zsh, PowerShell vs CMD). VCR# respects
`Set Shell` but falls back to platform defaults when not specified.

## Shell Selection

VCR#'s shell auto-detection when `Set Shell` is not specified in tape files:

### Platform Defaults

| Platform | Detection Priority      | Rationale                                                                       |
|----------|-------------------------|---------------------------------------------------------------------------------|
| Windows  | pwsh → powershell → cmd | Modern PowerShell (pwsh) preferred, falls back to Windows PowerShell, then CMD  |
| Linux    | bash                    | Universal Unix shell, almost always available                                   |
| macOS    | bash                    | Standard macOS shell (Zsh now default but Bash more predictable for recordings) |

### How User's `Set Shell` Maps to ttyd

```tape
Set Shell "pwsh"          # VCR# invokes: ttyd [...] pwsh
Set Shell "bash"          # VCR# invokes: ttyd [...] bash
Set Shell "zsh"           # VCR# invokes: ttyd [...] zsh
Set Shell "cmd"           # VCR# invokes: ttyd [...] cmd
Set Shell "/bin/bash"     # VCR# invokes: ttyd [...] /bin/bash
```

VCR# passes the shell string directly to ttyd. No validation or path resolution - ttyd will fail if the shell isn't
available.

## Process Lifecycle

### Startup Sequence

1. VCR# spawns ttyd process with specified options
2. ttyd binds to assigned port
3. ttyd starts shell subprocess
4. HTTP endpoint becomes available
5. WebSocket server starts
6. Shell startup files execute (.bashrc, profile.ps1, etc.)

### Communication

**Browser → ttyd → Shell:**

- Keyboard input via WebSocket
- Terminal commands via xterm.js

**Shell → ttyd → Browser:**

- Terminal output via WebSocket
- Display updates via xterm.js

### Shutdown

1. VCR# sends termination signal
2. ttyd terminates shell subprocess
3. ttyd process exits
4. Port released
5. Resources cleaned up

## xterm.js Configuration

VCR# injects configuration into ttyd's xterm.js frontend via Playwright:

| Setting      | Value                            | Source                                 | Rationale                                            |
|--------------|----------------------------------|----------------------------------------|------------------------------------------------------|
| Columns      | User's `Cols` or calculated      | Tape `Set Cols` or `Width`/`FontSize`  | Controls terminal width in characters                |
| Rows         | User's `Rows` or calculated      | Tape `Set Rows` or `Height`/`FontSize` | Controls terminal height in lines                    |
| Font Size    | User's `FontSize` (default 22px) | Tape `Set FontSize`                    | User-controllable for readability                    |
| Font Family  | `monospace`                      | Hardcoded                              | Generic monospace ensures cross-platform consistency |
| Theme Colors | User's `Theme`                   | Tape `Set Theme`                       | User-controllable color schemes                      |
| Cursor Style | `block`                          | Hardcoded                              | Most visible cursor style for recordings             |

### How Configuration Injection Works

1. VCR# connects Playwright to ttyd's URL
2. Playwright injects CSS into the page (themes, fonts)
3. Playwright calls xterm.js JavaScript APIs to configure terminal dimensions
4. Terminal renders with injected configuration

**Why CSS injection?** ttyd doesn't support configuration files. VCR# must inject styling and configuration at runtime
via the browser automation layer.

## Working Directory

How user's `WorkingDirectory` setting affects ttyd invocation:

```tape
Set WorkingDirectory "C:\\Projects\\MyApp"
# VCR# invokes: ttyd [...] -w "C:\Projects\MyApp" pwsh
```

```tape
Set WorkingDirectory "/home/user/projects"
# VCR# invokes: ttyd [...] -w "/home/user/projects" bash
```

If `WorkingDirectory` is not set, VCR# omits the `-w` flag and ttyd starts the shell in its current working directory (
typically where `vcr-sharp` was invoked).

## Environment Variables

User's `Env` commands set environment variables for the shell:

```tape
Env PATH "/custom/bin:$PATH"
Env NODE_ENV "production"
```

VCR# passes these to the shell subprocess (via ttyd), not to ttyd itself. They're available to commands executed in the
recording.

## Terminal Type

ttyd automatically sets `TERM=xterm-256color` in the shell environment:

- Indicates 256-color terminal capability
- Compatible with most terminal applications
- VCR# does not override this (hardcoded by ttyd)

## Port Selection

VCR#'s port allocation strategy:

- **Requests random available port** from OS (typically 8000-9000 range)
- **Binds to localhost** (127.0.0.1) only via `-i` flag
- **Protocol:** HTTP for initial page load, WebSocket for terminal I/O

**Why random ports?** Allows multiple VCR# instances to run concurrently without port conflicts. The specific port
number doesn't matter since Playwright connects programmatically.

## Shell Startup Behavior

ttyd launches shells as **interactive login shells**, meaning your shell configuration files execute:

### What This Means

- Shell configuration files sourced (`.bashrc`, `profile.ps1`, etc.)
- Profile scripts execute
- Environment variables from profile loaded
- Shell prompt customization applies
- Aliases and functions available

**Why this matters:** Your recordings reflect your actual shell environment. If your `.bashrc` sets custom prompts or
aliases, they'll appear in recordings. This is usually desirable but can cause issues if startup scripts are slow or
produce unexpected output.

### Platform-Specific Configuration Files

| Shell      | Windows                                   | Linux/macOS                        |
|------------|-------------------------------------------|------------------------------------|
| PowerShell | `$PROFILE` paths                          | `~/.config/powershell/profile.ps1` |
| Bash       | N/A (Windows doesn't use Bash by default) | `~/.bashrc`, `~/.bash_profile`     |
| Zsh        | N/A                                       | `~/.zshrc`                         |
| CMD        | N/A (limited startup configuration)       | N/A                                |

## Security Model

### Why Localhost-Only Binding?

VCR# hardcodes `-i 127.0.0.1` for ttyd:

- **No external network access:** Terminal session not accessible from other machines
- **Not visible to local network:** Other devices on your network cannot connect
- **Only accessible from local machine:** Playwright running locally can connect

ttyd's HTTP server has no authentication. Binding to localhost prevents accidentally exposing terminal sessions to the
network.

### Process Isolation

Each VCR# recording creates an isolated ttyd process:

- **No persistent terminal servers:** ttyd starts and stops per recording
- **Process terminates after recording:** No lingering background processes
- **No shared state:** Each recording gets a fresh shell environment

**Why isolation?** Ensures recordings are reproducible and don't interfere with each other.

## Exit Code Handling

VCR# monitors ttyd exit codes to detect failures:

| Exit Code | Meaning           | VCR# Behavior                                    |
|-----------|-------------------|--------------------------------------------------|
| 0         | Success           | Normal termination, continue to encoding         |
| 1         | Generic error     | Log error from ttyd stderr, abort recording      |
| 127       | Command not found | Display "Shell not found" error, abort recording |
| Other     | Various errors    | Log error, abort recording                       |

**Why VCR# cares:** ttyd failures usually indicate missing dependencies (ttyd not installed, shell not found) or port
binding issues. VCR# captures stderr output to provide debugging information.

## HTTP Endpoints (For Reference)

ttyd exposes these endpoints (VCR# uses `/` and `/ws`):

| Path     | Method    | VCR# Usage                                                           |
|----------|-----------|----------------------------------------------------------------------|
| `/`      | GET       | Playwright navigates here to access xterm.js terminal                |
| `/token` | POST      | Not used (ttyd authentication feature VCR# doesn't need)             |
| `/ws`    | WebSocket | Terminal I/O stream (keyboard input → shell, shell output → browser) |

## WebSocket Protocol (For Reference)

ttyd's WebSocket communication format (transparent to VCR# users):

| Direction      | Format | Content                                                           |
|----------------|--------|-------------------------------------------------------------------|
| Browser → ttyd | JSON   | Input events (key presses, paste, resize)                         |
| ttyd → Browser | Binary | Terminal output (ANSI escape sequences, colors, cursor movements) |

Playwright handles this protocol automatically. VCR# doesn't interact with WebSocket directly.

## Timeout Configuration

VCR#'s hardcoded timeouts for ttyd operations:

| Operation            | Timeout             | Source                               | Rationale                                               |
|----------------------|---------------------|--------------------------------------|---------------------------------------------------------|
| ttyd startup         | 10s default         | User's `StartWaitTimeout` or default | Shell startup files can be slow (loading profile, etc.) |
| HTTP endpoint ready  | Included in startup | Part of `StartWaitTimeout`           | ttyd must bind port and start HTTP server               |
| WebSocket connection | 5s                  | Hardcoded                            | Connecting to already-running ttyd should be fast       |
| Process termination  | 5s                  | Hardcoded                            | Graceful shutdown should complete quickly               |

**User control:** Only `StartWaitTimeout` is configurable via `Set StartWaitTimeout 15s`. Other timeouts are hardcoded
based on expected operation durations.

## ttyd Version Requirements

VCR#'s ttyd version compatibility:

| ttyd Version  | Status           | Notes                                        |
|---------------|------------------|----------------------------------------------|
| < 1.7.2       | Not supported    | Missing features VCR# depends on             |
| 1.7.2 - 1.7.x | Fully supported  | Minimum required version, tested extensively |
| 2.x           | Expected to work | Not extensively tested but API is stable     |

**Why 1.7.2 minimum?** Earlier versions have WebSocket protocol incompatibilities and missing command-line options that
VCR# requires.

## External Resources

- [ttyd GitHub Repository](https://github.com/tsl0922/ttyd)
- [ttyd Command-Line Options](https://github.com/tsl0922/ttyd/blob/main/README.md#command-line-options)
- [xterm.js API Documentation](https://xtermjs.org/docs/api/terminal/)
- [WebSocket Protocol RFC 6455](https://tools.ietf.org/html/rfc6455)
