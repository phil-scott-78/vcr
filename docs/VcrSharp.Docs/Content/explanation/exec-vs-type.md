---
title: "Exec vs Type: Determinism and Authenticity"
description: "Understanding the philosophical tension between simulated typing and real command execution"
uid: "docs.explanation.exec-vs-type"
order: 3300
---

## The Fundamental Tension

Every terminal recorder faces a design choice: should you simulate terminal interactions or capture them authentically? VCR# uniquely offers both—and understanding why reveals deeper insights about what terminal recordings are actually for.

**Type**: Simulation - creates the narrative experience of typing
**Exec**: Execution - captures the authentic experience of results

This isn't just a feature comparison. It's a philosophical difference about what matters more: **control** or **truth**.

## Understanding Through History

### The asciinema Approach: Pure Authenticity

asciinema records your actual terminal session—every keystroke, every output byte, exactly as it happened. This is maximally authentic but minimally controllable. If you typo during recording, that typo is permanent. If your command takes 10 minutes, your recording is 10 minutes.

**Philosophy**: "Show what actually happened"
**Trade-off**: Authenticity over repeatability

### The VHS Approach: Pure Simulation

VHS introduced scripted terminal recordings—you write what you want to appear, and VHS makes it look like typing. Every character, every delay, every output is predetermined. This is maximally controllable but only as authentic as you make it.

**Philosophy**: "Show what you want to show"
**Trade-off**: Repeatability over authenticity

### VCR#'s Synthesis: Both

VCR# inherited VHS's `Type` command but added `Exec` for real command execution. This creates a unique tension: you can choose, on a per-command basis, whether to prioritize control or truth.

**Philosophy**: "Different moments need different trade-offs"
**Trade-off**: Complexity for flexibility

## The Case for Simulation (Type)

When you watch someone type character-by-character, you're not just seeing output—you're following their thought process. The typing itself communicates intention.

Consider teaching someone git:
```tape
Type "git sta"
Sleep 200ms
Backspace 3
Type "status"
Enter
```

That correction—the backspace, the retype—teaches something the final command doesn't: even experts make typos, and that's okay. The typing is pedagogical content, not just input mechanics.

**What simulation provides**:
- **Narrative pacing**: You control exactly when things appear
- **Visual storytelling**: Typing speed conveys emphasis
- **Error demonstration**: Showing corrections models real workflow
- **Predictability**: Works identically every time

**What simulation sacrifices**:
- **Authenticity**: Output must be manually scripted
- **Dynamic content**: Can't show real timestamps, test results, etc.
- **Proof**: Viewers can't be certain commands actually work

Simulation is phenomenological—it's about the *experience* of commanding, not the reality of results.

## The Case for Execution (Exec)

Exec serves a different purpose: proving that something genuinely works. When you watch real test results scroll by, you're seeing truth, not theater.

```tape
Exec "dotnet test"
Wait /Test Run Successful/
```

That output is real—if tests fail, the recording shows failure. This matters when your goal isn't teaching syntax but demonstrating functionality.

**What execution provides**:
- **Authenticity**: Output is exactly what the command produces
- **Dynamic content**: Timestamps, random values, actual build numbers
- **Proof of concept**: Demonstrates that commands genuinely work
- **Automatic synchronization**: No need to manually script expected output

**What execution sacrifices**:
- **Narrative control**: Commands run at their own pace
- **Visual storytelling**: No typing animation, instant execution
- **Predictability**: Output can vary between runs
- **Error handling**: Failed commands complicate the recording

Execution is evidential—it's about *results*, not experience.

## The Philosophical Divide

The Type vs Exec choice reflects a deeper question: what is a terminal recording for?

### Recordings as Pedagogy

If recordings teach, then Type makes sense. Education is about modeling thought processes, showing corrections, controlling pacing. The typing itself is instructional—it shows *how* to think about commands, not just what they produce.

Example: Teaching someone to construct a complex pipeline
```tape
Type "cat data.json"
Sleep 300ms
Type " | jq"
Sleep 500ms  # Pause: "we need to filter"
Type " '.items[]'"
Sleep 300ms
Type " | grep"
Sleep 200ms
Type " 'active'"
Enter
```

Each pause communicates decision-making. The delays aren't technical necessity—they're pedagogical tools. Execution would rush through this, losing the narrative.

### Recordings as Evidence

If recordings prove functionality, then Exec makes sense. Documentation needs to show that commands produce claimed results. CI/CD logs need authentic build output. Security audits need proof that tests actually pass.

Example: Proving a deployment works
```tape
Exec "kubectl get pods"
Wait /Running/
Exec "curl https://my-service.com/health"
Wait /\"status\":\"healthy\"/
```

Simulating this output would be pointless—viewers need to see real pod names, real health checks. The authenticity is the value.

## Why VCR# Offers Both

Most terminal recorders force you to choose the philosophy upfront. VCR# doesn't, because real-world recordings often need both.

Consider documenting a new feature:
```tape
# Start with explanation (simulation for control)
Type "# This new feature validates input automatically"
Enter
Sleep 1s

# Show the syntax (simulation for teaching)
Type "myapp validate --input data.json"
Enter
Sleep 300ms

# Now prove it works (execution for authenticity)
Exec "myapp validate --input data.json"
Wait /✓ Validation passed/
```

The commentary uses Type because you want precise timing and narrative control. The actual command uses Exec because viewers need to see real validation output, not your prediction of what validation output should say.

