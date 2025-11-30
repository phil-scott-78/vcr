# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VcrSharp is a .NET terminal recorder that generates GIFs and videos from `.tape` files. It is inspired by VHS but adds better Windows support and the ability to execute real commands via the `Exec` command rather than just simulating typing.

**Important**: This project is in early development (alpha quality). The API will change rapidly.

## Common Commands

### Build
```bash
dotnet build VcrSharp.sln
```

### Run the CLI
```bash
# Record a tape file
dotnet run --project src/VcrSharp.Cli -- demo.tape

# Validate a tape file without recording
dotnet run --project src/VcrSharp.Cli -- validate demo.tape

# List available themes
dotnet run --project src/VcrSharp.Cli -- themes
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/VcrSharp.Core.Tests
dotnet test tests/VcrSharp.Integration.Tests

# Run single test (example)
dotnet test --filter "FullyQualifiedName~TapeParserTests"
```

### Integration Test Setup
Integration tests require Playwright browsers. Install them after building:
```bash
pwsh tests/VcrSharp.Integration.Tests/bin/Debug/net9.0/playwright.ps1 install chromium --no-shell
```

## Architecture

### Three-Layer Architecture

1. **VcrSharp.Core** - Domain logic and parsing
   - `.tape` file parsing using Sprache parser combinators (TapeParser, TapeLexer)
   - AST representation of commands (all implement `ICommand` interface)
   - Session state management and configuration (SessionState, SessionOptions)
   - Theme definitions (BuiltinThemes, Theme)

2. **VcrSharp.Infrastructure** - External integrations
   - Playwright browser automation (PlaywrightBrowser, TerminalPage)
   - ttyd process management (TtydProcess)
   - Frame capture and storage (FrameCapture, FrameStorage, FrameWriteQueue)
   - Video encoding with FFmpeg (VideoEncoder)
   - Activity monitoring and inactivity detection (ActivityMonitor)

3. **VcrSharp.Cli** - CLI application
   - Commands: RecordCommand, ValidateCommand, ThemesCommand
   - User interface with Spectre.Console

### Recording Flow

1. **Parse**: TapeParser reads `.tape` file and produces a list of `ICommand` objects
2. **Initialize**: VcrSession starts ttyd (terminal server) with EXEC commands as startup script, then launches Playwright browser
3. **Execute**: Commands execute sequentially via `ExecuteAsync(ExecutionContext, CancellationToken)` (EXEC commands are skipped - already running in background from ttyd startup)
4. **Capture**: FrameCapture takes screenshots during execution, FrameStorage manages them
5. **Trim**: FrameTrimmer removes blank frames at start/end
6. **Encode**: VideoEncoder routes to format-specific encoders (GIF/MP4/WebM via FFmpeg, raw Frames, or SVG)

### Command Architecture

All tape commands implement `ICommand` with a single method:
```csharp
Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken);
```

ExecutionContext provides:
- ITerminalPage (browser terminal interface)
- IFrameCapture (screenshot capture)
- SessionState (session configuration and runtime state)

Command types in `VcrSharp.Core/Parsing/Ast/`:
- Configuration: SetCommand, OutputCommand
- Input: TypeCommand, KeyCommand, ModifierCommand (e.g., Ctrl+C)
- Control: SleepCommand, WaitCommand, HideCommand, ShowCommand
- Execution: ExecCommand (runs real shell commands)
- Utility: CopyCommand, PasteCommand, ScreenshotCommand (supports PNG and SVG formats)
- Environment: EnvCommand, RequireCommand, SourceCommand

### Key Components

**TapeParser** (Core): Sprache-based parser that transforms `.tape` syntax into `ICommand` AST nodes.

**VcrSession** (Infrastructure): Main orchestrator that manages the entire recording lifecycle - ttyd, browser, terminal, frame capture, command execution, and video encoding.

**TerminalPage** (Infrastructure): Playwright-based terminal interface that wraps browser automation for xterm.js. Handles keyboard input, screen scraping, and waiting for output patterns.

**ExecCommand** (Core): Passes shell commands to ttyd as startup script arguments. Commands execute in background while recording captures output. Does not execute during tape playback (ExecuteAsync is a no-op).

