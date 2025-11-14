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

## Examples

**Wait for build completion:**
```tape
Exec "cargo build --release"
Wait@3m /Finished release/        # Rust builds can be slow
```

**Wait for server startup:**
```tape
Exec "python -m http.server 8000"
Wait /Serving HTTP on/            # Wait for server ready message
```

**Chain multiple waits:**
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

- **Use specific patterns** - `Wait /Build done in \d+ms/` is better than `Wait /done/` which might match unrelated output
- **Match actual output format** - Check your actual output first, then write the pattern
- **Combine with Exec for real output** - More reliable than Type+Enter since Exec captures actual command output
- **Set appropriate timeouts** - Use `@5s` for fast operations, `@5m` for slow builds
- **Use default Wait for prompts** - Just `Wait` with no pattern waits for the shell prompt to return
