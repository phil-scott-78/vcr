---
title: "How VCR# Works: Architecture Overview"
description: "Understanding how ttyd, Playwright, and FFmpeg work together to generate terminal recordings"
uid: "docs.explanation.architecture"
order: 3100
---

## High-Level Overview

VCR# combines three powerful tools to create terminal recordings:
- **ttyd** - Provides a web-based terminal
- **Playwright** - Automates the browser and captures screenshots
- **FFmpeg** - Converts screenshots into video files

Together, these create a pipeline that transforms tape file commands into high-quality GIFs, MP4s, and WebMs.

## Design Philosophy: Composition Over Custom

At first glance, VCR#'s architecture might seem heavyweight—launching a terminal server, a headless browser, and a video encoder just to record terminal output. Why not build a custom terminal emulator and renderer?

This apparent complexity reflects a deliberate choice: **compose battle-tested tools rather than build custom solutions**.

Each component (ttyd, Playwright, FFmpeg) has thousands of users finding bugs we'll never encounter. xterm.js powers VS Code's terminal—millions of users have stress-tested it across every edge case. FFmpeg encodes video for YouTube, Netflix, and countless other services—its optimizations are world-class.

Building custom alternatives would be lighter weight initially, but:
- Custom terminal rendering would take years to match xterm.js quality
- Custom video encoding would never match FFmpeg's compression
- Custom browser automation would reimplement what Playwright provides

**The trade-off**: VCR# requires more dependencies and has higher startup overhead (~2-3 seconds) compared to simpler approaches. But it gains cross-platform stability and visual quality that would take years to build from scratch.

This composition strategy prioritizes **reliability and maintainability** over minimalism. VCR# isn't the lightest recorder—it's optimized for producing polished, consistent recordings that work everywhere.

## The Three Core Components

### ttyd: Terminal Server

**Role:** Exposes a real terminal session through a web browser.

**Why we use it:**
- Provides pixel-perfect terminal rendering via xterm.js
- Easy to automate with browser tools
- Cross-platform (works on Windows, Linux, macOS)
- Supports any shell (PowerShell, Bash, Zsh, CMD)

**How VCR# uses it:**
1. Spawns ttyd process with specified shell
2. ttyd binds to a random localhost port
3. Serves xterm.js terminal in a web page
4. VCR# connects Playwright to this URL

### Playwright: Browser Automation

**Role:** Controls the browser, sends input, and captures screenshots.

**Why embrace browser-based automation:**

At first, using a full browser to record terminal sessions might seem like overkill. But this apparent overhead buys us something valuable: the battle-tested rendering of xterm.js, the same terminal emulator that powers VS Code and countless web-based terminals.

Rather than reinventing terminal emulation (and inevitably hitting edge cases with fonts, colors, Unicode, ligatures, and emoji across different platforms), we leverage years of community refinement. The browser gives us:
- Production-quality terminal rendering without years of development
- CSS injection for themes and styling without custom rendering code
- Pixel-perfect screenshot API for exact visual representation
- Mature keyboard/mouse automation for realistic interaction

**How VCR# uses it:**
1. Launches headless Chromium browser
2. Navigates to ttyd URL
3. Injects theme CSS and configuration
4. Sends keyboard input via Playwright API
5. Takes screenshots at specified framerate
6. Saves PNG frames to disk

### FFmpeg: Video Encoding

**Role:** Converts PNG frame sequences into final video formats.

**Why we use it:**
- Industry-standard video encoding
- Supports GIF, MP4, WebM, and more
- Advanced palette optimization for GIFs
- H.264 and VP9 codec support

**How VCR# uses it:**
1. Receives directory of PNG frames
2. For GIFs: generates optimized color palette
3. Encodes frames into requested formats
4. Applies compression and quality settings

## The Recording Pipeline

VCR#'s recording process follows six distinct phases:

### Phase 1: Parse

**Input:** `.tape` file
**Output:** List of `ICommand` objects (AST)