**ActivityMonitor** (Infrastructure): Monitors terminal output by polling buffer every 20ms to detect output stabilization. Tracks when content stops changing based on configurable inactivity timeout.

**FrameStorage** (Infrastructure): Manages captured PNG frames during recording, handles concurrent writes via FrameWriteQueue, and provides cleanup.

**VideoEncoder** (Infrastructure): Orchestrator that routes encoding requests to format-specific encoders. Uses interface-based architecture with IEncoder implementations for each format (GIF, MP4, WebM, PNG, Frames, SVG).

**SvgRenderer** (Infrastructure): Shared SVG rendering component used by both SvgEncoder (for animated recordings) and FrameCapture (for static screenshots). Renders terminal content as text-based SVG with full styling support.

## Dependencies

### Runtime Requirements
- .NET 9 SDK
- ttyd >= 1.7.2 (terminal server)
- FFmpeg (video encoding)
- Playwright browsers (auto-installed on first run)
  - **Note**: As of version 0.0.11+, all platform drivers (Windows, macOS, Linux for x64 and ARM64) are bundled in the package
  - Package size: ~15-20MB (increased from ~5MB to ensure cross-platform compatibility)
  - Previous versions only included drivers for the build platform, causing installation failures on other platforms

### Key NuGet Packages
- Sprache (parser combinators)
- Playwright (browser automation)
- Spectre.Console (CLI UI)
- SkiaSharp (image processing)

## Project Structure

```
src/
├── VcrSharp.Cli/              # CLI entry point, commands
├── VcrSharp.Core/             # Domain logic, parsing, commands, session state
└── VcrSharp.Infrastructure/   # External integrations (Playwright, FFmpeg, ttyd)

tests/
├── VcrSharp.Core.Tests/       # Unit tests for parsing and domain logic
└── VcrSharp.Integration.Tests/ # End-to-end recording tests
```

## Important Implementation Details

### Parser Implementation
Uses Sprache parser combinators for `.tape` file parsing. TapeParser defines grammar rules that build ICommand objects. Parser is recursive-descent and supports comments, quoted strings, duration literals (e.g., "500ms", "2s"), and regex patterns for Wait commands.

### Wait Command Scopes
Wait command has three scopes for pattern matching:
- `Wait+Buffer` (default): Search in output accumulated since last Wait
- `Wait+Line`: Search current line only
- `Wait+Screen`: Search entire visible terminal screen

### Frame Capture Strategy
FrameCapture takes screenshots at specified framerate during recording. Hide/Show commands control whether frames are captured (commands still execute when hidden). FrameTrimmer removes blank frames at start/end based on StartBuffer/EndBuffer settings.

### Exec vs Type
- `Type`: Simulates character-by-character keyboard input (for demos)
- `Exec`: Passes commands to ttyd startup script. Commands execute in background as shell starts, recording captures real output. VcrSession waits for output stabilization before ending.

### Screenshot Command
The Screenshot command captures a single frame at any point during recording. Format is auto-detected from file extension:
- **PNG** (`.png`): Raster screenshot using Playwright's built-in screenshot capability. Produces pixel-perfect images of the terminal. Always captures what is visually displayed.
- **SVG** (`.svg`): Vector screenshot using SvgRenderer. Captures terminal content with full text and styling information. Benefits:
  - Lightweight (5-50KB vs 50-500KB for PNG)
  - Scalable (vector graphics, perfect at any zoom level)
  - Searchable (actual text content, not pixels)
  - Accessible (screen readers can read text)

**Examples**:
```tape
Screenshot output.png       # PNG raster screenshot
Screenshot output.svg       # SVG vector screenshot
Screenshot samples/demo.svg # SVG with path
```

**Implementation**: ScreenshotCommand delegates to IFrameCapture.CaptureScreenshotAsync(), which is implemented by FrameCapture in Infrastructure. FrameCapture detects the file extension and routes to either TerminalPage.ScreenshotAsync() (PNG) or SvgRenderer.RenderStaticAsync() (SVG).

**When to use**:
- Charm/VHS-based TUI applications
- Vim, neovim editing sessions
- htop, btop system monitors
- Any full-screen ncurses/TUI application