**This mixed approach acknowledges that documentation serves multiple purposes simultaneously**:
- Teaching (how to use the tool)
- Proving (that it actually works)
- Demonstrating (what the output looks like)

Different purposes demand different trade-offs.

## The Determinism vs Authenticity Spectrum

Think of terminal recording approaches on a spectrum:

```
Pure Simulation          Mixed                Pure Capture
(VHS)                   (VCR#)           (asciinema, screen recorders)
│                        │                    │
Control                  Choice               Authenticity
Repeatable               Contextual           Variable
Scripted                 Selective            Spontaneous
```

**Pure simulation** (far left): Maximum control, zero runtime variation
- Recordings are perfectly reproducible
- Output is exactly what you script
- Can't prove anything actually works

**Pure capture** (far right): Maximum authenticity, zero narrative control
- Recordings show exactly what happened
- Typos, delays, errors all captured
- Can't edit or improve pacing

**VCR#** (middle): Per-command choice of simulation vs execution
- Type commands provide narrative control
- Exec commands provide authentic results
- Complexity cost: you must choose per command

This spectrum reveals that "best" depends entirely on context. Teaching syntax? Lean left. Proving functionality? Lean right. Doing both? Stay middle.

## How Other Tools Handle This Tension

Understanding alternatives helps clarify VCR#'s position:

**OBS / Screen recorders**: Pure capture
- Records everything: typos, mouse movements, other windows
- Maximally authentic, minimally controlled
- Can't script or repeat

**asciinema**: Pure capture (terminal only)
- Records terminal state, not pixels
- Still maximally authentic
- Text-based output enables replay at different speeds

**VHS**: Pure simulation
- Every character scripted
- Maximally controlled, minimally authentic
- Can fake anything, prove nothing

**VCR#**: Selective mixture
- Type for control where needed
- Exec for proof where needed
- Complexity: you must think about which to use

No approach is universally superior. They optimize for different values.

## When the Choice Matters Most

The Type vs Exec distinction becomes critical in specific scenarios:

### Scenario 1: Timing-Sensitive Demonstrations

Imagine recording a demo for a conference talk. You have exactly 2 minutes. Using Exec means commands run at their actual speed—maybe fast, maybe slow, always uncertain.

Type gives you perfect timing control. Every pause is deliberate. The recording fits your exact time slot.

**Trade-off**: Sacrificing proof for predictability

### Scenario 2: Dynamic Content

Imagine documenting a testing framework. Tests produce timestamps, random test data, varying output. Simulating this output means manually updating your tape file every time test format changes.

Exec captures actual test output automatically. When test format changes, recordings automatically reflect it.

**Trade-off**: Sacrificing control for maintainability

### Scenario 3: Building Trust

Imagine you're proving a security fix works. Viewers need to trust that tests actually pass, not that you typed believable-looking output.

Only Exec provides cryptographic-level proof (assuming they trust your screen recorder). Type can fake anything.

**Trade-off**: Sacrificing narrative polish for verifiability

## The Meta-Question

Here's the deeper insight: the fact that VCR# offers both Type and Exec forces you to think about what each part of your recording is trying to accomplish.

When you write:
```tape
Type "git status"
```

You're making a statement: "The typing itself matters here. The pacing, the visual, the narrative."

When you write:
```tape
Exec "git status"
```

You're making a different statement: "The results matter here. The authenticity, the proof, the evidence."

This consciousness—thinking about *why* you're using each command—improves documentation. You're not just recording terminal sessions; you're making editorial choices about what matters.

## Integration Patterns

While you can mix Type and Exec freely, certain patterns emerge:

**Pattern 1: Annotated Execution**
```tape
Type "# Running integration tests..."
Enter
Sleep 500ms
Exec "npm test"
Wait /passed/
```

Type provides commentary, Exec provides evidence. Common in tutorials.

**Pattern 2: Simulated Setup, Real Results**
```tape
# Simulate setup (for speed)
Type "docker-compose up -d"
Enter
Sleep 100ms

# Execute check (for proof)
Exec "docker ps"
Wait /healthy/
```

Simulating slow setup commands saves time; real health checks prove it worked.

**Pattern 3: Full Simulation for Consistency**
```tape
Type "date"
Enter
# Manually specify output for consistency
Type "Mon Nov 13 14:22:31 PST 2024"
Sleep 1s
```

Some users prefer full control even when Exec is available. This is valid—especially when output formatting matters more than accuracy.

## The Deeper Principle

The existence of both Type and Exec reveals VCR#'s design philosophy: **tools should adapt to users, not vice versa**.

Other recorders force you into their worldview:
- asciinema says "recordings must be authentic"
- VHS says "recordings must be scripted"
- Screen recorders say "recordings must capture everything"

VCR# says "recordings serve different purposes, choose appropriately."

This flexibility is also complexity. Every command is a decision. But that decision-making is where thoughtful documentation happens. The tool doesn't decide what matters—you do.

## Conclusion: No Universal Answer

Should you use Type or Exec? The question itself is unanswerable without context. It's like asking "should I use a photograph or a sketch?" It depends entirely on what you're trying to communicate.

Use Type when the process matters—when teaching, demonstrating pacing, showing corrections, controlling narrative.

Use Exec when results matter—when proving functionality, capturing dynamic content, providing evidence.

Use both when your recording serves multiple purposes simultaneously, which most real-world documentation does.

VCR#'s refusal to choose for you is the feature. It acknowledges that you understand your documentation needs better than any tool can.