The `TapeParser` uses Sprache parser combinators to:
- Tokenize the tape file
- Build Abstract Syntax Tree (AST)
- Create typed command objects (`TypeCommand`, `ExecCommand`, etc.)
- Validate syntax

### Phase 2: Initialize

**Setup tasks:**
- Start ttyd process with selected shell
- Launch Playwright browser (headless Chromium)
- Navigate to ttyd URL (e.g., `http://localhost:8765`)
- Wait for terminal to be ready
- Inject theme CSS and terminal configuration

### Phase 3: Execute

**Command execution loop:**
Each command's `ExecuteAsync()` method runs sequentially:
- Configuration commands (`Set`, `Output`) update session state
- Input commands (`Type`, `Enter`) send keyboard events
- Wait commands (`Wait`, `Sleep`) pause for output or time
- Exec commands run real shell commands

**ExecutionContext** provides access to:
- `ITerminalPage` (Playwright terminal interface)
- `IFrameCapture` (screenshot system)
- `SessionState` (configuration and runtime state)

### Phase 4: Capture

**Concurrent frame capture:**
While commands execute, a background thread captures frames:
- Takes screenshots at configured framerate (default 50fps)
- Writes PNG files to temporary directory
- Uses `FrameWriteQueue` for concurrent disk writes
- Respects `Hide`/`Show` commands (skips capture when hidden)

### Phase 5: Trim

**Post-capture processing:**
After command execution finishes:
- `FrameTrimmer` removes blank frames at start/end
- Keeps `StartBuffer` time before first activity
- Keeps `EndBuffer` time after last activity
- Results in cleaner, more polished output

### Phase 6: Encode

**Video generation:**
For each `Output` command:
- `VideoEncoder` invokes FFmpeg
- Format detected from file extension (`.gif`, `.mp4`, `.webm`)
- Format-specific encoding applied
- Output file written to specified path

## Data Flow

### Tape File → Commands

```
demo.tape
  ↓ [TapeParser]
List<ICommand>
  ↓
[TypeCommand, WaitCommand, ExecCommand, ...]
```

### Commands → Terminal

```
TypeCommand.ExecuteAsync()
  ↓
Playwright.Keyboard.TypeAsync()
  ↓
Browser keyboard events
  ↓
xterm.js terminal
  ↓
ttyd WebSocket
  ↓
Shell process (bash/pwsh/cmd)
```

### Terminal → Frames

```
Terminal renders output
  ↓
Playwright.Page.ScreenshotAsync()
  ↓
PNG bytes
  ↓
FrameWriteQueue
  ↓
frame0001.png, frame0002.png, ...
```

### Frames → Video

```
PNG sequence
  ↓
FFmpeg
  ↓
[GIF: palettegen → paletteuse]
[MP4: libx264 + yuv420p]
[WebM: libvpx-vp9]
  ↓
output.gif / output.mp4 / output.webm
```

**Notice the transformation chain**: text → terminal rendering → pixels → frames → video. Each transformation introduces potential loss (text semantics become purely visual), but gains universal compatibility. This is why VCR# outputs can embed anywhere—unlike text-based formats that need custom players.

## Key Design Decisions

### Why Web-Based Terminal?

**Alternatives considered:**
- Direct terminal emulation in .NET
- Screen scraping actual terminal windows

**Why ttyd + xterm.js:**
- **Production quality**: xterm.js is battle-tested (used by VS Code, Hyper, etc.)
- **Cross-platform**: Works identically on Windows/Linux/macOS
- **Themeable**: CSS injection allows full customization
- **Automation-friendly**: Playwright provides robust browser automation

### Why Browser Automation?

**Alternatives considered:**
- Terminal recording libraries (asciinema, script)
- Direct frame buffer capture

**Why Playwright:**
- **Pixel-perfect screenshots**: Captures exactly what users see
- **Reliable automation**: Mature API for keyboard/mouse input
- **CSS control**: Theme injection, font customization
- **Headless**: No visible browser window needed

