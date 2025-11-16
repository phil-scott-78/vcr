---
title: "Getting Started"
description: "Quick start guide to creating your first terminal recording with VCR#"
uid: "docs.tutorials.getting-started"
order: 1000
---

In this tutorial, you'll create a simple terminal recording that outputs a GIF. You'll install VCR#, write a basic tape
file, and run your first recording. This takes about 5 minutes.

By the end, you'll have a working animated GIF showing "Hello, VCR#!" appearing in a terminal.

## Prerequisites

You'll need:

- .NET 9 SDK installed
- Windows, macOS, or Linux
- A terminal/command prompt

### Installing FFmpeg

[FFmpeg](https://ffmpeg.org/) is required for encoding videos and GIFs.

```bash tabs=true title=Windows (using Chocolatey)
choco install ffmpeg
```


```bash title=macOS (using Homebrew)
brew install ffmpeg
```

```bash title=Linux (using apt)
sudo apt update
sudo apt install ffmpeg
```

Verify installation:
```bash
ffmpeg -version
```

### Installing ttyd

[ttyd](https://github.com/tsl0922/ttyd) (>= 1.7.2) is required for terminal emulation.

```bash tabs=true title=Windows
choco install ttyd
```


```bash title=macOS
brew install ttyd
```

```bash title=Linux
sudo apt update
sudo apt install ttyd
```

Verify installation:
```bash
ttyd --version
```

## Installation

### Install VCR#

Install VCR# globally using the .NET CLI:

```bash
dotnet tool install --global vcr
```

### Verify Installation

Confirm all dependencies are installed correctly:

```bash
# Check VCR#
vcr --version

# Check FFmpeg
ffmpeg -version

# Check ttyd
ttyd --version
```

All three commands should display version information. If any command fails, revisit the Prerequisites section above.

## Your First Recording

### Create a Tape File

Create a new file called `hello.tape` with this content:

```tape
Output hello.gif

Set Cols 80
Set Rows 20
Set Theme Dracula

Type "echo 'Hello, VCR#!'"
Enter
Sleep 1s
```

Let's look at what each line does:

- `Output hello.gif` - Names your output file
- `Set Cols/Rows` - Sets terminal size (80 columns × 20 rows)
- `Set Theme` - Chooses the Dracula color scheme
- `Type` - Simulates typing text character-by-character
- `Enter` - Presses the Enter key
- `Sleep 1s` - Pauses for 1 second

### Run the Recording

Now run VCR# with your tape file:

```bash
vcr hello.tape
```

You'll see VCR# start a browser, execute your commands, and encode the video. This takes 10-20 seconds.

Watch for the message: `✓ Recording complete: hello.gif`

### View Your Output

Open `hello.gif` in your browser or image viewer. You should see:

1. An empty terminal appears
2. Text is typed: `echo 'Hello, VCR#!'`
3. The command executes
4. Output appears: `Hello, VCR#!`
5. The terminal pauses briefly

Notice how the typing appears character-by-character, just like a real terminal session. This creates the animated
effect that makes your recordings look realistic.

## What's Next?

Congratulations! You've created your first terminal recording.

Ready to build something more interesting? Try the [Typing Demo tutorial](typing-demo) to learn how to create
realistic terminal interactions with navigation and editing.