**Note**: This setting only affects SVG capture (screenshots and recordings). PNG screenshots always capture the visible screen content regardless of this setting.

### Theme System
10 built-in themes in BuiltinThemes.cs. Themes define color palettes for terminal foreground/background/ANSI colors. Applied via CSS injection in PlaywrightBrowser.

### Encoder Architecture
VideoEncoder uses an interface-based architecture for output formats:
- **IEncoder**: Interface defining `RenderAsync()` and `SupportsPath()` methods
- **EncoderBase**: Abstract base class providing common functionality (frame validation, metadata access)
- **Format-specific encoders** in `Infrastructure/Rendering/Encoders/`:
  - **GifEncoder**: FFmpeg-based GIF with palette generation
  - **Mp4Encoder**: FFmpeg-based H.264 MP4
  - **WebMEncoder**: FFmpeg-based VP9 WebM (supports transparency)
  - **PngEncoder**: Single-frame PNG output
  - **FramesEncoder**: Raw PNG frames + manifest files to directory
  - **SvgEncoder**: Text-based SVG animation (uses SvgRenderer for rendering)

### SVG Rendering Architecture
**SvgRenderer** (`Infrastructure/Rendering/SvgRenderer.cs`) is a shared component that renders terminal content as SVG:
- **RenderStaticAsync()**: Generates static SVG for screenshots (no animations)
- **RenderAnimatedAsync()**: Generates animated SVG for recordings (CSS keyframe animations)
- **Shared rendering logic**: Both methods share core SVG generation code (dimension calculations, style CSS, terminal state rendering, background rendering, style run building)

**Used by**:
- **SvgEncoder**: Calls `RenderAnimatedAsync()` to generate animated SVG output from recordings
- **FrameCapture**: Calls `RenderStaticAsync()` to generate SVG screenshots via Screenshot command

**Architecture benefits**:
- Single source of truth for SVG rendering
- Consistent SVG output across animated and static use cases
- Easier maintenance and feature additions

**Adding a new encoder**:
1. Create class implementing `IEncoder` in `Infrastructure/Rendering/Encoders/`
2. Inherit from `EncoderBase` for common functionality
3. Implement `SupportsPath()` to detect output format (by extension or path characteristics)
4. Implement `RenderAsync()` to generate output
5. Register encoder in `VideoEncoder` constructor

**Output format examples**:
```tape
Output demo.gif          # GIF encoder (animated)
Output demo.mp4          # MP4 encoder (animated)
Output demo.webm         # WebM encoder (animated, supports transparency)
Output demo.svg          # SVG encoder (animated)
Output frames/           # Frames encoder (directory of PNG frames)
Output screenshot.png    # PNG encoder (single frame)

# Screenshot command examples
Screenshot frame.png     # PNG screenshot (raster)
Screenshot frame.svg     # SVG screenshot (vector)
```

## Testing Strategy

- **Core.Tests**: Focus on parser correctness, command parsing, duration parsing
- **Integration.Tests**: End-to-end recording tests with real ttyd/Playwright/FFmpeg

Run integration tests locally with caution - they create actual browser instances and video files.

## Common Patterns

### Adding New Commands
1. Create class in `VcrSharp.Core/Parsing/Ast/` implementing `ICommand`
2. Add parser rule in `TapeParser.cs`
3. Implement `ExecuteAsync` with terminal/capture logic
4. Add tests in `VcrSharp.Core.Tests/Parsing/Ast/`

### Adding New Settings
1. Add property to `SessionState` in `VcrSharp.Core/Session/SessionState.cs`
2. Add parser case in `SetCommand.cs`
3. Update command execution logic to use new setting
4. Add parser tests

### Working with Playwright Terminal
TerminalPage wraps xterm.js terminal in browser. Key methods:
- `TypeTextAsync()`: Send keyboard input
- `GetVisibleTextAsync()`: Read screen content
- `WaitForPatternAsync()`: Wait for output matching regex
- `GetBufferAsync()`: Get scrollback buffer

## Platform Considerations

- **Windows**: Primary development platform, excellent PowerShell/CMD support
- **Linux/macOS**: Fully supported, use Bash by default
- Shell detection uses ShellConfiguration/ShellConfig for platform-specific defaults
