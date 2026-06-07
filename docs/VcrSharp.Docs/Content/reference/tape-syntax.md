---
title: "Tape File Syntax Reference"
description: "Complete catalog of every command, lexical rule, and parser rule in the VCR# tape file format."
uid: "docs.reference.tape-syntax"
order: 4100
---

A tape file (`.tape`) is a plain-text script. Each command is on its own line, and commands run sequentially from top to bottom. This page catalogs every command and the lexical and parser rules that govern a tape.

For the full list of `Set` settings (names, defaults, ranges) see the [configuration options reference](xref:docs.reference.configuration-options). For the `vcr.toml` preset/macro layer used by `Use` and the `Exec` macro form, see the [vcr.toml reference](xref:docs.reference.vcr-toml). For the CLI verbs that consume tapes, see the [CLI commands reference](xref:docs.reference.cli-commands).

## Command catalog

The table below is the complete set of commands. Detailed entries follow.

| Command | Purpose |
|---------|---------|
| `Output <path>` | Declare an output target (format chosen by extension). |
| `Set <Name> <value>` | Configure a setting. Must precede all action commands. |
| `Type[@speed] "text"` | Type characters into the terminal. |
| Special keys | `Enter`, `Tab`, `Up`, etc. — single keypresses, optionally repeated. |
| Modifier chords | `Ctrl+C`, `Alt+Enter`, `Ctrl+Alt+Shift+Tab`, etc. |
| `Sleep <duration>` | Pause for a fixed duration. |
| `Wait[+Scope][@timeout] [/regex/]` | Block until output matches (or settles). |
| `Hide` / `Show` | Pause / resume frame capture. |
| `Screenshot <path>` | Capture one frame mid-recording (`.png` or `.svg`). |
| `Exec "command"` | Run a real shell command (literal form). |
| `Exec name arg` | Expand a `[macro]` from `vcr.toml` (macro form). |
| `Env KEY "value"` | Set an environment variable for the session. |
| `Use <preset>` | Apply a named preset from a discovered `vcr.toml`. |
| `Run "command"` | Sugar for `Type` + `Enter` + `Wait`. |

## Output

Declares a file the recording is written to. The format is chosen by the file extension. A path with no extension is treated as a **directory of PNG frames**.

```tape
Output "demo.svg"     # animated SVG (no FFmpeg required)
Output "demo.gif"     # animated GIF (requires FFmpeg)
Output "demo.mp4"     # H.264 MP4 (requires FFmpeg)
Output "demo.webm"    # VP9 WebM, supports transparency (requires FFmpeg)
Output "demo.png"     # single-frame PNG
Output "frames/"      # directory of PNG frames + a frames.txt manifest
```

A tape may have multiple `Output` lines to emit several formats from one run:

```tape
Output "demo.svg"
Output "demo.gif"
```

> [!NOTE]
> SVG, PNG, and the frame directory need no external binaries. FFmpeg is required only for `.gif`, `.mp4`, and `.webm`. Prefer SVG for no-setup examples.

## Set

Configures a setting. The value is a quoted string, a duration, a number, a boolean, or a bare word.

```tape
Set Theme Dracula
Set Cols 100
Set FontSize 22
Set TypingSpeed 40ms
Set TransparentBackground true
Set Mode static
```

Parser rules for `Set`:

- **All `Set` commands must come before any action command** (`Type`, a key, `Sleep`, `Wait`, `Hide`, `Show`, `Screenshot`). `Output`, `Env`, `Use`, and `Exec` are not subject to this ordering rule.
- **No setting may be set twice.** A duplicate `Set` of the same name is a parse error.
- **An unknown setting name is a parse error** with a "Did you mean…?" suggestion.

The full catalog of setting names, defaults, and ranges lives in the [configuration options reference](xref:docs.reference.configuration-options).

## Type

Types text character-by-character at the configured typing speed.

```tape
Type "echo hello"
Type@500ms "slow typing"   # override per-character speed for this command
```

