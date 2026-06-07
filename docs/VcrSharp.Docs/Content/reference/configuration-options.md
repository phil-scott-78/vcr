---
title: "Configuration Options Reference"
description: "Complete catalog of every VCR# Set setting: name, type, default, range, and meaning."
uid: "docs.reference.configuration-options"
order: 4200
---

This page is the exhaustive catalog of every VCR# setting. Settings are configured with the `Set`
command in a tape file (see <xref:docs.reference.tape-syntax>), with a `[preset]` key in a
`vcr.toml` (see <xref:docs.reference.vcr-toml>), or from the command line with `--set` (see
<xref:docs.how-to.cli-overrides>).

Setting names are **case-insensitive**.

## Setting in a tape file

```tape
Set Theme "Dracula"
Set Cols 80
Set FontSize 18
```

> [!IMPORTANT]
> Ordering rules for tape files: every `Set` must appear **before** any action command (`Type`,
> key presses, `Sleep`, `Wait`, `Exec`, `Screenshot`, etc.), and **no setting may be set twice**.
> A duplicate `Set`, or a `Set` placed after an action, is a parse error. Settings cannot change
> mid-recording.

## Overriding from the CLI

The default `vcr` command accepts repeatable `--set KEY=VALUE` and `-o|--output FILE` options:

```bash
vcr demo.tape --set Theme=Nord --set FontSize=24 -o extra.png
```

A `--set` override **wins over a matching `Set` in the tape** (key match is case-insensitive) and is
exempt from the once-only / ordering rules above. `-o|--output` appends an extra output target
without removing the tape's own `Output` declarations. Full precedence and worked examples are in
<xref:docs.how-to.cli-overrides>.

Precedence, highest to lowest:

1. CLI `--set`
2. Tape `Set` (and `vcr.toml` preset keys merged in via `Use`)
3. Built-in defaults

## Terminal and sizing

| Setting | Type | Default | Range / values | Meaning |
| --- | --- | --- | --- | --- |
| `Cols` | int | auto | `> 0` | Terminal width in character columns. Unset = auto. |
| `Rows` | int | auto | `> 0` | Terminal height in character rows. |
| `Size` | word | `grid` | `grid`, `fit` | `grid` renders exactly `Cols`×`Rows`. `fit` crops the SVG to the measured content extent, letting you over-provision `Cols`/`Rows` (SVG only). |

> [!NOTE]
> There is no `Set Width` or `Set Height` — pixel dimensions are derived from `Cols`/`Rows` and the
> font metrics. To size the terminal, set `Cols` and `Rows`.

## Fonts

| Setting | Type | Default | Meaning |
| --- | --- | --- | --- |
| `FontSize` | int (px) | `22` | Font size in pixels. |
| `FontFamily` | string | `monospace` | Font family name (raster output only; SVG draws text plus box/block/powerline glyphs as paths). |
| `LetterSpacing` | float | `1.0` | Letter-spacing multiplier. |
| `LineHeight` | float | `1.0` | Line-height multiplier. |

## Animation and video

| Setting | Type | Default | Range / values | Meaning |
| --- | --- | --- | --- | --- |
| `Framerate` | int | `50` | `1`–`120` | Recording framerate (frames per second). |
| `PlaybackSpeed` | float | `1.0` | `> 0` | Playback-speed multiplier. `> 1.0` speeds up, `< 1.0` slows down. |
| `LoopOffset` | float | `0` | — | Seconds of pause added before animated output loops. |
| `Loop` | bool | `true` | — | When `false`, the reveal plays once and holds the final frame (SVG: `repeatCount="1"` + `fill="freeze"`; GIF: plays once). Ignored when `LoopCount` is set. |
| `LoopCount` | int | unset | `> 0` | Explicit number of plays before holding the final frame. Overrides `Loop`. Applies to SVG (`repeatCount`) and GIF (`-loop`). |
| `MaxColors` | int | `256` | `1`–`256` | Maximum colors in the GIF palette. GIF only. |

## Styling

| Setting | Type | Default | Meaning |
| --- | --- | --- | --- |
| `Theme` | string | `Default` | Color theme. Case-insensitive match; an unknown name falls back to `Default`. See <xref:docs.how-to.themes>. |
| `Padding` | int (px) | `0` | Padding around terminal content. **Honored by SVG.** |
| `Margin` | int (px) | `0` | Margin around the whole recording. SVG ignores this. |
| `MarginFill` | string | unset | Margin fill: hex color or image path. SVG ignores this. |
| `WindowBarSize` | int (px) | `30` | Height of the decorative window bar. SVG ignores this. |
| `BorderRadius` | int (px) | `0` | Corner radius for rounded corners. SVG ignores this. |
| `CursorBlink` | bool | `true` | Enable or disable cursor blinking. |
| `DisableCursor` | bool | `false` | When `true`, the cursor is not rendered in frames, screenshots, or SVG output. |
| `TransparentBackground` | bool | `false` | Use a transparent background instead of the theme background. |

> [!NOTE]
> The SVG renderer honors `Padding` only. It ignores `Margin`, `MarginFill`, `WindowBarSize`, and
> `BorderRadius`, and captures only the visible viewport (not scrollback). Those four settings apply
> to raster output (GIF/MP4/WebM/PNG).

## SVG-only

