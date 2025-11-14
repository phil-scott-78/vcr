---
title: "How VCR# Talks to ttyd"
description: "Understanding the side-by-side architecture where VCR# orchestrates a terminal through browser automation"
uid: "docs.explanation.ttyd-interaction"
order: 3100
---

## The Side-by-Side Architecture

When VCR# records a terminal session, it doesn't directly manipulate a terminal window. Instead, four separate processes work together:

1. **VCR#** - The orchestrator
2. **ttyd** - The terminal server
3. **Browser** (Chromium) - The rendering engine
4. **Shell** (bash/pwsh/cmd) - The actual command interpreter

These components run independently, side-by-side, each handling a distinct responsibility. VCR# never directly reads from or writes to the shell—it operates by controlling a browser that displays a web-based terminal served by ttyd.

This architecture might seem indirect, but it's what enables VCR# to work identically across platforms while maintaining pixel-perfect visual consistency.

## Why This Separation Exists

**Why not directly control a terminal?**

Terminal emulation is deceptively complex. Getting fonts, colors, Unicode, ligatures, and emoji to render consistently across Windows, macOS, and Linux would take years of development. Each platform has different terminal APIs (ConPTY on Windows, PTY on Unix), different font rendering engines, and different default behaviors.

**VCR#'s approach: composition over custom**

