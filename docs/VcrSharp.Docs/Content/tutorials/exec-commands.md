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
Set Theme Dracula
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

Open the GIF. Notice something different from the typing demo: the output appeared instantly, and it shows the actual
git status output from your repository.

The `Exec` command runs the actual program and captures its real output. VCR# waits for the command to finish before
continuing to the next line.

## Step 3: Chain Multiple Commands

Update your tape file to create and commit a file:

```tape
Output git-workflow.gif

Set Cols 100
Set Rows 30
Set Theme Dracula
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

Watch the GIF. You'll see each command execute in sequence with their real output: the file is created, staged,
committed, and the commit appears in the log. Each `Sleep` command creates a pause so viewers can read the output.

Notice how VCR# waits for each command to complete before moving to the next one. You don't have to guess how long
commands will take.

## Step 4: Wait for Specific Output

Sometimes you want to wait for specific text to appear before continuing. Let's add a `Wait` command:

```tape
Output git-workflow.gif

Set Cols 100
Set Rows 30
Set Theme Dracula
Set Shell "bash"

Exec "git status"
Sleep 500ms

Exec "echo 'Hello from VCR#' > README.md"
Sleep 500ms

Exec "git add README.md"
Sleep 500ms

Exec "git commit -m 'Add README'"
Wait "Add README"
Sleep 500ms

Exec "git log --oneline -n 1"
Sleep 2s
```

Notice the `Wait "Add README"` line. This tells VCR# to pause until it sees "Add README" in the terminal output before
continuing. This is more reliable than using fixed delays when output timing varies.

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

You now know both simulated typing (from the typing demo) and real execution.

## Complete Code

Here's the full `git-workflow.tape` file:

```tape
Output git-workflow.gif

Set Cols 100
Set Rows 30
Set Theme Dracula
Set Shell "bash"

Exec "git status"
Sleep 500ms

Exec "echo 'Hello from VCR#' > README.md"
Sleep 500ms

Exec "git add README.md"
Sleep 500ms

Exec "git commit -m 'Add README'"
Wait "Add README"
Sleep 500ms

Exec "git log --oneline -n 1"
Sleep 2s
```
