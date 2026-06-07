# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VcrSharp (the CLI is `vcr`, styled **VCR#**) is a .NET terminal recorder that turns `.tape` scripts into GIFs, MP4/WebM videos, PNGs, and (animated or static) SVGs. It is inspired by [VHS](https://github.com/charmbracelet/vhs) but adds first-class Windows support, the ability to run real commands with `Exec`, and an interactive `record` mode that works in any shell.

**The big architectural fact:** VCR# is **browserless**. There is **no ttyd, no Playwright, no Chromium, no xterm.js, no WebSocket**. VCR# launches a real shell over an in-process pseudo-terminal (ConPTY on Windows, a Unix PTY on Linux/macOS), parses the shell's output with a from-scratch VT500 terminal engine (`VtScreen`), and rasterizes the resulting cell grid directly to frames. The only external runtime dependency is **FFmpeg**, and only when encoding GIF/MP4/WebM. SVG and PNG output need no external binaries at all. (Raster output additionally needs a monospace font installed on the system; SVG does not.)

This was a ground-up rewrite (the `total-rewrite` branch / commit `0cf0885` made the native path the only backend). If you find code, docs, or comments referencing ttyd, Playwright, Chromium, browsers, or xterm.js as a *live* path, they are stale — flag and fix them.

**Status:** alpha. The API and tape grammar still change.

## Common Commands

### Build
```bash
dotnet build VcrSharp.sln
```

### Run the CLI
```bash
# Record a tape file to its declared Output target(s)
dotnet run --project src/VcrSharp.Cli -- demo.tape

# Validate a tape file (parse + resolve presets) without recording
dotnet run --project src/VcrSharp.Cli -- validate demo.tape

# List the built-in themes
dotnet run --project src/VcrSharp.Cli -- themes

# Interactively record a live shell session into a .tape file
dotnet run --project src/VcrSharp.Cli -- record demo.tape

# One-shot static SVG screenshot of a command's final output (no tape file)
dotnet run --project src/VcrSharp.Cli -- snap "ls -la" -o ls.svg

# One-shot animated SVG of a command start-to-finish (no tape file)
dotnet run --project src/VcrSharp.Cli -- capture "dotnet --version" -o ver.svg

# Mine a directory of tapes into a shared vcr.toml preset (dry run by default)
dotnet run --project src/VcrSharp.Cli -- migrate samples/ --write
```

### Testing
```bash
# Run all tests
dotnet test

# Run a single test project
dotnet test tests/VcrSharp.Core.Tests
dotnet test tests/VcrSharp.Terminal.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TapeParserTests"
```

There is **no Playwright/browser install step** anymore. The old `playwright.ps1 install chromium` instruction is gone. The terminal-engine tests run a vendored libvterm conformance corpus in-process with no external setup.

