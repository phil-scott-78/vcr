# VcrSharp VT engine — libvterm conformance scoreboard

> Corpus: **43** vendored libvterm `*.test` files (MIT). Rate = passed / evaluated; *skipped* assertions depend on engine features not yet modelled (callback events, pen/cell colors, line info). See docs/vt-engine-conformance.md §4.

## Overall: 367/421 evaluated assertions pass (**87.2%**) · 839 skipped · 0 errors

| File | Pass | Fail | Skip | Err | Rate |
|---|---:|---:|---:|---:|---:|
| 02parser.test | 0 | 0 | 76 | 0 | — |
| 03encoding_utf8.test | 0 | 0 | 20 | 0 | — |
| 10state_putglyph.test | 0 | 0 | 22 | 0 | — |
| 11state_movecursor.test | 75 | 2 | 0 | 0 | 97% |
| 12state_scroll.test | 18 | 4 | 32 | 0 | 82% |
| 13state_edit.test | 56 | 0 | 68 | 0 | 100% |
| 14state_encoding.test | 0 | 0 | 28 | 0 | — |
| 15state_mode.test | 12 | 1 | 21 | 0 | 92% |
| 16state_resize.test | 5 | 2 | 17 | 0 | 71% |
| 17state_mouse.test | 0 | 0 | 65 | 0 | — |
| 18state_termprops.test | 0 | 0 | 16 | 0 | — |
| 20state_wrapping.test | 8 | 0 | 28 | 0 | 100% |
| 21state_tabstops.test | 6 | 0 | 18 | 0 | 100% |
| 22state_save.test | 15 | 0 | 17 | 0 | 100% |
| 25state_input.test | 0 | 0 | 53 | 0 | — |
| 26state_query.test | 0 | 0 | 13 | 0 | — |
| 27state_reset.test | 4 | 0 | 2 | 0 | 100% |
| 28state_dbl_wh.test | 3 | 1 | 29 | 0 | 75% |
| 29state_fallback.test | 0 | 0 | 7 | 0 | — |
| 30state_pen.test | 31 | 0 | 29 | 0 | 100% |
| 31state_rep.test | 0 | 0 | 94 | 0 | — |
| 32state_flow.test | 0 | 0 | 6 | 0 | — |
| 40state_selection.test | 0 | 0 | 32 | 0 | — |
| 60screen_ascii.test | 26 | 0 | 6 | 0 | 100% |
| 61screen_unicode.test | 3 | 3 | 7 | 0 | 50% |
| 62screen_damage.test | 1 | 0 | 55 | 0 | 100% |
| 63screen_resize.test | 31 | 5 | 17 | 0 | 86% |
| 64screen_pen.test | 0 | 0 | 25 | 0 | — |
| 65screen_protect.test | 3 | 1 | 0 | 0 | 75% |
| 66screen_extent.test | 0 | 0 | 5 | 0 | — |
| 67screen_dbl_wh.test | 0 | 0 | 8 | 0 | — |
| 68screen_termprops.test | 0 | 0 | 6 | 0 | — |
| 69screen_pushline.test | 0 | 0 | 4 | 0 | — |
| 69screen_reflow.test | 17 | 25 | 10 | 0 | 40% |
| 90vttest_01-movement-1.test | 17 | 8 | 0 | 0 | 68% |
| 90vttest_01-movement-2.test | 20 | 0 | 0 | 0 | 100% |
| 90vttest_01-movement-3.test | 4 | 1 | 0 | 0 | 80% |
| 90vttest_01-movement-4.test | 1 | 0 | 0 | 0 | 100% |
| 90vttest_02-screen-1.test | 5 | 0 | 0 | 0 | 100% |
| 90vttest_02-screen-2.test | 2 | 1 | 0 | 0 | 67% |
| 90vttest_02-screen-3.test | 2 | 0 | 0 | 0 | 100% |
| 90vttest_02-screen-4.test | 2 | 0 | 0 | 0 | 100% |
| 92lp1640917.test | 0 | 0 | 3 | 0 | — |
| **TOTAL** | **367** | **54** | **839** | **0** | **87.2%** |

## Sample failures (screen tests)

**61screen_unicode.test**
- [Single width UTF-8] line 11: ?screen_text 0,0,1,80 expected "ÃÃ©" actual "Áé"
- [Wide char] line 20: ?screen_text 0,0,1,80 expected "ï¼23" actual "０23"
- [Combining char] line 29: ?screen_text 0,0,1,80 expected "eÌ123" actual "é123"

**63screen_resize.test**
- [Resize wider allows print in new area] line 25: ?screen_chars 0,78,1,81 expected "CDE" actual "CE"
- [Resize taller attempts to pop scrollback] line 103: ?screen_row 0 expected "ABCDE" actual "Line 1"
- [Resize taller attempts to pop scrollback] line 104: ?screen_row 5 expected "Line 1" actual ""

**65screen_protect.test**
- [Selective erase] line 9: ?screen_row 0 expected " B" actual ""

**69screen_reflow.test**
- [Resize wider reflows wide lines] line 12: ?screen_row 0 expected "AAAAAAAAAA" actual "\"A\"x12"
- [Resize wider reflows wide lines] line 13: ?screen_row 1 expected "AA" actual ""
- [Resize wider reflows wide lines] line 15: ?cursor expected 1,2 actual 0,6
