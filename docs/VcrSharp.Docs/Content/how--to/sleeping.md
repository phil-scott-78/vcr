---
title: "How to Control Timing with Sleep"
description: "Add pauses and control pacing in your terminal recordings with Sleep commands"
uid: "docs.how-to.sleeping"
order: 2300
---

## Overview

Add fixed-duration pauses to your recordings for pacing and readability.

## Basic Sleep Syntax

```tape
Sleep <duration>
```

Duration consists of a number + unit suffix.

### Duration Units

- `ms` - Milliseconds
- `s` - Seconds
- `m` - Minutes

**Examples:**
```tape
Sleep 500ms    # Half a second
Sleep 2s       # 2 seconds
Sleep 1m       # 1 minute
```

## When to Use Sleep

Use Sleep to control pacing at three key moments:

- **After typing** - Give viewers time to read the command before pressing Enter
- **Before important actions** - Build anticipation before key moments
- **After output** - Let results sink in before proceeding

```tape
Type "npm install express"
Sleep 800ms        # After typing
Enter
Wait
Sleep 2s           # After output completes
```

## Sleep vs Wait

**Use Sleep when you know the exact duration needed:**
```tape
Type "# Step 1: Install dependencies"
Enter
Sleep 1s           # Fixed pause
Type "npm install"
Enter
Wait               # Wait for actual completion
```

**Use Wait when you need to sync with terminal output:**
```tape
Exec "dotnet build"
Wait /Build succeeded/    # Waits for specific pattern
```

## Combining Sleep and Wait

Use both in the same recording for optimal control:

```tape
Output "demo.gif"

# Show command with Sleep for pacing
Type "docker build -t myapp ."
Sleep 500ms
Enter

# Wait for actual build completion
Wait /Successfully built/

# Pause to let viewers see result
Sleep 2s

# Continue
Type "docker run myapp"
Enter
Wait
```

## Choosing Sleep Durations

Match sleep duration to your audience and purpose:

- **Demo videos (experienced audience):** 200-300ms pauses
- **Tutorials (learning audience):** 1-2s pauses
- **Presentations:** 1-1.5s pauses, with longer delays at key moments

**Tip:** For more natural pacing, combine slower typing speed with shorter sleeps:
```tape
Set TypingSpeed 80ms     # Slightly slower typing
Type "npm run test"
Sleep 400ms              # Shorter sleep needed
Enter
```

**Tip:** Use consistent sleep durations for similar actions to maintain rhythm.

**Note:** At 50fps, sleeps under 100ms may be barely noticeable (20ms = 1 frame).

## Complete Example

This tutorial-style recording demonstrates effective sleep usage:

```tape
Output "tutorial.gif"
Set TypingSpeed 70ms

Type "# Let's create a new file"
Enter
Sleep 1s              # Let comment be read

Type "touch hello.txt"
Sleep 500ms           # Pause after typing
Enter
Wait

Sleep 1s              # Pause after completion
Type "# Now let's add some content"
Enter
Sleep 1s

Type "echo 'Hello World' > hello.txt"
Sleep 500ms
Enter
Wait
```