### Docs site
The docs live in `docs/VcrSharp.Docs`, a [Pennington](https://usepennington.github.io/pennington/) `DocSite` (Blazor-rendered, MonorailCSS, no Node). Content is Markdown under `Content/`, organized into four Diátaxis areas (`tutorials`, `how--to`, `explanation`, `reference`).
```bash
cd docs/VcrSharp.Docs
dotnet run                 # dev-serve with hot reload
dotnet run -- build        # static build; exits non-zero on any error/broken link
dotnet run -- diag warnings  # report broken links / unresolved xrefs
```

## Architecture

### Four projects

1. **VcrSharp.Core** — domain logic, parsing, and the cell-grid contract. No external-process or platform code.
   - `.tape` parsing with Superpower combinators (`Parsing/TapeTokenizer.cs`, `TapeParser.cs`, `TapeToken.cs`)
   - AST command nodes (`Parsing/Ast/*.cs`), all implementing `ICommand`
   - Session config and runtime state (`Session/SessionOptions.cs`, `SessionState.cs`), shell resolution (`Session/ShellConfiguration.cs`)
   - Terminal-grid DTOs the renderers consume (`Rendering/TerminalCell.cs`, `TerminalContent.cs`, `ContentExtent.cs`)
   - The config layer: `vcr.toml` model + reader + preset resolver + tape migrator (`Config/*.cs`)
   - Themes (`Settings/BuiltinThemes.cs`, `Theme.cs`), deprecation table (`Settings/SettingDeprecations.cs`)
   - Interactive-recording conversion (`Recording/InputEvent.cs`, `InputToTapeConverter.cs`)

2. **VcrSharp.Terminal** — the from-scratch terminal engine. A standalone library whose single public type, `VtScreen`, is a VT500-series emulator (canonical Paul Williams state machine). It feeds on a byte/char stream and maintains a `Cols×Rows` grid of styled cells; `ToTerminalContent()` snapshots that grid into the Core DTOs. It is conformance-tested against a vendored libvterm corpus. Depends only on Core (for the `TerminalContent`/`TerminalCell` types).

3. **VcrSharp.Infrastructure** — platform integration and rendering.
   - PTY backends behind a neutral seam: `Terminal/IPtyProcess.cs` (factory `PtyProcess.Start`), `Terminal/ConPtyProcess.cs` (Windows ConPTY), `Terminal/UnixPtyProcess.cs` (`posix_openpt` + `posix_spawn`)
   - The terminal surface: `Terminal/TerminalPage.cs` (`ITerminalPage` over PTY+grid), `Terminal/FrameCapture.cs` (`IFrameCapture`), `Terminal/KeyMap.cs` (key → raw terminal bytes), `Terminal/InteractiveRecorder.cs` (`vcr record`), `Terminal/TerminalRenderer.cs` (standalone run-and-snapshot helpers)
   - The orchestrator: `Session/RecordingSession.cs`
   - Rendering: `Rendering/RasterRenderer.cs` (ImageSharp/SixLabors.Fonts → frames), `Rendering/SvgRenderer.cs` (text SVG, static + animated) with `Rendering/CustomGlyphRenderer.cs`, `Rendering/VideoWriter.cs` (FFmpeg via FFMpegCore), `Rendering/SvgWriter.cs`
   - `Processes/DependencyValidator.cs` (checks only FFmpeg, only when needed)

4. **VcrSharp.Cli** — the `vcr` CLI, built on Spectre.Console.Cli (`Program.cs` + `Commands/*.cs`).

### Recording flow (`vcr <tape>`)

1. **Parse** — `TapeParser` reads the `.tape` into a list of `ICommand` AST nodes.
2. **Resolve config** — `Config/PresetResolver.ResolveWithDiscovery` walks up from the tape's directory to find a `vcr.toml`, then expands `Use` presets, the `Exec name arg` macro form, and `Run` sugar, and derives `Output` from a preset's `outDir`/`output`. CLI `--set`/`--output` overrides are then applied.
3. **Launch** — `RecordingSession` decides the launch shape from **`Exec` presence** (`ShouldUseBareRepl` = no `Exec`):
   - **No `Exec`** → launch a bare interactive REPL; the tape `Type`s its own visible command line.
   - **Has `Exec`** → join the `Exec` command(s) and run them as the shell's hidden foreground process (the launch line is never echoed); later `Type`/`Key` flow over the PTY to that app.
   It opens the PTY (`PtyProcess.Start`), creates a `VtScreen`, and starts a background drain thread reading PTY output → `screen.Feed(...)`.
4. **Capture** — capture is **event-driven**: after every output chunk the drain thread snapshots the grid and appends it if it differs from the last frame (de-duped by a content fingerprint). This is the "never miss a frame" design — periodic sampling would drop transient TUI states. Key presses are paced ~24 ms (`KeyPaceMs`) so a TUI has time to redraw between keys.
5. **Execute** — the command loop runs the action commands (`Type`, `Key`, `Modifier`, `Wait`, `Hide`/`Show`, `Sleep`, `Screenshot`) against an `ExecutionContext`. `Set`/`Output`/`Env`/`Use`/`Exec` are no-ops at this stage — their effect was realized before/around the loop.
6. **Settle** — after the loop, wait for output to stabilize and force-append a final frame so the recording always ends on finished output.
7. **Encode** — per `Output` path, by extension: `.svg` → `SvgWriter` (animated) or `RenderStaticAsync` (when `Mode static`); `.gif`/`.mp4`/`.webm` → `VideoWriter` (rasterize frames + FFmpeg); `.png` → single final raster frame; extension-less path → a PNG frame sequence + concat manifest.

### Interactive recording flow (`vcr record`)

`InteractiveRecorder.RecordAsync` authors a `.tape` from a live session instead of playing one back:

1. Launch the user's shell interactively over a PTY (no `Exec`).
2. Put the host console in raw/VT pass-through mode (`ConsoleRawMode`: console-mode flags on Windows; `stty raw -echo < /dev/tty` on Unix). Falls back to cooked mode if raw can't be set.
3. Run two pump threads: **output** (PTY → host stdout, so the user sees the live shell) and **input** (host stdin → PTY, recording every read chunk as a timestamped `InputEvent`). Because a multi-byte keystroke (an arrow's `ESC [ A`, `Alt+char`) arrives in one read, escape sequences stay intact.
4. The session ends when the shell exits (`exit` / Ctrl+D).
5. `InputToTapeConverter.Convert` turns the captured input byte-stream into tape text — coalescing `Type`, mapping keys/modifiers, inserting `Sleep` from real pauses, stripping the trailing `exit`. This is shell-agnostic because it operates on *input*, not output. (This converter is unchanged from before; only the capture mechanism — console pumps over a PTY — replaced the old browser hook.)

### Command architecture

All tape commands implement `ICommand`:
```csharp
Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken);
```
`ExecutionContext` exposes `ITerminalPage` (the PTY-backed `TerminalPage`), `IFrameCapture` (`FrameCapture`), `SessionOptions`, and `SessionState`.

Command nodes in `Parsing/Ast/`:
- **Config (no-op at runtime, applied before/around the loop):** `SetCommand`, `OutputCommand`, `EnvCommand`, `UseCommand`, `ExecCommand`
- **Input (real actions):** `TypeCommand`, `KeyCommand`, `ModifierCommand`
- **Control (real actions):** `SleepCommand`, `WaitCommand`, `HideCommand`, `ShowCommand`
- **Utility (real action):** `ScreenshotCommand` (PNG and SVG)
- **Sugar (desugared by `PresetResolver` before the loop):** `RunCommand` (→ `Type` + `Enter` + `Wait`)

`Exec`'s effect happens outside the loop: literal `Exec` text is baked into the shell launch line; the `Exec name arg` macro form is expanded to a literal `Exec` by `PresetResolver` against a `[macro]` template in `vcr.toml`.

> **Removed commands:** `Copy`, `Paste`, `Require`, and `Source` were deleted from the grammar. They are parse errors now — do not reference them.

### Key components

- **`VtScreen`** (`VcrSharp.Terminal`) — the in-process VT500 engine. Implements the full SGR attribute model (bold/dim/italic/underline incl. styled + colored, reverse/conceal/strike/overline/blink, 16/256/truecolor), DEC private modes, alternate screen, scroll regions, line editing, tab stops, cursor save/restore, content-preserving resize, UTF-8 + grapheme/emoji width, and DEC line-drawing. Crash-proof on pathological input (a hard fuzz gate). True reflow-on-resize is deliberately deferred — the largest remaining conformance gap.

- **`RecordingSession`** (Infrastructure) — the orchestrator: chooses the launch shape, runs the PTY drain + event-driven capture, runs the command loop, settles, and routes each `Output` to the right writer.

- **`TerminalPage`** (Infrastructure) — `ITerminalPage` over the PTY + grid. `Type`/`Key`/`Modifier` write bytes to PTY stdin (the shell echoes them back through the parser); `Wait` polls the grid text against a regex (and treats a non-interactive `Exec` shell exiting as "Wait satisfied"); `Hide`/`Show` toggle capture and cursor visibility.

- **`PtyProcess` / `ConPtyProcess` / `UnixPtyProcess`** (Infrastructure) — the only platform-specific code. Everything above the `IPtyProcess` seam (engine, grid, renderers, commands, capture) is platform-neutral.

- **`PresetResolver`** (Core) — expands the whole config layer (`Use`, `Run`, `Exec` macros, derived `Output`) into primitive commands before recording. The engine never sees presets/macros. Fast-paths (returns input unchanged) when a tape uses none of them.

- **`TapeMigrator`** / **`MigrateCommand`** — mine a directory of legacy tapes for shared "house style" `Set` lines, emit a `vcr.toml` preset, and rewrite each tape to `Use` it. Every rewrite is equivalence-checked (identical realized config + action sequence) so a migration provably never changes behavior; it also rewrites deprecated `StaticOutput`/`FitToContent` into `Mode`/`Size`.

- **`RasterRenderer`** (Infrastructure) — renders a `TerminalContent` grid to a raster image with SixLabors.ImageSharp + SixLabors.Fonts (CPU). Resolves a real monospace system font (Cascadia Mono/Code, Consolas, Courier New, DejaVu Sans Mono, …) and throws an actionable error if none exists. Used for GIF/MP4/WebM/PNG.

- **`SvgRenderer`** (Infrastructure) — renders the grid to text-based SVG (static via `RenderStaticAsync`, animated via `RenderAnimatedAsync`). Box-drawing/block/powerline glyphs are emitted as SVG paths (`CustomGlyphRenderer`), so **SVG needs no installed font**. Embedding options (all SVG-only): `Set Loop false`/`Set LoopCount N` (finite reveal that holds), `Set Size fit` (crop to content extent), `Set SvgIntrinsicSize`/`Set SvgMetadata` (explicit `width`/`height` + `data-*`, both default on). SVG honors `Padding` only; it ignores `Margin`/`MarginFill`/`WindowBarSize`/`BorderRadius`.

- **`VideoWriter`** (Infrastructure) — rasterizes captured frames and drives FFmpeg (via FFMpegCore) for GIF (palettegen/paletteuse), MP4 (libx264), and WebM (libvpx-vp9, alpha). Also writes the extension-less frame directory (PNG sequence + concat manifest).

- **`DependencyValidator`** (Infrastructure) — checks **only** for FFmpeg, and `RecordCommand` only calls it when an `Output` is `.gif`/`.mp4`/`.webm`. SVG/PNG/frames-only tapes need no external binaries.

## CLI commands

| Command | Purpose | Key options |
|---|---|---|
| `vcr <tape>` (default) | Record/play a tape to its `Output` target(s) | `-v/--verbose`, `--set KEY=VALUE` (repeatable), `-o/--output FILE` (repeatable) |
| `vcr validate <tape>` | Parse + resolve presets and report command counts; no recording | — |
| `vcr themes` | List the built-in themes (name/bg/fg) | — |
| `vcr migrate <dir>` | Extract shared `Set` lines into `vcr.toml`, rewrite tapes to `Use` it (dry run by default) | `--write`, `--preset NAME` (def `doc`), `--threshold FRACTION` (def `0.6`), `-r/--recursive` |
| `vcr snap <command>` | Static SVG screenshot of a command's final output (forces `.svg`, fit-to-content) | `-o/--output`, `--theme`, `--cols`, `--rows`, `--font-size`, `--disable-cursor`, `--transparent-background`, `--end-buffer`, `-v` |
| `vcr capture <command>` | Animated SVG recording of a command's output (forces `.svg`) | same as `snap` |
| `vcr record [tape]` | Interactively record keystrokes in a real shell → `.tape` (default `recording.tape`) | `--shell`, `--theme`, `--cols`, `--rows`, `--font-size`, `-v` |

## Dependencies

### Runtime requirements
- **.NET 10** runtime (the tool is a `dotnet tool`, command `vcr`).
- **FFmpeg** on PATH — **only** for GIF/MP4/WebM output. SVG/PNG/frames need it not.
- A **monospace font** installed — only for raster output (GIF/MP4/WebM/PNG). Usually already present (Windows: Consolas/Cascadia; Linux: DejaVu Sans Mono). SVG output needs none.
- ~~ttyd~~, ~~Playwright/Chromium~~ — **removed**. Not dependencies.

### Key NuGet packages
- **Superpower** (parser combinators) — Core
- **SixLabors.ImageSharp** + **SixLabors.ImageSharp.Drawing** (raster/font rendering) — Core
- **FFMpegCore** (FFmpeg wrapper, the only external-binary-backed package) — Infrastructure
- **Spectre.Console** / **Spectre.Console.Cli** + **Errata** (CLI UI) — Cli
- **Serilog** (logging) — Core/Cli

There is **no `Microsoft.Playwright`** reference.

## Project structure

```
src/
├── VcrSharp.Core/            # Parsing, AST, SessionOptions, config (vcr.toml), themes, grid DTOs
├── VcrSharp.Terminal/        # VtScreen — the from-scratch VT500 engine
├── VcrSharp.Infrastructure/  # PTY backends, RecordingSession, renderers, FFmpeg writer
└── VcrSharp.Cli/             # `vcr` CLI commands (Spectre.Console.Cli)

tests/
├── VcrSharp.Core.Tests/      # Parser, settings, config/preset/migrate, native-launch tests
└── VcrSharp.Terminal.Tests/  # VtScreen unit tests + vendored libvterm conformance corpus

docs/
├── VcrSharp.Docs/            # Pennington DocSite (Content/ = tutorials/how--to/explanation/reference)
└── vt-conformance-scoreboard.md  # auto-generated by the conformance tests

samples/                      # Example .tape files (and samples/docs/* feed the docs SVGs)
```

## Implementation details

### Tape grammar (current)

- **Settings:** `Set <Name> <value>` (value = quoted string, duration, number, bool, or bare word). Must appear before any action command; no duplicates; unknown name is a parse error with a "Did you mean…?" suggestion.
- **Output:** `Output <path>` — format by extension (`.svg`/`.gif`/`.mp4`/`.webm`/`.png`, or a directory for raw frames).
- **Input:** `Type[@speed] "text"`; special keys (`Enter`, `Tab`, `Backspace`, `Delete`, `Escape`, arrows, `Home`, `End`, `PageUp/Down`, `Insert`, `Space`) with optional `@speed` and a trailing repeat count (e.g. `Backspace 5`, `Down@100ms 3`); modifier chords (`Ctrl+C`, `Alt+Enter`, `Ctrl+Alt+Shift+Tab`).
- **Control:** `Sleep <duration>`; `Wait[+Buffer|+Line|+Screen][@timeout] [/regex/]`; `Hide` / `Show` (stop/resume frame capture — commands keep executing while hidden).
- **Execution:** `Exec "cmd"` (run a real command) and `Exec name arg` (expand a `[macro]` from `vcr.toml`).
- **Environment:** `Env KEY "value"`.
- **Config sugar:** `Use <preset>` (apply a `vcr.toml` preset); `Run "cmd"` (= `Type` + `Enter` + `Wait`).

### Settings worth knowing

- **`Mode`** = `animated` (default) | `static`. `static` runs `Exec`, lets output settle, and emits one static frame per `Output` (`.svg`/`.png` only). Supersedes the deprecated `Set StaticOutput true`.
- **`Size`** = `grid` (default) | `fit`. `fit` crops SVG to measured content extent. Supersedes the deprecated `Set FitToContent true`.
- **`HoldDuration`** is an alias for `EndBuffer`.
- Both deprecated booleans (`StaticOutput`, `FitToContent`) still parse (with a warning) so `vcr migrate` can read legacy tapes.
- **Do not document or use `Set CssVariables`** — despite a lingering `SessionOptions.CssVariables` property, it is **not** in `ValidSettingNames`, so `Set CssVariables …` is a parse error.

### `vcr.toml` (config layer)

Discovered by walking up from the tape's directory. A deliberate TOML subset with two section kinds:
```toml
[preset.doc]            # pulled in by `Use doc`
theme = "one dark"
cols = 82
transparentBackground = true
endBuffer = 5s
outDir = "assets"      # reserved: derive Output = assets/<tape>.svg

[preset.landing]
inherits = "doc"       # reserved: inherit base-first
mode = static          # migrator emits Mode/Size, not StaticOutput/FitToContent
output = "assets/{name}.png"  # reserved: explicit template, {name} = tape basename

[macro]
showcase = "dotnet run --no-build showcase {0}"   # Exec showcase <arg>
```
Reserved keys: `inherits`, `outDir`, `output`. Every other key is a setting (validated against the tape `Set` allow-list at resolve time). Presets/macros/`Use`/`Run` never reach the engine — `PresetResolver` expands them into `Set`/`Output`/`Type`/`Key`/`Wait`/literal `Exec`.

### Wait scopes
- `Wait+Buffer` (default): search output accumulated since the last `Wait`.
- `Wait+Line`: current line only.
- `Wait+Screen`: the entire visible grid.

### Exec vs Type
- `Type`: simulates character-by-character keyboard input (the typed text is part of the demo).
- `Exec`: runs a real command as the shell's hidden foreground process. The launch shape keys off **`Exec` presence**, not Type/Key. A tape with `Exec` never echoes the launch line; a tape without `Exec` types its own visible command line into a bare REPL.

### Screenshot
`Screenshot <path>` captures a single frame mid-recording. `.png` → `RasterRenderer`; `.svg` → `SvgRenderer`. With `Set ScreenshotWaitForInactivity true`, capture waits for output to settle first (`ScreenshotInactivityTimeout`).

### Themes
Built-in themes in `Settings/BuiltinThemes.cs` define foreground/background/ANSI palettes. Applied by the native renderers (raster + SVG) — there is no CSS injection anymore. `vcr themes` lists them; unknown names fall back to `Default`.

## Common patterns

### Adding a new tape command
1. Create a class in `VcrSharp.Core/Parsing/Ast/` implementing `ICommand`.
2. Add a parser rule in `Parsing/TapeParser.cs` (and a token in `TapeTokenizer.cs` if it needs a new keyword).
3. Implement `ExecuteAsync` against `ITerminalPage`/`IFrameCapture` (or make it a no-op if its effect is realized before the loop, like `Set`/`Output`/`Exec`).
4. If it should be expanded before recording (like `Run`), handle it in `Config/PresetResolver.cs`.
5. Add tests in `tests/VcrSharp.Core.Tests/Parsing/`.

### Adding a new setting
1. Add a property (with default) to `Session/SessionOptions.cs`.
2. Add the name to `ValidSettingNames` in `Parsing/TapeParser.cs`.
3. Add an apply case in `SessionOptions.ApplySetting`.
4. Add validation in `SessionOptions.Validate` if needed.
5. Add parser tests.

Note: setting names may begin with a command keyword (e.g. `WaitTimeout`, `ScreenshotWaitForInactivity`). `TapeTokenizer.Keyword()` guards the right boundary so such names tokenize as a single identifier.

### Extending the terminal engine
`VtScreen` is conformance-driven. The vendored libvterm corpus lives in `tests/VcrSharp.Terminal.Tests/Conformance/LibVterm/`; the harness (`LibVtermHarness.cs`) decodes the Perl byte literals and asserts against the grid snapshot. `EngineDoesNotCrash` + `FuzzTests` are hard robustness gates. The `Scoreboard` test regenerates `docs/vt-conformance-scoreboard.md`. When adding VT behavior, add or enable the relevant corpus assertions and keep the crash gate green.

## Platform considerations
- **Windows**: primary platform. PTY = ConPTY (`CreatePseudoConsole`, Windows 10 1809+). Default shell `pwsh` → `powershell` → `cmd`.
- **Linux/macOS**: PTY = `posix_openpt` + `posix_spawn` (managed `fork`/`exec` is unsafe in a multi-threaded CLR). Default shell `bash`.
- Everything above the `IPtyProcess` seam is identical across platforms; only PTY launch and raw-console plumbing differ. `Set Shell` is resolved through `Session/ShellConfiguration.cs` (bash/zsh/fish/sh/cmd/pwsh/powershell), which also supplies a clean `> ` prompt and profile/rc suppression.