Rather than building a custom terminal emulator, VCR# orchestrates existing, battle-tested tools:
- **ttyd** provides the terminal-to-web bridge
- **xterm.js** (inside ttyd's web page) handles terminal emulation and rendering
- **Playwright** provides browser automation and screenshot capabilities

Each component has millions of users finding edge cases VCR# will never encounter. xterm.js powers VS Code's terminal—that level of refinement would take a decade to build from scratch.

The trade-off: This architecture requires more dependencies and has ~2 seconds of startup overhead. But it provides cross-platform consistency and visual quality that would otherwise be impossible.

## The Communication Chain

### Sending Input: From VCR# to Shell

When a `Type` or `Key` command executes, input flows through multiple layers:

```
VCR# Command
  ↓
Playwright Automation API
  ↓
Browser Keyboard Event
  ↓
xterm.js (in browser)
  ↓
WebSocket Message
  ↓
ttyd Process
  ↓
PTY (Pseudo-Terminal)
  ↓
Shell Process (bash/pwsh/cmd)
```

**What's actually happening:**

1. **VCR# calls Playwright**: `page.Keyboard.TypeAsync("hello")`
2. **Playwright sends browser automation commands**: Simulates physical keyboard events in Chromium
3. **xterm.js receives keyboard events**: The terminal emulator running in the browser captures them
4. **WebSocket transmission**: xterm.js sends the keystrokes to ttyd over a WebSocket connection
5. **ttyd writes to PTY**: Acts as a bridge, converting WebSocket messages to PTY input
6. **Shell receives input**: The actual shell process (bash/PowerShell/cmd) reads from its PTY

This chain seems convoluted, but each step serves a purpose:
- **Playwright**: Provides reliable cross-platform automation
- **Browser**: Handles all the complexity of keyboard input (modifiers, special keys, international keyboards)
- **xterm.js**: Translates browser keyboard events into terminal input codes
- **WebSocket**: Provides bi-directional communication between browser and server
- **ttyd**: Bridges the web world (WebSocket) and Unix world (PTY)
- **PTY**: Provides the shell with a terminal interface it understands

### Reading Output: From Shell to VCR#

Reading terminal output works differently—VCR# doesn't intercept shell output. Instead, it reads what's already been rendered in the browser:

```
Shell Process Output
  ↓
PTY
  ↓
ttyd Process
  ↓
WebSocket Message
  ↓
xterm.js Rendering
  ↓
Browser Canvas Display
  ↓
Playwright JavaScript Execution
  ↓
VCR# receives text/screenshots
```

**What's actually happening:**

1. **Shell produces output**: Command writes to stdout/stderr
2. **PTY captures output**: Terminal interface receives the bytes
3. **ttyd forwards via WebSocket**: Sends output to the browser
4. **xterm.js renders**: Terminal emulator interprets ANSI codes, updates display
5. **Browser renders to canvas**: xterm.js draws characters, colors, cursor to canvas elements
6. **VCR# reads through Playwright**: Executes JavaScript in the browser to access xterm.js internals

**Two ways VCR# reads output:**

**Text reading** (for Wait commands):
```javascript
// VCR# runs this JavaScript in the browser
window.term.buffer.active.getLine(lineNumber).translateToString(true)
```

This accesses xterm.js's internal buffer directly, reading the actual text content that was rendered.

**Visual capture** (for recording):
```csharp
// Playwright screenshots the canvas elements
await page.ScreenshotAsync(options)
```

This captures the pixels displayed in the browser—the visual representation of the terminal.

Notice the key insight: **VCR# never directly reads from the shell's output stream**. It reads what the browser has already rendered. This indirection is what enables consistent visual output—we capture exactly what xterm.js displays, not what the shell emitted.

## The Recording Lifecycle

Understanding how these components interact during a full recording session reveals why this architecture works:

### Phase 1: Startup

**VCR# starts the terminal server:**

1. Finds an available port (requests ephemeral port from OS)
2. Spawns ttyd process: `ttyd --port 12345 --writable pwsh.exe`
3. ttyd launches the shell (PowerShell in this example)
4. ttyd binds to localhost:12345 and waits for browser connections
5. VCR# polls the port until ttyd responds (confirms it's ready)

At this point, two processes are running side-by-side:
- **ttyd** (serving a web page on port 12345)
- **shell** (running under ttyd, waiting for input)

**VCR# connects the browser:**

1. Launches headless Chromium via Playwright
2. Navigates to `http://localhost:12345`
3. Browser loads ttyd's web page (which includes xterm.js)
4. xterm.js initializes and connects to ttyd via WebSocket
5. VCR# waits for terminal to be ready (checks for shell prompt)

Now four processes are running side-by-side:
- **VCR#** (orchestrating)
- **Browser** (displaying terminal)
- **ttyd** (bridging browser ↔ shell)
- **shell** (ready for commands)

### Phase 2: Command Execution

**VCR# executes tape commands:**

For each command in the tape file, VCR# calls `ExecuteAsync()`, which interacts with the terminal through the browser.

**Example: Type command**
```tape
Type "echo hello"
```

1. VCR#: Calls `Playwright.Keyboard.TypeAsync("echo hello")`
2. Browser: Generates keyboard events for each character
3. xterm.js: Receives events, sends via WebSocket
4. ttyd: Forwards to shell's PTY
5. Shell: Receives input, displays it (echo mode)
6. ttyd: Forwards display updates back to browser
7. xterm.js: Renders updated terminal display
8. Browser: Canvas shows "echo hello"

**Example: Wait command**
```tape
Wait /hello/
```

1. VCR#: Starts polling loop (every 10ms)
2. Executes JavaScript in browser: `window.term.buffer.active`
3. Reads terminal text content from xterm.js
4. Checks if pattern `/hello/` matches
5. Repeats until match found or timeout

**Example: Exec command**
```tape
Exec "npm test"
```

1. VCR#: Command was already sent to shell at startup (optimization)
2. Shell: Executes the command, produces output
3. Output flows: shell → ttyd → xterm.js → browser canvas
4. VCR#: Monitors terminal buffer for inactivity (no changes for 2 seconds = command finished)

### Phase 3: Frame Capture

**While commands execute, VCR# continuously captures screenshots:**

1. **Background thread** runs a capture loop at 50fps (20ms intervals)
2. Each iteration: `await page.ScreenshotAsync()`
3. Playwright captures the browser's canvas elements
4. Returns PNG bytes
5. VCR# writes PNG to disk: `frame0001.png`, `frame0002.png`, ...

This happens **in parallel** with command execution. The capture loop doesn't care what commands are running—it just screenshots at regular intervals.

**Key insight**: Frame capture and command execution are independent. This separation ensures consistent framerate regardless of command timing.

### Phase 4: Cleanup

**When recording finishes:**

1. VCR# stops frame capture (no more screenshots)
2. Closes browser (disconnects from ttyd)
3. Kills ttyd process (which terminates the shell)
4. Processes frames (trim blank frames, generate video)
5. Deletes temporary PNG files

The components shut down in reverse order of startup, ensuring clean termination.

## Why This Seems Inefficient (But Isn't)

At first glance, this architecture looks wasteful:
- Why run a browser just to automate a terminal?
- Why use WebSockets when the shell is on the same machine?
- Why capture screenshots instead of recording terminal escape codes?

**The answers reveal VCR#'s priorities:**

**Browser overhead is deliberate**: We're not trying to be the lightest recorder—we're trying to produce pixel-perfect, cross-platform consistent output. xterm.js rendering quality justifies the browser overhead.

**WebSocket indirection enables testing**: ttyd's web interface means VCR# never directly manipulates PTY APIs, which differ across platforms. ttyd handles platform-specific complexity.

**Screenshots enable universality**: Capturing pixels (not escape codes) means output works everywhere—GitHub READMEs, presentations, documentation sites. No special player needed.

The "inefficiency" is an investment in:
- **Reliability**: Battle-tested components instead of custom solutions
- **Consistency**: Identical rendering across platforms
- **Compatibility**: Standard video formats that play anywhere

## What This Means for Users

Understanding this architecture explains several VCR# characteristics:

**Why startup takes 2-3 seconds**: VCR# must launch ttyd, wait for port availability, launch browser, wait for xterm.js initialization, and wait for shell prompt. That's unavoidable with this architecture.

**Why VCR# requires dependencies**: ttyd, Chromium (via Playwright), and FFmpeg aren't optional—they're fundamental to how VCR# works.

**Why recordings look identical everywhere**: Because we're capturing what xterm.js renders, not what the shell emits. xterm.js is consistent across platforms.

**Why Wait commands are necessary**: VCR# can't know when commands finish (it doesn't monitor the shell directly). It must watch the browser's terminal display for expected output.

**Why recordings work in CI/CD**: Everything runs headless—no visible windows, no display server needed. ttyd serves localhost, browser runs headless, shell executes commands normally.

## The Browser as Automation Target

The crucial insight: **VCR# treats the browser as the terminal**, not the shell.

To VCR#, the terminal *is* the browser tab showing ttyd's web interface. All interaction happens through browser automation—keyboard input via Playwright, output reading via JavaScript execution, visual capture via screenshots.

The shell, ttyd, and WebSocket are implementation details. VCR# doesn't care how keystrokes reach the shell or how output reaches the browser. It only cares that typing in the browser causes characters to appear, and running commands causes output to display.

This abstraction is what makes VCR# maintainable. Browser automation is well-understood and well-supported. Terminal emulation is complex and platform-specific. By operating at the browser level, VCR# avoids re-implementing terminal complexity.

## Comparison to Other Architectures

Understanding how other tools work differently clarifies VCR#'s approach:

**asciinema** (direct terminal recording):
- Records directly from PTY output
- No browser, no web server
- Much lighter weight
- Requires custom player for playback

**OBS / Screen recorders** (display capture):
- Captures actual screen pixels
- Records whatever is visible
- No programmatic control
- Can't script or reproduce

**VHS** (VCR#'s inspiration):
- Same ttyd + browser architecture
- Identical communication patterns
- Difference: VHS lacks real command execution (no Exec)

**VCR#'s position**:
- Heavier than asciinema (browser overhead)
- More reproducible than screen recorders (fully scripted)
- More flexible than VHS (Exec + Type)

Each architecture optimizes for different priorities. VCR# prioritizes reproducible, visually consistent, universally compatible output—and the side-by-side architecture enables that.

## The Design Philosophy

This architecture embodies VCR#'s core philosophy: **compose battle-tested tools rather than build custom solutions**.

Each component is industry-proven:
- **ttyd**: Powers countless web-based terminals
- **xterm.js**: Powers VS Code, GitHub Codespaces, AWS Cloud9
- **Playwright**: Used by thousands of projects for browser testing
- **Chromium**: Powers Chrome, Edge, Brave

VCR# is the orchestrator, not the implementer. It coordinates these components to produce terminal recordings, but it doesn't reinvent terminal emulation, browser automation, or video encoding.

The side-by-side architecture isn't accidental—it's essential to VCR#'s value proposition. By operating through established tools, VCR# achieves reliability and compatibility that would take years to build from scratch.

## Conclusion

When you run `vcr demo.tape`, you're not just recording a terminal—you're orchestrating a pipeline:
- VCR# launches ttyd
- ttyd launches a shell
- VCR# launches a browser
- Browser connects to ttyd
- VCR# automates the browser
- Browser controls the terminal
- Terminal controls the shell

Each layer adds overhead, but each layer solves a real problem:
- **ttyd**: Platform-independent terminal interface
- **Browser**: Consistent rendering engine
- **xterm.js**: Production-quality terminal emulation
- **Playwright**: Reliable automation API

The result: recordings that look identical everywhere, work in CI/CD, and require no special playback tools.

The side-by-side architecture isn't the simplest approach—it's the most reliable one.
