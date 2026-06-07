---
title: "vcr.toml and Presets Reference"
description: "Reference for the vcr.toml config file: preset and macro sections, discovery, value types, and resolution rules."
uid: "docs.reference.vcr-toml"
order: 4250
---

## Overview

`vcr.toml` is an optional config file that lets multiple tapes share `Set` values, output
locations, and reusable `Exec` command templates. A tape opts in with [`Use`](xref:docs.reference.tape-syntax)
(for presets) and the macro form of [`Exec`](xref:docs.reference.tape-syntax) (for macros).

Presets and macros are **expanded before recording**. `PresetResolver` rewrites `Use` and the
`Exec` macro form into primitive commands while the tape is being resolved, so the recording
engine never sees a preset or a macro — only the final, flattened settings and actions.

For a task-oriented walkthrough, see [the presets how-to guide](xref:docs.how-to.presets). For the
inverse operation — factoring existing tapes into a shared `vcr.toml` — see
[`vcr migrate`](xref:docs.reference.cli-commands).

## Discovery

VCR# locates `vcr.toml` by **walking up** the directory tree from the tape file's directory until it
finds a file named exactly `vcr.toml`. The first one found is used.

This means a single `vcr.toml` at the root of a project applies to every tape beneath it, and a
nested `vcr.toml` overrides the one above it for tapes in that subtree.

> [!NOTE]
> If no `vcr.toml` is found, `Use` and the `Exec` macro form have nothing to resolve against and
> produce a resolution error. Presets and macros only exist when a `vcr.toml` is discovered.

## File format

`vcr.toml` is a deliberate **TOML subset**. It supports two section kinds and simple scalar values
only. The following TOML features are **not** supported:

- Arrays
- Inline tables
- Dotted keys (beyond the section header itself)

There are exactly two section kinds: `[preset.NAME]` and `[macro]`.

### Value types

Values in both section kinds are parsed as one of:

| Value | Example | Notes |
| --- | --- | --- |
| Quoted string | `theme = "one dark"` | |
| Boolean | `transparentBackground = true` | `true` / `false` |
| Duration | `endBuffer = 5s` | `5s`, `250ms`, `1.5m` |
| Number / bare word | `cols = 82` | Kept as a literal number or word |

## `[preset.NAME]` sections

A `[preset.NAME]` section declares a named preset. `NAME` is **case-insensitive** and may be quoted
(`[preset."my doc"]`). Repeated `[preset.NAME]` headers with the same name **merge** into one preset.

### Reserved keys

Three keys inside a preset are reserved and are **not** settings:

| Key | Purpose |
| --- | --- |
| `inherits` | Name of a parent preset to inherit from. Resolution is **base-first**: the parent is applied, then this preset's keys override it. |
| `outDir` | Derives the tape's Output as `{outDir}/{tape}.svg`, where `{tape}` is the tape's basename. |
| `output` | Explicit output template, e.g. `"assets/{name}.png"`. `{name}` is the tape basename. **Takes precedence over `outDir`.** |

### Setting keys

Every key other than the three reserved keys is a **setting**, using the same names as the tape
`Set` command (see [the configuration options reference](xref:docs.reference.configuration-options)).
Settings are validated at resolve time, so an unknown name or an out-of-range value is reported
before recording starts.

## `[macro]` section

The `[macro]` section holds `name = "template"` entries that back the macro form of `Exec`. A tape
references one with `Exec name arg`, which expands the named template.

Templates support two placeholders:

| Placeholder | Expands to |
| --- | --- |
| `{0}` | The `Exec` argument. Defaults to the tape basename when no arg is given. |
| `{name}` | The tape basename. |

For example, with `showcase = "dotnet run --no-build showcase {0}"` in `[macro]`, a tape line of
`Exec showcase demo` expands to `Exec "dotnet run --no-build showcase demo"`.

## Resolution rules

When a tape is resolved, presets, inheritance, and output derivation combine in a fixed order:

- **Inheritance is base-first.** If a preset declares `inherits`, the parent's keys are applied
  first, then the child preset's keys override them.
- **Later `Use` wins.** A tape may have multiple `Use` lines. When two applied presets set the same
  key, the one applied later wins.
- **The tape's own `Set` always wins.** Any setting the tape sets directly with `Set` overrides
  whatever a preset would have applied.
- **Output derivation.** An applied preset's `output` template (or, failing that, `outDir`) supplies
  the tape's Output. An explicit `Output` line in the tape takes precedence.

## Example

```toml
[preset.doc]
theme = "one dark"
cols = 82
transparentBackground = true
endBuffer = 5s
outDir = "assets"          # Output becomes assets/<tape>.svg

[preset.landing]
inherits = "doc"
fontSize = 13
output = "assets/{name}.png"

[macro]
showcase = "dotnet run --no-build showcase {0}"
```

Given this config, a tape with `Use landing` inherits everything from `doc` (theme, cols,
transparent background, end buffer), overrides `fontSize` to `13`, and writes its output to
`assets/<tape>.png` (the `output` template overrides `doc`'s `outDir`). A tape with
`Exec showcase chart` runs `dotnet run --no-build showcase chart`.

## See also

- [How to share settings with presets](xref:docs.how-to.presets) — task-oriented guide.
- [Configuration options reference](xref:docs.reference.configuration-options) — every setting name and value.
- [Tape file syntax reference](xref:docs.reference.tape-syntax) — `Use`, `Run`, and `Exec`.
- [CLI commands reference](xref:docs.reference.cli-commands) — `vcr migrate` factors tapes into a `vcr.toml`.
