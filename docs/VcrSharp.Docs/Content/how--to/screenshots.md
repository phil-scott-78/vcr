---
title: "How to Capture Screenshots"
description: "Learn how to capture specific frames during your recording as standalone images"
uid: "docs.how-to.screenshots"
order: 2100
---

## Overview

Capture specific frames as standalone images during recording.

## Basic Screenshot Capture

```tape
Screenshot "filename.png"
```

Supported formats: `.png` (raster, pixel-perfect) and `.svg` (vector, lightweight and searchable). Format is auto-detected from the file extension; any extension other than `.svg` is treated as PNG.

### Example

```tape
Output "demo.gif"

Type "docker ps"
Enter
Wait
Screenshot "containers.png"    # Capture current state
```

## Timing Your Screenshots

**Capture after delay** using `Sleep`:
```tape
Type "ls -la"
Enter
Wait
Sleep 500ms                     # Let output stabilize
Screenshot "files.png"
```

**Capture when output appears** using `Wait`:
```tape
Exec "npm test"
Wait /tests passed/             # Wait for completion
Screenshot "test-results.png"
```

**Tip:** Use `.png` for pixel-perfect raster output. Use `.svg` when you want a small, scalable, text-searchable file (great for embedding in docs).

### Static SVG Screenshot

This is an actual SVG screenshot captured mid-recording with `Screenshot "screenshot-svg.svg"` — text is selectable and the image scales crisply at any size:

<VcrTape src="../demos/screenshot-svg.svg" />

## Capturing a Clean Frame After an `Exec` Command

`Exec` runs in the background and the shell may exit (clearing the screen) before a plain `Screenshot` fires, capturing an empty terminal. Two settings fix this:

**Make `Screenshot` wait for the output to settle:**

```tape
Output "demo.gif"
Set ScreenshotWaitForInactivity true     # wait for the buffer to stop changing
Exec "my-tui --render-table"
Screenshot "table.svg"
```

**Or produce a single static frame with no animation** (no SMIL, no command echo) — best for a widget you want to embed as a still image:

```tape
Set StaticOutput true                    # run Exec, settle, emit one static frame
Output "table.svg"                        # must be .svg or .png
Exec "my-tui --render-table"
```

`StaticOutput` skips the frame-capture loop entirely and emits exactly one settled frame per `Output`.

## Static SVG Widgets for Embedding

For SVG widgets embedded in a docs site or README, combine the SVG output settings:

```tape
Set StaticOutput true        # one clean static frame
Set FitToContent true        # crop to content — no need to guess Cols/Rows
Set CssVariables true         # colors follow the page theme via --vcr-* CSS variables
Set Theme "Dracula"
Output "widget.svg"
Exec "my-tui --render-table"
```

- **`FitToContent`** trims trailing blank rows / right-side blank columns and relaxes the clip-path, so the last row is never shaved — over-provision `Rows` and let the renderer size the SVG.
- **`SvgMetadata`** and **`SvgIntrinsicSize`** are on by default, so the root `<svg>` carries explicit `width`/`height` plus `data-cols`/`data-rows`/`data-font-size` — an `<img>` embed gets a stable intrinsic size and a consumer can compute exact display size without parsing the `viewBox`.
- **`CssVariables`** emits `fill:var(--vcr-green,#…)` with a `:root` palette, so the embedding page can recolor or light/dark-swap the inlined SVG with no regeneration. (Each `var()` has a hex fallback, so `<img>` embeds still render.)

For an **animated** widget that mostly shows a static end state, add `Set Loop false` so the reveal plays once and holds the final frame instead of flashing empty → content every loop.

See the [Configuration Options reference](xref:docs.reference.configuration-options#svg-output-settings) for the full list.

## Multiple Screenshots

Capture different stages of a workflow:

```tape
Output "tutorial.gif"

Type "git status"
Enter
Wait
Screenshot "01-status.png"

Type "git add ."
Enter
Wait
Screenshot "02-add.png"

Type "git commit"
Enter
Wait
Screenshot "03-commit.png"
```

## Screenshots with Hide/Show

Capture without recording the setup:

```tape
Output "demo.gif"

Hide
# Setup not recorded
Type "cd project"
Enter
Wait
Type "clear"
Enter
Wait
Show

# Now recording
Type "npm test"
Enter
Wait
Screenshot "results.png"        # Captured but setup wasn't recorded
```

## Example: Step-by-Step Guide

Capture each stage of a workflow:

```tape
Output "tutorial.gif"

# Step 1
Type "npm install"
Enter
Wait
Screenshot "step-1-install.png"

# Step 2
Type "npm test"
Enter
Wait
Screenshot "step-2-test.png"

# Step 3
Type "npm start"
Enter
Wait /Server listening/
Screenshot "step-3-running.png"
```

## Troubleshooting

**If Screenshot captures mid-animation or partial output:**
Add `Sleep 500ms` before the Screenshot command to let output stabilize, or set `Set ScreenshotWaitForInactivity true` to wait for the buffer to settle automatically.

**If Screenshot after an `Exec` command captures an empty screen:**
The shell exited (clearing the screen) before the Screenshot fired. Use `Set ScreenshotWaitForInactivity true`, or switch to `Set StaticOutput true` to emit a single settled frame.

**If Screenshot shows wrong content:**
Ensure your `Wait` pattern completed successfully before the Screenshot executes.

**If Screenshot file is not created:**
- Check file permissions in the output directory
- Use absolute paths instead of relative paths
- Verify the parent directory exists
