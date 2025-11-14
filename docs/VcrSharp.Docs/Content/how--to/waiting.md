---
title: "How to Wait for Output Patterns"
description: "Use Wait commands to synchronize your recording with command output and prompts"
uid: "docs.how-to.waiting"
order: 2200
---

## Overview

Synchronize your recording with terminal output by waiting for specific patterns or shell prompts.

## Basic Wait Command

```tape
Wait
```

Without arguments, `Wait` looks for the default shell prompt pattern (configured per shell).

**Example:**
```tape
Type "ls"
Enter
Wait       # Waits for prompt to reappear
```

## Waiting for Patterns

To wait for specific output, use a regex pattern in forward slashes:

```tape
Wait /pattern/
```

**Common patterns:**
```tape
Wait /Complete/           # Literal word
Wait /Build succeeded/    # Phrase with space
Wait /\d+ tests passed/   # Number + text (matches "5 tests passed", "100 tests passed")
```

**Examples:**
```tape
# Wait for build completion
Exec "dotnet build"
Wait /Build succeeded/

# Wait for server startup
Exec "npm start"
Wait /Server listening on port/

# Wait for test results
Exec "pytest"
Wait /\d+ passed/
```

## Wait Scopes

Wait has three scope modes that control where it searches for patterns:

### Wait+Buffer (Default)

Searches in a persistent buffer that accumulates output across multiple Wait commands.

```tape
Wait+Buffer /pattern/
# or just
Wait /pattern/            # Buffer is default
```

**Use for fast-scrolling output or multi-line patterns:**
```tape
Exec "npm install"
Wait+Buffer /added.*packages/    # Catches message even if it scrolls fast
```

### Wait+Line

Searches only the current terminal line.

```tape
Wait+Line /pattern/
```

**Use for prompt detection or single-line status messages:**
```tape
Type "echo 'Done'"
Enter
Wait+Line /Done/       # Only check current line
```

### Wait+Screen

Searches the entire visible terminal screen.

```tape
Wait+Screen /pattern/
```

**Use for TUI applications or multi-line visible output:**
```tape
Exec "htop"
Wait+Screen /Load average/    # Find in visible screen content
```

## Custom Timeouts

Override the default 15-second timeout:

```tape
Wait@30s /pattern/
```

### When to Use Custom Timeouts

**Long-running operations:**
```tape
Exec "docker build -t myapp ."
Wait@5m /Successfully built/     # Build can take minutes
```

**Quick operations (shorter timeout):**
```tape
Type "echo hi"
Enter
Wait@2s                          # Should finish quickly
```

## Combining Scope and Timeout

```tape
Wait+<scope>@<duration> /pattern/
```

**Examples:**
```tape
Wait+Buffer@30s /Complete/
Wait+Line@5s />\s*$/
Wait+Screen@10s /Status: OK/
```

## Practical Examples

### Wait for Build Completion

```tape
Output "build.gif"

Exec "cargo build --release"
Wait@3m /Finished release/        # Rust builds can be slow

Type "# Build complete!"
Enter
Wait
```

### Wait for Server Startup

```tape
Output "server.gif"

Exec "python -m http.server 8000"
Wait /Serving HTTP on/            # Wait for server ready message

Sleep 1s
Type "# Server is now running"
Enter
```

### Wait for Download Progress

```tape
Exec "wget https://example.com/large-file.zip"
Wait@2m /saved/                   # Wait for download completion
```

### Chaining Multiple Waits

```tape
Exec "npm test"
Wait /Tests started/              # Wait for tests to begin
Sleep 2s                          # Pause to show progress
Wait /Tests completed/            # Wait for completion
```

## Troubleshooting

### Pattern Not Matching

**Problem:** Wait times out even though output appeared.

**Solutions:**

1. **Check your regex:**
   ```tape
   # Bad: Missing escape
   Wait /Build succeeded./       # '.' matches any character

   # Good: Escaped correctly
   Wait /Build succeeded\./      # Literal period
   ```

2. **Use broader scope:**
   ```tape
   Wait+Buffer /pattern/         # Instead of Wait+Line
   ```

3. **Check case sensitivity:**
   ```tape
   # Regex is case-sensitive by default
   Wait /success/                # Won't match "Success"
   Wait /[Ss]uccess/             # Matches both
   ```

### Timeout Too Short

**Problem:** Command takes longer than expected.

**Solution:**
```tape
Set WaitTimeout 30s              # Increase default
# or
Wait@60s /pattern/               # Override for specific Wait
```

### Output Scrolls Too Fast

**Problem:** Pattern appears but Wait misses it (happens with Wait+Line).

**Solution:**
```tape
Wait+Buffer /pattern/            # Use Buffer scope (default)
```

## Best Practices

**Use specific patterns:**
```tape
# Vague
Wait /done/                      # Might match unrelated output

# Specific
Wait /Build done in \d+ms/       # Matches only build completion
```

**Match actual output format:**
```tape
# Check your actual output first, then write pattern
Exec "npm test"
Wait /\d+ passing/               # Matches "5 passing"
```

**Combine with Exec for real output:**
```tape
# Good: Real output
Exec "dotnet test"
Wait /Test Run Successful/

# Less reliable: Simulated typing
Type "dotnet test"
Enter
Wait /Test Run Successful/       # Might not appear if command fails
```

**Set appropriate timeouts:**
```tape
# Fast operation
Wait@5s /Installed/

# Slow operation
Wait@5m /Build complete/
```

**Use default Wait for prompts:**
```tape
Type "ls"
Enter
Wait                            # Simple - waits for shell prompt
```

**If Wait fails, verify your pattern matches the actual output:**
Run your recording and check the terminal output format. Adjust your regex pattern if needed.
