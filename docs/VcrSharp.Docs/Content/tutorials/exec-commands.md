---
title: "Capture Real Command Output with Exec"
description: "Learn how to record actual command execution and output using the Exec command"
uid: "docs.tutorials.exec-commands"
order: 1250
---

In this tutorial, you'll record a real git workflow showing actual command output. Unlike the typing demo tutorial where
we simulated typing, here you'll execute actual commands and capture their real output.

Your finished recording will show:

1. Checking git status
2. Creating a new file
3. Staging and committing the changes
4. Viewing the commit log

This is perfect for creating accurate documentation, demos, and tutorials showing real tool behavior. This tutorial
takes about 20 minutes.

## Prerequisites

You should complete the [Typing Demo tutorial](typing-demo) first to understand basic VCR# concepts.

You'll also need:

- Git installed and configured
- A test repository (we'll create one)
- About 20 minutes

## Step 1: Create a Test Repository

First, let's create a test git repository to work with:

```bash
mkdir vcr-git-demo
cd vcr-git-demo
git init
git config user.name "Demo User"
git config user.email "demo@example.com"
```

Create a tape file called `git-workflow.tape`:

```tape
Output git-workflow.gif

Set Cols 100
Set Rows 30
Set Theme "Dracula"
Set Shell "bash"
```

We're ready to record real commands.

## Step 2: Execute Your First Real Command

Add this line to `git-workflow.tape`:

```tape
Exec "git status"
Sleep 1s
```

Run the recording:

```bash
vcr git-workflow.tape
```

Open the GIF. Notice something different from the typing demo: nothing is "typed" — the real `git status` output from
your repository simply appears.

Unlike `Type`, which simulates keystrokes, `Exec` runs a real program and captures its actual output. The `Exec`
command runs as the shell's foreground process — the launch line itself is never echoed, so you only see the
program's real output. When a tape has several `Exec` commands, VCR# joins them and runs them in sequence. After the
output stops changing, VCR# waits for it to settle (controlled by `InactivityTimeout`) before ending the recording.
Here's a simple example using `dotnet --version`:

<VcrTape src="../demos/exec-real-command.svg" />

## Step 3: Chain Multiple Commands

Update your tape file to create and commit a file:

```tape
Output git-workflow.gif

Set Cols 100
Set Rows 30
Set Theme "Dracula"
Set Shell "bash"

Exec "git status"
Sleep 500ms

Exec "echo 'Hello from VCR#' > README.md"
Sleep 500ms

Exec "git add README.md"
Sleep 500ms

Exec "git commit -m 'Add README'"
Sleep 500ms

Exec "git log --oneline -n 1"
Sleep 2s
```

Run the recording again:

```bash
vcr git-workflow.tape
```

Watch the GIF. The commands run in order within the shell — the file is created, staged, committed, and the commit
appears in the log — while VCR# records the real output. The `Sleep` commands between them pace the *recording*, holding
each result on screen long enough for viewers to read it.

Because the `Exec` commands run as the shell's foreground process in sequence rather than being typed one at a time,
the `Sleep` durations control the recording timeline, not the commands themselves. If you need to hold the recording
until specific output appears, use a `Wait /pattern/` command (covered next) instead of guessing with `Sleep`. For
more on waiting strategies, see [How to wait for output](xref:docs.how-to.waiting).

## Step 4: Wait for Specific Output

Sometimes you want to wait for specific text to appear before continuing. Let's add a `Wait` command:

```tape
Output git-workflow.gif

Set Cols 100
Set Rows 30
Set Theme "Dracula"
Set Shell "bash"

Exec "git status"
Sleep 500ms

Exec "echo 'Hello from VCR#' > README.md"
Sleep 500ms

Exec "git add README.md"
Sleep 500ms

Exec "git commit -m 'Add README'"
Wait /Add README/
Sleep 500ms

Exec "git log --oneline -n 1"
Sleep 2s
```

Notice the `Wait /Add README/` line. `Wait` takes a regular expression between slashes (not a quoted string), and it
pauses the recording until that pattern appears in the terminal output. This is more reliable than a fixed `Sleep` when
output timing varies. (Regex matching is case-sensitive, so the pattern must match the text exactly.)

Run the recording:

```bash
vcr git-workflow.tape
```

The recording works the same, but now it explicitly waits for the commit message to appear. This technique is useful
when commands produce output gradually or when timing is unpredictable.

## Next Steps

Great work! You've learned to:

- Execute real commands with `Exec`
- Chain multiple commands in sequence
- Wait for specific output with `Wait`
- Create accurate recordings of real workflows

You now know both simulated typing (from the typing demo) and real execution. For the full list of commands and their
options, see the [tape syntax reference](xref:docs.reference.tape-syntax).

## Complete Code

Here's the full `git-workflow.tape` file:

```tape
Output git-workflow.gif

Set Cols 100
Set Rows 30
Set Theme "Dracula"
Set Shell "bash"

Exec "git status"
Sleep 500ms

Exec "echo 'Hello from VCR#' > README.md"
Sleep 500ms

Exec "git add README.md"
Sleep 500ms

Exec "git commit -m 'Add README'"
Wait /Add README/
Sleep 500ms

Exec "git log --oneline -n 1"
Sleep 2s
```
