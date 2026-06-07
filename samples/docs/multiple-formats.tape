# One tape, rendered for the docs as SVG. The same tape can declare additional
# Output lines (Output demo.gif / demo.mp4 / demo.webm) to emit every format at
# once; only the SVG is embedded here because SVG needs no FFmpeg.
# Used in: how--to/multiple-formats.md
Output "multiple-formats.svg"

Use doc
Set Cols 100
Set Rows 12

Run "echo 'one .tape -> SVG, GIF, MP4, WebM'"
