---
title: "How to Capture Screenshots"
description: "Learn how to capture specific frames during your recording as standalone images"
uid: "docs.how-to.screenshots"
order: 2100
---

## Overview

Capture specific frames as standalone PNG or JPG images during recording.

## Basic Screenshot Capture

```tape
Screenshot "filename.png"
```

Supported formats: `.png`, `.jpg`

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

**Tip:** For lossless quality, use `.png`. For smaller file sizes, use `.jpg`.

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
Add `Sleep 500ms` before the Screenshot command to let output stabilize.

**If Screenshot shows wrong content:**
Ensure your `Wait` pattern completed successfully before the Screenshot executes.

**If Screenshot file is not created:**
- Check file permissions in the output directory
- Use absolute paths instead of relative paths
- Verify the parent directory exists
