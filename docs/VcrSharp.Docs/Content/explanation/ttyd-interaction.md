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

These components run independently, side-by-side, each handling a distinct responsibility.

## Why This Separation Exists

**Why not directly control a terminal?**

Terminal emulation is deceptively complex. Getting fonts, colors, Unicode, ligatures, and emoji to render consistently across Windows, macOS, and Linux would take years of development. Each platform has different terminal APIs (ConPTY on Windows, PTY on Unix), different font rendering engines, and different default behaviors.

**VCR#'s approach: composition over custom**

Rather than building a custom terminal emulator, VCR# orchestrates existing, battle-tested tools:
- **ttyd** provides the terminal-to-web bridge
- **xterm.js** (inside ttyd's web page) handles terminal emulation and rendering
- **Playwright** provides browser automation and screenshot capabilities

Each component has millions of users finding edge cases VCR# will never encounter.

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

**The key architectural insight**: VCR# operates entirely through the browser—it never directly interacts with the shell's input/output streams. All input goes through Playwright's keyboard automation, and all output is read from xterm.js's rendered display. This indirection is what enables consistent visual output across platforms—we capture exactly what xterm.js displays, not what the shell emitted.

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

This follows the input flow described above. VCR# calls `Playwright.Keyboard.TypeAsync("echo hello")`, which travels through the chain to the shell. The shell's echo mode displays each character as it's typed, creating the visual effect of someone typing in real-time.

**Example: Wait command**
```tape
Wait /hello/
```

1. VCR#: Starts polling loop (every 10ms)
2. Executes JavaScript in browser: `window.term.buffer.active`
3. Reads terminal text content from xterm.js
4. Checks if pattern `/hello/` matches
5. Repeats until match found or timeout

### Phase 3: Frame Capture

**While commands execute, VCR# continuously captures screenshots:**

1. **Background thread** runs a capture loop at 50fps (20ms intervals)
2. Each iteration: `await page.ScreenshotAsync()`
3. Playwright captures the browser's canvas elements
4. Returns PNG bytes
5. VCR# writes PNG to disk: `frame0001.png`, `frame0002.png`, ...

This happens **in parallel** with command execution. The capture loop doesn't care what commands are running—it just screenshots at regular intervals. This separation ensures consistent framerate regardless of command timing.

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

**The answers reveal VCR#'s design priorities:**

- **Browser overhead is deliberate**: We prioritize pixel-perfect, cross-platform consistent output over being the lightest recorder. xterm.js's production-grade rendering (it powers VS Code's terminal) justifies the browser overhead.
- **WebSocket indirection enables platform independence**: ttyd handles all platform-specific PTY complexity, so VCR# never directly manipulates PTY APIs that differ across Windows, macOS, and Linux.
- **Screenshots enable universality**: Capturing pixels (not escape codes) means output works everywhere—GitHub READMEs, presentations, documentation sites—with no special player needed.

The "inefficiency" is an investment in reliability, consistency, and compatibility.

## What This Means for Users

Understanding this architecture explains several VCR# characteristics:

**Why startup takes 2-3 seconds**: VCR# must launch ttyd, wait for port availability, launch browser, wait for xterm.js initialization, and wait for shell prompt. That's unavoidable with this architecture.

**Why VCR# requires dependencies**: ttyd, Chromium (via Playwright), and FFmpeg aren't optional—they're fundamental to how VCR# works.

**Why recordings look identical everywhere**: xterm.js provides consistent rendering across all platforms.

**Why Wait commands are necessary**: VCR# can't know when commands finish (it doesn't monitor the shell directly). It must watch the browser's terminal display for expected output.

**Why recordings work in CI/CD**: Everything runs headless—no visible windows, no display server needed. ttyd serves localhost, browser runs headless, shell executes commands normally.

## The Browser as Automation Target

To VCR#, the terminal *is* the browser tab showing ttyd's web interface. All interaction happens through browser automation—keyboard input via Playwright, output reading via JavaScript execution, visual capture via screenshots.

This abstraction is what makes VCR# maintainable. Browser automation is well-understood and well-supported. Terminal emulation is complex and platform-specific. By operating at the browser level, VCR# avoids re-implementing terminal complexity.

## Conclusion

When you run `vcr demo.tape`, you're orchestrating a multi-layered pipeline where each component handles a distinct concern. The result: recordings that look identical everywhere, work in CI/CD, and require no special playback tools.
