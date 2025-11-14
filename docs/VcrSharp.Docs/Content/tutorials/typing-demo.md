---
title: "Create Your First Typing Demo"
description: "Learn the basics of VCR# by creating an animated typing demonstration"
uid: "docs.tutorials.typing-demo"
order: 1150
---

In this tutorial, you'll create an animated GIF showing realistic terminal typing. The recording will demonstrate navigating and editing a command before executing it—perfect for creating polished documentation and tutorials.

Your finished recording will show:
1. Typing a command with a typo
2. Using arrow keys to navigate back
3. Correcting the mistake with backspace
4. Running the corrected command

This technique is perfect for creating documentation demos and tutorials. This tutorial takes about 15 minutes.

## Prerequisites

This tutorial assumes you have VCR# installed. If not, complete the [Getting Started](./getting-started.md) tutorial first (it takes 5 minutes).

You'll need a terminal and about 15 minutes.

## Step 1: Create Your Tape File

Create a new file called `demo.tape`:

```tape
Output typing-demo.gif

Set Cols 100
Set Rows 30
Set Theme Dracula
```

These lines set up the output file and terminal appearance. We'll add commands next.

## Step 2: Add Typing Commands

Add these lines to your `demo.tape` file:

```tape
Type "echo 'Helo, World!'"
Sleep 500ms
```

Run the recording now to see what happens:

```bash
vcr demo.tape
```

Open `typing-demo.gif`. Notice how the text appears character-by-character with a realistic typing speed. The `Sleep 500ms` creates a half-second pause after typing.

Notice the typo—"Helo" instead of "Hello". We'll fix that in the next step by simulating realistic editing.

## Step 3: Add Navigation and Editing

Replace your tape file content with this:

```tape
Output typing-demo.gif

Set Cols 100
Set Rows 30
Set Theme Dracula

Type "echo 'Helo, World!'"
Sleep 500ms
Left 9
Sleep 300ms
Backspace
Sleep 100ms
Type "ll"
Sleep 500ms
Home
Sleep 300ms
Enter
Sleep 1s
```

Run the recording again:

```bash
vcr demo.tape
```

Watch the GIF carefully. You'll see the cursor move back with `Left 9`, delete the character with `Backspace`, type "ll" to complete "Hello", then jump to the start of the line with `Home` before executing the command with `Enter`.

Notice how the `Sleep` commands create natural pauses that make the editing look realistic. A real person pauses to think while navigating and editing.

## Step 4: Customize the Theme

Now let's change the visual appearance. Update the theme line:

```tape
Set Theme Monokai
```

Run the recording once more:

```bash
vcr demo.tape
```

Compare the new GIF to the previous one. The terminal now uses Monokai's color scheme instead of Dracula. You've just learned how to control the visual style of your recordings.

## Next Steps

Excellent work! You've learned how to:
- Create typing animations with realistic timing
- Simulate cursor navigation and text editing
- Control terminal appearance with themes

Your typing demo technique works great for documentation, but what if you want to capture real command output? The [Exec Commands tutorial](exec-commands) shows you how to record actual program execution.

## Complete Code

Here's the full `demo.tape` file:

```tape
Output typing-demo.gif

Set Cols 100
Set Rows 30
Set Theme Monokai

Type "echo 'Helo, World!'"
Sleep 500ms
Left 9
Sleep 300ms
Backspace
Sleep 100ms
Type "ll"
Sleep 500ms
Home
Sleep 300ms
Enter
Sleep 1s
```
