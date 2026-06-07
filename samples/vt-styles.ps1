# SGR text-style sampler for VcrSharp's SVG renderer.
#
# Emits every styled-text attribute the engine tracks and the SVG path renders, so a recording of
# this script is a one-glance check that reverse / dim / strikethrough / overline / conceal (and the
# older bold / italic / underline) all survive into the .svg output — at parity with the GIF/PNG
# (raster) path.
#
# Run directly (eyeball in any terminal):   pwsh -NoProfile -File samples/vt-styles.ps1
# Or record it:                             vcr native-play samples/vt-styles.tape -o vt-styles.svg

$e = [char]27
function Sgr([string]$codes, [string]$text) { "$e[${codes}m$text$e[0m" }

Write-Host ("{0}   {1}" -f (Sgr '7'    ' reverse '),      (Sgr '7;31' ' reverse red-fg '))
Write-Host ("{0}   {1}" -f (Sgr '2'    'dim / faint'),    'vs normal')
Write-Host ("{0}   {1}" -f (Sgr '9'    'strikethrough'),  'vs normal')
Write-Host ("{0}   {1}" -f (Sgr '53'   'overline'),       'vs normal')
Write-Host ("{0}   {1}" -f (Sgr '4'    'underline'),      'vs normal')
Write-Host ("{0}   {1}" -f (Sgr '8'    'concealed'),      '(hidden between the parens)')
Write-Host ""
Write-Host ("{0}   {1}   {2}" -f (Sgr '1' 'bold'), (Sgr '3' 'italic'), (Sgr '4;9' 'underline+strike'))
Write-Host ("{0}   {1}" -f (Sgr '7;2' ' reverse + dim '), (Sgr '1;4;53' 'bold underline overline'))
