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

## Common Sleep Patterns

### Pause After Typing

Give viewers time to read what was typed:

```tape
Type "npm install express"
Sleep 800ms        # Let viewers read the command
Enter
Wait
```

### Pause Before Important Actions

Build anticipation before key moments:

```tape
Type "Let's deploy to production..."
Enter
Sleep 1s           # Dramatic pause
Type "kubectl apply -f production.yaml"
Enter
Wait
```

### Pause After Output

Let results sink in:

```tape
Type "echo 'Build complete!'"
Enter
Wait
Sleep 2s           # Give time to read success message
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

## Choose Your Pacing

**For demo videos (experienced audience):** Use 200-300ms pauses
```tape
Type "git status"
Sleep 300ms
Enter
Wait
```

**For tutorials (learning audience):** Use 1-2s pauses
```tape
Type "# This command shows uncommitted changes"
Enter
Sleep 2s
Type "git status"
Sleep 800ms
Enter
Wait
Sleep 3s
```

**For presentations:** Use 1-1.5s pauses with longer delays at key moments
```tape
Type "# The magic happens here..."
Enter
Sleep 1.5s
Type "terraform apply"
Sleep 1s
Enter
Wait
```

## Best Practices

**If your recording feels slow:** Reduce sleep durations or use them only at key moments for emphasis.

**For more natural pacing:** Combine slower typing speed with shorter sleeps:
```tape
Set TypingSpeed 80ms     # Slightly slower typing
Type "npm run test"
Sleep 400ms              # Shorter sleep needed
Enter
```

**For consistent rhythm:** Use the same sleep duration for similar actions:
```tape
Type "command1"
Sleep 500ms
Enter
Wait

Type "command2"
Sleep 500ms              # Same pause
Enter
Wait
```

**If very short sleeps aren't visible:** At 50fps, sleeps under 100ms may be barely noticeable (20ms = 1 frame).

## Examples

**Basic tutorial pacing:**
```tape
Output "tutorial.gif"
Set TypingSpeed 70ms

Type "# Let's create a new file"
Enter
Sleep 1s

Type "touch hello.txt"
Sleep 500ms
Enter
Wait

Sleep 1s
Type "# Now let's add some content"
Enter
Sleep 1s

Type "echo 'Hello World' > hello.txt"
Sleep 500ms
Enter
Wait
```

**Quick demo (minimal pauses):**
```tape
Output "quick-demo.gif"
Set TypingSpeed 40ms

Type "git status"
Sleep 200ms
Enter
Wait

Type "git add ."
Sleep 200ms
Enter
Wait

Type "git commit -m 'Update'"
Sleep 200ms
Enter
Wait
```

**Presentation with emphasis:**
```tape
Output "presentation.gif"

Type "# Watch what happens when we run this:"
Enter
Sleep 2s              # Long pause for emphasis

Type "docker-compose up"
Sleep 1s              # Anticipation
Enter
Wait /started/

Sleep 3s              # Let audience absorb result
```
