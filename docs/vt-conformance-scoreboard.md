# VcrSharp VT engine — libvterm conformance scoreboard

> Corpus: **43** vendored libvterm `*.test` files (MIT). Rate = passed / evaluated; *skipped* assertions depend on engine features not yet modelled (callback events, pen/cell colors, line info). See docs/vt-engine-conformance.md §4.

## Overall: 278/385 evaluated assertions pass (**72.2%**) · 875 skipped · 0 errors

| File | Pass | Fail | Skip | Err | Rate |
|---|---:|---:|---:|---:|---:|
| 02parser.test | 0 | 0 | 76 | 0 | — |
| 03encoding_utf8.test | 0 | 0 | 20 | 0 | — |
| 10state_putglyph.test | 0 | 0 | 22 | 0 | — |
| 11state_movecursor.test | 75 | 2 | 0 | 0 | 97% |
| 12state_scroll.test | 18 | 4 | 32 | 0 | 82% |
| 13state_edit.test | 56 | 0 | 68 | 0 | 100% |
| 14state_encoding.test | 0 | 0 | 28 | 0 | — |
| 15state_mode.test | 8 | 5 | 21 | 0 | 62% |
| 16state_resize.test | 2 | 5 | 17 | 0 | 29% |
| 17state_mouse.test | 0 | 0 | 65 | 0 | — |
| 18state_termprops.test | 0 | 0 | 16 | 0 | — |
| 20state_wrapping.test | 8 | 0 | 28 | 0 | 100% |
| 21state_tabstops.test | 6 | 0 | 18 | 0 | 100% |
| 22state_save.test | 7 | 3 | 22 | 0 | 70% |
| 25state_input.test | 0 | 0 | 53 | 0 | — |
| 26state_query.test | 0 | 0 | 13 | 0 | — |
| 27state_reset.test | 4 | 0 | 2 | 0 | 100% |
| 28state_dbl_wh.test | 3 | 1 | 29 | 0 | 75% |
| 29state_fallback.test | 0 | 0 | 7 | 0 | — |
| 30state_pen.test | 0 | 0 | 60 | 0 | — |
| 31state_rep.test | 0 | 0 | 94 | 0 | — |
| 32state_flow.test | 0 | 0 | 6 | 0 | — |
| 40state_selection.test | 0 | 0 | 32 | 0 | — |
| 60screen_ascii.test | 24 | 2 | 6 | 0 | 92% |
| 61screen_unicode.test | 2 | 4 | 7 | 0 | 33% |
| 62screen_damage.test | 1 | 0 | 55 | 0 | 100% |
| 63screen_resize.test | 18 | 18 | 17 | 0 | 50% |
| 64screen_pen.test | 0 | 0 | 25 | 0 | — |
| 65screen_protect.test | 3 | 1 | 0 | 0 | 75% |
| 66screen_extent.test | 0 | 0 | 5 | 0 | — |
| 67screen_dbl_wh.test | 0 | 0 | 8 | 0 | — |
| 68screen_termprops.test | 0 | 0 | 6 | 0 | — |
| 69screen_pushline.test | 0 | 0 | 4 | 0 | — |
| 69screen_reflow.test | 13 | 29 | 10 | 0 | 31% |
| 90vttest_01-movement-1.test | 17 | 8 | 0 | 0 | 68% |
| 90vttest_01-movement-2.test | 1 | 19 | 0 | 0 | 5% |
| 90vttest_01-movement-3.test | 4 | 1 | 0 | 0 | 80% |
| 90vttest_01-movement-4.test | 1 | 0 | 0 | 0 | 100% |
| 90vttest_02-screen-1.test | 3 | 2 | 0 | 0 | 60% |
| 90vttest_02-screen-2.test | 2 | 1 | 0 | 0 | 67% |
| 90vttest_02-screen-3.test | 0 | 2 | 0 | 0 | 0% |
| 90vttest_02-screen-4.test | 2 | 0 | 0 | 0 | 100% |
| 92lp1640917.test | 0 | 0 | 3 | 0 | — |
| **TOTAL** | **278** | **107** | **875** | **0** | **72.2%** |

## Sample failures (screen tests)

**60screen_ascii.test**
- [Altscreen] line 65: ?screen_row 0 expected "" actual "P"
- [Altscreen] line 69: ?screen_row 0 expected "P" actual "A"

**61screen_unicode.test**
- [Single width UTF-8] line 11: ?screen_text 0,0,1,80 expected "ÃÃ©" actual "Áé"
- [Wide char] line 20: ?screen_text 0,0,1,80 expected "ï¼23" actual "０23"
- [Combining char] line 28: ?screen_row 0 expected "é123" actual "é23"

**63screen_resize.test**
- [Resize wider preserves cells] line 12: ?screen_chars 0,0,1,100 expected "AB" actual ""
- [Resize wider preserves cells] line 13: ?screen_chars 1,0,2,100 expected "CD" actual ""
- [Resize wider allows print in new area] line 22: ?screen_chars 0,0,1,2 expected "AB" actual ""

**65screen_protect.test**
- [Selective erase] line 9: ?screen_row 0 expected " B" actual ""