These affect SVG output (`Output *.svg`, `Screenshot *.svg`, and `Set Mode static` SVG frames) and are
ignored for other formats.

| Setting | Type | Default | Meaning |
| --- | --- | --- | --- |
| `SvgIntrinsicSize` | bool | `true` | Emit explicit `width`/`height` (px) on the root `<svg>` (in addition to `viewBox`) so an `<img>` embed has a stable intrinsic size. `false` = `viewBox` only. |
| `SvgMetadata` | bool | `true` | Emit machine-readable `data-*` attributes on the root `<svg>`: `data-cols`, `data-rows`, `data-font-size`, `data-cell-width`, `data-cell-height`, `data-padding`. With `Size fit`, `data-cols`/`data-rows` report the cropped extent. |

## Capture mode

| Setting | Type | Default | Values | Meaning |
| --- | --- | --- | --- | --- |
| `Mode` | word | `animated` | `animated`, `static` | `animated` = normal animated recording. `static` runs `Exec`, waits for output to settle, then emits **one** static frame per `Output` (no animation, no command echo). Every `Output` must be `.svg` or `.png`. |

```tape
Set Mode static
Exec "my-tui --render-table"
Output table.svg
```

> [!NOTE]
> `Size` (above) pairs naturally with `Mode static`: over-provision `Cols`/`Rows`, set `Size fit`,
> and the finished frame is cropped to exactly its content.

## Behavior and timing

| Setting | Type | Default | Meaning |
| --- | --- | --- | --- |
| `Shell` | string | `pwsh` (Windows) / `bash` (Unix) | Shell launched for the session. |
| `WorkingDirectory` | string | current directory | Working directory for the session. Must exist (validated). |
| `TypingSpeed` | duration | `60ms` | Delay between keystrokes for `Type`. A **bare number = milliseconds** here. |
| `WaitTimeout` | duration | `15s` | Maximum time a `Wait` blocks before timing out. |
| `InactivityTimeout` | duration | `5s` | Output-unchanged window that marks an `Exec` command done. |
| `MaxWaitForInactivity` | duration | `120s` | Overall cap on waiting for output to settle after `Exec`. |
| `StartWaitTimeout` | duration | `10s` | Maximum time to wait for first terminal activity before recording starts. |
| `StartBuffer` | duration | `0ms` | Blank time kept before the first activity; earlier frames are trimmed. |
| `EndBuffer` | duration | `100ms` | Time kept after the last activity before the recording ends. |
| `HoldDuration` | duration | (alias) | **Alias of `EndBuffer`** — sets the same value. |
| `StartupDelay` | duration | `3.5s` | Delay before `Exec` runs at startup. Uses a shell-specific sleep for cross-platform consistency. |
| `ScreenshotWaitForInactivity` | bool | `false` | Make `Screenshot` wait for the buffer to settle before capturing (so a shot right after `Exec` catches finished output). |
| `ScreenshotInactivityTimeout` | duration | `500ms` | How long the buffer must be unchanged for a settling `Screenshot` to fire. Only used when `ScreenshotWaitForInactivity` is `true`. |

> [!NOTE]
> Duration syntax: `500ms`, `2s`, `1.5m`. A bare number means **seconds** everywhere **except**
> `TypingSpeed`, where a bare number means milliseconds.

## Deprecated settings

These still parse but emit a warning. They exist only to ease migration — prefer `Mode` and `Size`.

| Deprecated | Replacement |
| --- | --- |
| `Set StaticOutput true` / `false` | `Set Mode static` / `Set Mode animated` |
| `Set FitToContent true` / `false` | `Set Size fit` / `Set Size grid` |

`vcr migrate` rewrites these into `mode`/`size` keys automatically; see <xref:docs.reference.vcr-toml>.

> [!CAUTION]
> `Set CssVariables`, `Set Width`, `Set Height`, and `Set WaitPattern` have been **removed** and are
> now parse errors. Use `Cols`/`Rows` for sizing. If you see these in an older tape, delete them.

## Validation

VCR# validates settings and reports clear errors:

- `Framerate`: `1`–`120`
- `MaxColors`: `1`–`256`
- `LoopCount`: `> 0`
- `Cols`, `Rows`, `FontSize`, `WindowBarSize`: `> 0`
- `Padding`, `Margin`, `BorderRadius`: `≥ 0`
- `Mode static` requires every `Output` to be `.svg` or `.png`.
- `WorkingDirectory` must exist.

Duration settings (`TypingSpeed`, `WaitTimeout`, `StartBuffer`, etc.) are not range-validated.

```
Error: Framerate must be between 1 and 120
Error: WorkingDirectory does not exist: C:\NonExistent
```

## Complete example

```tape
# A static SVG of a finished TUI table, cropped to content.
Output table.svg

Set Mode static
Set Size fit
Set Theme "One Dark"
Set Cols 100
Set Rows 40
Set Padding 16
Set FontSize 22
Set DisableCursor true
Set InactivityTimeout 3s

Exec "my-tui --render-table"
```

## See also

- <xref:docs.reference.tape-syntax> — the full tape grammar, including how `Set` fits with action commands.
- <xref:docs.reference.vcr-toml> — sharing settings across tapes via `vcr.toml` presets.
- <xref:docs.how-to.cli-overrides> — overriding settings at the command line with `--set` and `-o`.
