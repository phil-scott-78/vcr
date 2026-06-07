# Static SVG widget: one settled frame, cropped to content (Mode static + Size fit).
# Demonstrates the embedding pattern documented in how--to/screenshots.md.
# Used in: how--to/screenshots.md ("Static SVG widgets for embedding")
Output "static-widget.svg"

Use doc
Set Mode static            # run Exec, let it settle, emit one static frame
Set Size fit               # crop the SVG to the measured content extent
Set Cols 50
Set Rows 20                # over-provisioned; Size fit trims the blank rows

Exec "1..3 | ForEach-Object { [pscustomobject]@{ Id = $_; Name = 'item-' + $_; Status = 'ok' } } | Format-Table"