The optional `@<duration>` suffix overrides the typing speed for that command only (default comes from `Set TypingSpeed`). Text uses one of the three [string quote styles](#string-literals); escape sequences such as `\n` and `\t` are interpreted only in double-quoted strings.

## Special keys

Each special key sends one keypress. Every special key accepts an optional `@<speed>` and an optional trailing **repeat count**.

```tape
Enter
Backspace 5          # press Backspace five times
Down@100ms 3         # press Down three times, 100ms apart
```

Available keys:

| | | | |
|---|---|---|---|
| `Enter` | `Space` | `Tab` | `Backspace` |
| `Delete` | `Insert` | `Escape` | `Up` |
| `Down` | `Left` | `Right` | `PageUp` |
| `PageDown` | `Home` | `End` | |

## Modifier chords

A chord is one or more modifiers — `Ctrl`, `Alt`, `Shift` — each joined with `+`, followed by a final key. Modifiers may appear in any order. The final key may be a special key or any single letter.

```tape
Ctrl+C
Alt+Enter
Shift+Tab
Ctrl+Alt+Shift+Tab
```

## Sleep

Pauses for a fixed duration. Unlike `Wait`, it does not look at output.

```tape
Sleep 1          # bare number = seconds → 1s
Sleep 500ms
Sleep 1.5m
```

## Wait

Blocks until terminal output matches a pattern, or — with no pattern — until output settles.

```tape
Wait                       # wait for output to settle (default scope: Buffer)
Wait /Complete/            # wait until "Complete" appears
Wait+Line /\$ $/           # search the current line only
Wait+Screen /done/         # search the whole visible grid
Wait@10s /ready/           # override the timeout for this command
Wait+Screen@5s /Complete/  # combine scope and timeout
```

- **Scope** (optional, default `+Buffer`):
  - `+Buffer` — output accumulated since the last `Wait`.
  - `+Line` — the current line only.
  - `+Screen` — the entire visible grid.
- **`@<timeout>`** (optional) overrides the default wait timeout (`Set WaitTimeout`, default 15s).
- **`/regex/`** (optional) overrides the default pattern (the shell prompt).

A `Wait` that times out raises an error.

## Hide / Show

`Hide` stops frame capture; `Show` resumes it. Commands keep executing while hidden — only capture is paused.

```tape
Hide
Type "secret setup"
Enter
Wait
Show
```

## Screenshot

Captures a single frame at the current point in the recording. The format is chosen by the file extension: `.svg` produces a vector screenshot, `.png` produces a raster image.

```tape
Screenshot "frame.png"   # raster
Screenshot "frame.svg"   # vector (scalable, searchable text)
```

<VcrTape src="../demos/screenshot-svg.svg" />

## Exec

Runs a real shell command, capturing actual output. `Exec` has two forms.

**Literal form** — a quoted command string:

```tape
Exec "dotnet build"
Exec "git status"
```

The command runs as the session's foreground process; its launch line is never echoed. Later `Type`/key commands flow to the running program. Multiple `Exec` lines are joined into one launch. VCR# waits for output to settle (governed by `InactivityTimeout` / `MaxWaitForInactivity`) before ending.

<VcrTape src="../demos/exec-real-command.svg" />

**Macro form** — a bare name plus an argument, expanding a `[macro]` template defined in `vcr.toml`:

```tape
Exec showcase MyDemo
```

The macro is expanded before recording. See the [vcr.toml reference](xref:docs.reference.vcr-toml) for defining `[macro]` templates and their `{0}` / `{name}` placeholders.

> [!NOTE]
> Whether a tape contains any `Exec` changes the launch shape. With **no** `Exec`, VCR# launches a plain interactive shell and types its own visible command line — the typed command is the demo. With **at least one** `Exec`, the `Exec` command runs as the hidden foreground process and the launch line is not echoed.

## Env

Sets an environment variable for the session.

```tape
Env NODE_ENV "production"
Env API_KEY "secret123"
```

## Use

Applies a named preset from a discovered `vcr.toml`. This is configuration sugar: the preset's settings are folded in before recording.

```tape
Use doc
```

The tape's own `Set` lines always win over a preset. When multiple `Use` lines are present, the later one wins; preset inheritance is applied base-first. See the [vcr.toml reference](xref:docs.reference.vcr-toml) for preset definitions, inheritance, and output templates.

## Run

Sugar for `Type "command"` followed by `Enter` and a bare `Wait` (waits for output to settle). It is expanded into those primitive commands before recording.

```tape
Run "dotnet --version"
```

is equivalent to:

```tape
Type "dotnet --version"
Enter
Wait
```

## Lexical rules

### Comments

A `#` begins a comment that runs to the end of the line and is ignored. It may start a line or follow a command.

```tape
# whole-line comment
Set Theme Dracula   # trailing comment
```

### String literals

There are three quote styles:

| Style | Example | Escapes |
|-------|---------|---------|
| Double | `"a\tb"` | Interprets `\n`, `\t`, `\r`, `\\`, `\"` |
| Single | `'C:\path'` | Literal — no escape processing |
| Backtick | `` `C:\path` `` | Literal — no escape processing |

Escape sequences interpreted in double-quoted strings:

| Sequence | Meaning |
|----------|---------|
| `\n` | Newline |
| `\t` | Tab |
| `\r` | Carriage return |
| `\\` | Backslash |
| `\"` | Double quote |

Single- and backtick-quoted strings pass their contents through verbatim — useful for Windows paths and regex-heavy text where backslashes should not be escaped.

### Numbers

Numbers are written as bare integers (`100`) or decimals (`1.5`). They are used for counts (e.g. key repeat), sizes, and the numeric part of durations.

### Duration literals

A duration is a number followed by a unit suffix, with no space between them.

| Unit | Meaning |
|------|---------|
| `ms` | Milliseconds |
| `s`  | Seconds |
| `m`  | Minutes |

```tape
Sleep 500ms
Wait@10s /ready/
Set StartWaitTimeout 1.5m
```

> [!IMPORTANT]
> A **bare number** (no unit) means **seconds** everywhere — `Sleep 1` is one second — **except** `Set TypingSpeed`, where a bare number means **milliseconds** (`Set TypingSpeed 40` = 40ms).

### Regex patterns

A regex is written between forward slashes and is used by `Wait`.

```tape
Wait /Complete/
Wait /\d{3} tests passed/
```

VCR# uses .NET regular expressions (`System.Text.RegularExpressions`). Matching is case-sensitive, single-line by default; an empty pattern (`//`) is a parse error.

## Parser rules

- **Ordering.** All `Set` commands must appear before any action command (`Type`, a key, `Sleep`, `Wait`, `Hide`, `Show`, `Screenshot`). `Output`, `Env`, `Use`, and `Exec` are exempt from this rule.
- **No duplicate settings.** Setting the same name more than once is a parse error.
- **Unknown settings.** An unrecognized `Set` name is a parse error with a "Did you mean…?" suggestion.
- **Command names are case-sensitive** — use the documented casing (`Type`, not `type`). Setting names and `true`/`false` literals are case-insensitive.
- **Whitespace.** Leading and trailing whitespace on a line is ignored; blank lines are ignored; whitespace inside string literals is preserved.
- **Unknown theme names are not errors** — VCR# falls back to the `Default` theme. Spelling and spaces still matter (`One Dark`, not `OneDark`).

```tape
# settings first…
Set Theme Dracula
Set FontSize 24

# …then actions
Type "echo hello"
Enter
Wait
```

## See also

- [Configuration options reference](xref:docs.reference.configuration-options) — every `Set` setting.
- [vcr.toml reference](xref:docs.reference.vcr-toml) — presets for `Use` and macros for `Exec`.
- [CLI commands reference](xref:docs.reference.cli-commands) — the verbs that run tapes.
