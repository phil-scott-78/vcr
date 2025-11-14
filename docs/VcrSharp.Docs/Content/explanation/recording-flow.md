---
title: "The Recording Flow Explained"
description: "Understanding why VCR#'s recording process works the way it does"
uid: "docs.explanation.recording-flow"
order: 3200
---

## Overview

VCR#'s recording flow might seem complex at first—parse, initialize, execute, capture, trim, encode. Why not just record the screen? This explanation explores the design decisions behind each phase and reveals why this particular flow enables VCR#'s unique capabilities.

## Why This Flow?

VCR#'s six-phase pipeline serves a purpose: each separation solves a specific problem.

**Parse before Initialize**: Validates syntax before spinning up external processes (ttyd, browser). If your tape file has a typo on line 50, you'll know in milliseconds—not after waiting for the terminal to start.

**Initialize before Execute**: Ensures the environment is ready, avoiding race conditions. The terminal must be fully initialized before commands execute, otherwise early commands might execute before the shell is ready.

**Capture in Parallel**: Maintains consistent framerate despite variable command timing. A 10-second build command doesn't mean 10 seconds of 1 fps—it's still 50 fps throughout.

**Trim After Capture**: Keeps recording simple, handles cleanup separately. The capture loop doesn't need to know about trimming logic; each phase has one responsibility.

**Encode Last**: Separates timing-critical recording from slower encoding. Encoding can take seconds; you don't want that blocking the next recording.

Other tools (like screen recorders) do everything simultaneously—simpler architecture, but tighter coupling. VCR#'s separation makes each phase testable, debuggable, and replaceable.

## Design Philosophy: Fail Fast, Fail Clearly

Notice the order: parse → validate → initialize → execute. This isn't accidental—it's optimized for fast feedback.

**Alternative approach**: Stream interpretation (read tape file line-by-line, execute as you go)
- **Benefit**: Faster start (no parsing overhead)
- **Cost**: Errors discovered late (after setup costs)

**VCR#'s choice**: Complete parsing upfront
- **Benefit**: Syntax errors caught immediately, before launching browser
- **Cost**: 50-100ms parsing overhead

VCR# prioritizes "fail fast, fail clearly" over immediate execution. Discovering a syntax error after ttyd launches wastes 2-3 seconds. Catching it during parsing wastes 50ms.

This trade-off reveals VCR#'s target use case: **scriptable, repeatable recordings**. For one-off screen captures, instant start matters more. For CI/CD and documentation, early validation matters more.

## Phase 1: Parsing - Building the AST

When you run `vcr demo.tape`, VCR# immediately builds an Abstract Syntax Tree (AST) using Sprache parser combinators. Each line becomes an `ICommand` object before any external process starts.

**Why build an AST?**

An AST enables features impossible with streaming interpretation:
- **Syntax validation**: Catch errors before launching external processes
- **Optimization**: Analyze command patterns, preload resources
- **Future capabilities**: Dry-run simulations, command reordering, parallel execution

```tape
Output "demo.gif"
Set Theme "Dracula"
Type "echo Hello"
Enter
Wait
```

Becomes:
```
List<ICommand>:
  [0] OutputCommand { FilePath = "demo.gif" }
  [1] SetCommand { SettingName = "Theme", Value = "Dracula" }
  [2] TypeCommand { Text = "echo Hello" }
  [3] KeyCommand { Key = "Enter" }
  [4] WaitCommand { Scope = Buffer, Timeout = 15s }
```

