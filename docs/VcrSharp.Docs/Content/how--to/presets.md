---
title: "How to Share Settings with vcr.toml Presets"
description: "Hoist repeated Set blocks into a shared vcr.toml preset, then apply it with Use"
uid: "docs.how-to.presets"
order: 2550
---

## Overview

If you maintain a folder of tapes that all start with the same `Set Theme …`, `Set Cols …`,
`Set Padding …` block, you can hoist those lines into a single `vcr.toml` and reference them with
`Use`. Each tape shrinks to the parts that are actually unique, and a settings change happens in one
place instead of across every file.

This guide shows how to build a preset, apply it, derive output paths from it, share command macros,
and generate a `vcr.toml` automatically from tapes you already have.

For the complete file format, see <xref:docs.reference.vcr-toml>. For the `Use`, `Run`, and `Exec`
grammar, see <xref:docs.reference.tape-syntax>.

## How vcr.toml is discovered

VCR# walks **up** from each tape's directory looking for a file literally named `vcr.toml`. Put one
at the root of your docs or samples folder and every tape underneath it shares the same presets — no
flag or path argument required.

## 1. Create a preset

Add a `[preset.NAME]` section. Every key inside it is a setting, using the same names as a tape
`Set` (see <xref:docs.reference.configuration-options>). Put a `vcr.toml` next to your tapes:

```toml
[preset.doc]
theme = "One Dark"
cols = 82
padding = 20
transparentBackground = true
endBuffer = 5s
outDir = "assets"          # Output becomes assets/<tape>.svg
```

> [!NOTE]
> Preset values use TOML types: quoted strings, `true`/`false`, durations (`5s`, `250ms`, `1.5m`),
> or a bare number/word. This is a deliberate TOML **subset** — no arrays, inline tables, or dotted
> keys beyond the section header.

## 2. Replace the repeated Set lines with Use

A tape that used to open with a wall of `Set` commands:

```tape
Set Theme "One Dark"
Set Cols 82
Set Padding 20
Set TransparentBackground true
Set EndBuffer 5s
Output assets/hello.svg

Type "echo hello"
Enter
Wait
```

collapses to:

```tape
Use doc
Output assets/hello.svg

Type "echo hello"
Enter
Wait
```

`Use doc` pulls in every setting from `[preset.doc]`. The preset is resolved before recording, so the
engine sees the same primitive commands either way.

> [!TIP]
> A tape's own `Set` always **overrides** the preset. Keep shared defaults in the preset and put
> per-tape exceptions in the tape:
>
> ```tape
> Use doc
> Set Cols 120     # this tape needs a wider grid; everything else comes from doc
> ```
>
> You can also `Use` more than one preset; a later `Use` wins on conflicting keys.

## 3. Inherit from another preset

Set `inherits` to build a variant on top of an existing preset. Parents apply first (base-first), so
the child only lists what it changes:

```toml
[preset.doc]
theme = "One Dark"
cols = 82
padding = 20
outDir = "assets"

[preset.landing]
inherits = "doc"
fontSize = 13
output = "assets/{name}.png"
```

`Use landing` resolves `doc` first, then applies `landing` on top — so a landing tape gets the doc
theme, cols, and padding plus a smaller font and a PNG output.

## 4. Derive Output paths from the preset

You can drop `Output` from the tape entirely and let the preset supply it. Two reserved keys do this:

- **`outDir`** — Output becomes `{outDir}/{tape}.svg`, where `{tape}` is the tape's basename.
- **`output`** — an explicit template that **takes precedence** over `outDir`. Use `{name}` for the
  tape basename, e.g. `output = "assets/{name}.png"`.

With `outDir = "assets"` in `[preset.doc]`, a tape named `hello.tape` writes to `assets/hello.svg`
with nothing but:

```tape
Use doc

Type "echo hello"
Enter
Wait
```

> [!NOTE]
> Prefer `.svg` for these defaults — SVG output needs no FFmpeg. FFmpeg is required only when an
> output is `.gif`, `.mp4`, or `.webm`.

## 5. Share commands with Run and Exec macros

Two pieces of tape sugar keep the action part of your tapes short.

**`Run "command"`** is shorthand for `Type "command"` + `Enter` + `Wait` (it waits for output to
settle). So instead of:

```tape
Type "dotnet build"
Enter
Wait
```

write:

```tape
Run "dotnet build"
```

**`Exec macroName arg`** expands a `[macro]` template from `vcr.toml`. Define the macro once:

```toml
[macro]
showcase = "dotnet run --no-build showcase {0}"
```

Then call it from any tape — `{0}` is the macro argument (it defaults to the tape basename if you
omit it):

```tape
Use doc
Exec showcase widgets
```

That runs `dotnet run --no-build showcase widgets` as the recording's hidden foreground process. Like
presets, macros are expanded before recording — the engine never sees them.

## 6. Generate vcr.toml from existing tapes

If you already have a directory of tapes with duplicated `Set` blocks, let VCR# build the `vcr.toml`
for you. `vcr migrate` mines the shared settings, writes a preset, and rewrites each tape to `Use` it.

It is a **dry run by default** — it prints what it would do and changes nothing:

```bash
vcr migrate ./tapes
```

Review the plan, then apply it with `--write`:

```bash
vcr migrate ./tapes --write
```

Useful options:

- `--preset NAME` — name the generated preset (default `doc`).
- `--threshold FRACTION` — the fraction of tapes that must share a setting before it is hoisted
  (default `0.6`).
- `-r` / `--recursive` — also process tapes in subdirectories.

```bash
vcr migrate ./tapes --write --preset landing --threshold 0.75 -r
```

Migration proves equivalence: each rewritten tape resolves to the same realized config and the same
action sequence as before. It also modernizes any legacy `StaticOutput`/`FitToContent` settings into
`mode`/`size` in the generated TOML.

For every flag and its default, see <xref:docs.reference.cli-commands>.

## See also

- <xref:docs.reference.vcr-toml> — the full `vcr.toml` format.
- <xref:docs.reference.cli-commands> — all `vcr migrate` options.
- <xref:docs.reference.tape-syntax> — `Use`, `Run`, and `Exec` grammar.
- <xref:docs.how-to.cli-overrides> — override settings per run without editing files.