### Why Frame-Based Approach?

**Alternatives considered:**
- Direct video capture (screen recording)
- Terminal escape sequence recording

**Why PNG frames + FFmpeg:**
- **Quality control**: Each frame is a lossless PNG before compression
- **Post-processing**: Can trim, edit, adjust frames after capture
- **Format flexibility**: Generate GIF, MP4, WebM from same frames
- **Deterministic**: Exact framerate, no dropped frames

This frame-based approach trades storage efficiency (PNG frames are large) for quality control and format flexibility. Notice how VCR# separates concerns: frame capture focuses on perfect timing, encoding focuses on compression. Combining these would risk dropped frames during encoding.

## Performance Considerations

### Frame Capture Performance

- Screenshots happen concurrently with command execution
- `FrameWriteQueue` batches writes to avoid I/O bottleneck
- PNG compression is fast (built into Playwright/Chromium)

**Typical performance:**
- 50fps capture = 20ms between frames
- Screenshot takes ~5-10ms
- Plenty of headroom for parallel execution

### Memory Management

- Frames written to disk immediately (not buffered in RAM)
- Temporary directory auto-cleaned after encoding
- Browser runs headless (low memory overhead)

### Encoding Speed

- FFmpeg encoding is usually faster than recording
- GIF: 10-30 seconds for typical recording
- MP4/WebM: 5-15 seconds

## Platform Support

VCR# works identically across platforms thanks to:

**Cross-platform components:**
- .NET 9 (runs on Windows, Linux, macOS)
- ttyd (available for all platforms)
- Playwright (Chromium works everywhere)
- FFmpeg (universal)

**Platform-specific handling:**
- **Shell detection**: Auto-selects pwsh/cmd (Windows) or bash (Unix)
- **Path handling**: Normalizes path separators
- **Font rendering**: Uses platform fonts for monospace

## Comparison with Other Approaches

### vs. asciinema

**asciinema:**
- Records terminal escape sequences (text-based)
- Very small file sizes
- Limited styling control
- JSON output format

**VCR#:**
- Records pixel-perfect screenshots (visual-based)
- Larger files, but more compatible
- Full theme/font customization
- Standard video formats (GIF/MP4/WebM)

### vs. VHS (Charm Bracelet)

**VHS:**
- Similar tape file syntax
- ttyd-based
- No real command execution (Type only)
- Go language

**VCR#:**
- Inspired by VHS
- Also uses ttyd
- Real command execution via Exec
- C#/.NET stack

### vs. TermRecord

**TermRecord:**
- Records to self-hosted JavaScript player
- Python-based
- Limited export formats

**VCR#:**
- Standard GIF/MP4/WebM output
- No player required
- Embeddable anywhere

## Three-Layer Architecture

VCR# follows clean architecture principles:

### VCR#.Core (Domain Layer)

- Tape parsing (TapeParser, TapeLexer)
- AST representation (ICommand, TypeCommand, WaitCommand, etc.)
- Session management (SessionOptions, SessionState)
- Theme definitions (BuiltinThemes, Theme)
- **No external dependencies** (pure domain logic)

### VCR#.Infrastructure (Integration Layer)

- Playwright browser automation (PlaywrightBrowser, TerminalPage)
- ttyd process management (TtydProcess)
- Frame capture and storage (FrameCapture, FrameStorage)
- Video encoding (VideoEncoder - FFmpeg wrapper)
- Activity monitoring (ActivityMonitor)

### VCR#.Cli (Application Layer)

- CLI commands (RecordCommand, ValidateCommand, ThemesCommand)
- User interface (Spectre.Console)
- Entry point and orchestration

**Dependency direction:** CLI → Infrastructure → Core
(Core has no dependencies, Infrastructure depends on Core, CLI depends on both)

This architecture makes VCR# maintainable, testable, and extensible.