**Comparison to other tools**:
- **asciinema**: No parsing—records terminal directly (can't validate or optimize)
- **VHS**: Pre-parses like VCR#, enabling similar validation
- **Screen recorders**: No scripting layer at all

VCR#'s parsing phase is what makes it *programmable* rather than just a screen recorder.

## Phase 2: Initialization - Why Browser-Based?

VCR# launches ttyd (terminal server) and connects via Playwright (browser automation). This seems heavyweight—why not use a native terminal emulator?

**The rendering consistency problem**: Terminal emulation is harder than it looks. Fonts, colors, Unicode, ligatures, emoji—getting these pixel-perfect across Windows/macOS/Linux is a years-long effort.

**VCR#'s solution**: Delegate to xterm.js, the battle-tested terminal renderer that powers VS Code, GitHub Codespaces, and countless web terminals. Rather than reinventing terminal rendering (and inevitably hitting edge cases), leverage a renderer with millions of users finding bugs we'll never encounter.

**The trade-off**:
- **Cost**: Browser overhead (~100MB memory, 1-2 second startup)
- **Benefit**: Pixel-perfect rendering consistency across all platforms

This choice reflects VCR#'s priority: **visual quality over resource efficiency**. The target user is creating documentation or demos where inconsistent rendering would be unacceptable. The extra 100MB is worth never worrying about "works on my machine" rendering bugs.

**Alternative approaches considered**:
1. **Native terminal emulation** (ConPTY on Windows, PTY on Unix)
   - Lighter weight
   - Platform-specific rendering differences
   - Missing advanced features (ligatures, etc.)

2. **VNC/screen capture of real terminal**
   - True native look
   - Can't style programmatically (no theme injection)
   - Screen resolution dependent

3. **Text-based recording** (like asciinema)
   - Minimal resource usage
   - Requires custom player, can't embed in slides/docs

VCR# chose browser-based rendering for **universality**: the output works everywhere, looks identical everywhere, and needs no special player.

## Phase 3: Execution - Why Sequential?

Commands execute one-at-a-time in a sequential loop. Why not parallelize?

```csharp
foreach (var command in commands)
{
    await command.ExecuteAsync(executionContext, cancellationToken);
}
```

**The determinism argument**: Terminal interactions are inherently sequential—the output of one command affects the next. Parallelizing would be faster but produce non-deterministic results.

**Example of why sequence matters**:
```tape
Exec "echo 'export FOO=bar' >> ~/.bashrc"
Exec "source ~/.bashrc"
Exec "echo $FOO"  # Depends on previous commands
```

Running these in parallel would be meaningless—commands have dependencies.

**Could some commands run in parallel?** Yes! Independent commands like `Sleep` don't interact with the terminal. But VCR# chooses simplicity: sequential execution is predictable, debuggable, and matches how humans think about terminal interactions.

**Trade-off**:
- **Cost**: Can't optimize independent operations
- **Benefit**: Predictable, debuggable, matches mental model

This reveals another design priority: **simplicity and predictability over performance optimization**.

## Phase 4: Frame Capture - Pixel-Based Recording

While commands execute, a parallel thread captures screenshots at regular intervals (default: 50fps).

**Why pixel-based? Why not record terminal state (text + attributes)?**

This is VCR#'s fundamental design choice, inherited from its inspiration (VHS):

**Text-based recording** (asciinema's approach):
- Records: character codes, colors, cursor positions
- Output: JSON with terminal state deltas
- **Benefit**: Tiny file sizes (KB not MB), perfect accuracy
- **Cost**: Requires custom player, can't embed in presentations

**Pixel-based recording** (VCR#'s approach):
- Records: PNG screenshots every 20ms (at 50fps)
- Output: GIF/MP4/WebM video files
- **Benefit**: Universal playback, embeddable anywhere
- **Cost**: Large file sizes (MB not KB), lossy compression

VCR# trades efficiency for **universal compatibility**. The target use case is documentation, presentations, and social media—contexts where requiring a custom player is a non-starter.

Notice the philosophical difference:
- **asciinema**: Prioritizes perfect fidelity to terminal state
- **VCR#**: Prioritizes visual output that works everywhere

Neither is "better"—they serve different needs.

## Phase 5: Trimming - The Blank Frame Problem

After recording, VCR# trims blank frames from the start and end. Why not just start/stop capture precisely?

**The timing uncertainty problem**: Shell startup isn't instant. Determining exactly when "useful output" begins is impossible—different shells, different configurations, different speeds.

**VCR#'s solution**: Record generously (with buffers), trim precisely (after the fact).

```
StartBuffer (default: 500ms) - Keep this much blank time before first activity
EndBuffer (default: 100ms) - Keep this much time after last activity
```

**Why separate capture and trimming?**

**During capture**: Focus on maintaining perfect framerate timing. Frame capture runs in a tight loop—adding trim logic here risks dropping frames.

**After capture**: Analyze frames leisurely. Trimming can be slow and thorough without affecting recording quality.

**Alternative approach**: Start capture on first activity
- **Benefit**: No wasted frames
- **Cost**: Risk missing early output if detection is delayed

VCR# chooses reliability: capture everything, trim precisely. The extra 25 frames (500ms at 50fps) costs negligible disk space temporarily but ensures nothing is missed.

## Phase 6: Encoding - Why FFmpeg?

Final phase: convert PNG frames to GIF/MP4/WebM using FFmpeg.

**Why external FFmpeg instead of .NET libraries?**

**FFmpeg**: Industry-standard video encoder
- **Benefit**: Best-in-class compression, format support, cross-platform
- **Cost**: External dependency

**.NET video libraries**: Native .NET encoding
- **Benefit**: No external dependencies
- **Cost**: Limited format support, worse compression, cross-platform issues

VCR#'s choice reflects the **composition over custom** philosophy seen in ttyd and Playwright. Rather than building video encoding (a massive undertaking), delegate to the tool that thousands of projects rely on.

**GIF palette optimization**: Notice that GIF encoding is two-pass:
1. Generate optimal color palette from all frames
2. Apply palette with dithering

This produces significantly smaller files than naive GIF conversion, at the cost of slower encoding. VCR# prioritizes output quality over encoding speed.

## How Other Tools Handle Recording

Understanding VCR#'s flow helps when you see how alternatives differ:

**asciinema** (text-based):
- Single phase: stream terminal output to JSON
- Fast, tiny files, requires custom player
- Perfect for developers sharing terminal sessions

**VHS** (pixel-based, VCR#'s inspiration):
- Similar multi-phase approach
- No real command execution (Type only, no Exec)
- Simpler flow, less flexibility

**OBS / Screen recorders**:
- No phases—continuous video capture
- Records everything (mouse, other windows)
- No scriptability or repeatability

**TermRecord** (GIF generator):
- Records real terminal, converts to GIF
- No scripting—manual recording
- Can't reproduce recordings

VCR#'s phased approach is more complex than streaming (asciinema) but enables **scriptable, reproducible, high-quality** recordings that work everywhere.

## The Core Trade-Offs

Every phase reflects these priorities:

1. **Reliability over speed**: Parse upfront, validate early, trim afterward
2. **Quality over efficiency**: Browser rendering, pixel-based capture, two-pass encoding
3. **Universality over optimization**: GIF/MP4 everyone can play, not specialized formats
4. **Composition over custom**: ttyd + Playwright + FFmpeg, not reinvented wheels

VCR# isn't the fastest recorder, nor the most efficient. It's optimized for **creating polished, reproducible, universally-compatible terminal recordings**—and the flow reflects that goal at every step.

## Understanding Through Examples

Consider this simple tape file:
```tape
Type "echo Hello"
Enter
Wait
```

**Why this takes 2-5 seconds to record:**
- 0.5s: Parse and validate
- 1-2s: Launch ttyd and Playwright
- 0.1s: Execute commands
- 0.5s: Wait for prompt
- 1-2s: Encode GIF

Most time is initialization, not execution. This is why VCR# works better for CI/CD (where one startup amortizes over many recordings) than rapid iteration (where startup matters).

**Alternative tools for different needs**:
- **Quick iteration**: Use OBS with manual recording
- **Sharing with developers**: Use asciinema (text-based, instant)
- **Polished, reproducible demos**: Use VCR#

The flow isn't "best"—it's optimized for specific use cases. Understanding the phases helps you choose when VCR# is the right tool.
