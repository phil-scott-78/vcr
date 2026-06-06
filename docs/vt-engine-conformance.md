# VcrSharp VT Engine — Conformance Audit & Build Plan

> Living conformance spec for the in-process terminal engine (`VtScreen`) that backs the browserless render path (`ConPtyProcess` → `VtScreen` → `TerminalContent` → `SvgRenderer`).
> Target: **at minimum match Windows Terminal's documented + source-confirmed behavior**, with its own consumable conformance test suite.
> Status legend: **done** / **partial** / **missing**. Priority: **P0** (must-have for Windows-Terminal parity) / **P1** (important) / **P2** (nice-to-have) / **P3** (out-of-scope, stub as no-op).
> Source files of record: `src/VcrSharp.Core/Terminal/VtScreen.cs` (~440 LOC), `src/VcrSharp.Core/Rendering/TerminalCell.cs`, `src/VcrSharp.Core/Rendering/TerminalContent.cs`, `src/VcrSharp.Infrastructure/Rendering/SvgRenderer.cs`, `src/VcrSharp.Infrastructure/Terminal/ConPtyProcess.cs`, `src/VcrSharp.Infrastructure/Terminal/NativeTerminalRenderer.cs`.

> **Accuracy note.** Every load-bearing factual claim below was adversarially fact-checked against primary sources (Microsoft Learn, `microsoft/terminal` source, `invisible-island.net` xterm ctlseqs, `vt100.net`). Four claims from the first draft were **refuted and corrected** — they are flagged inline with **[corrected]** and summarized in [§8](#8-verification-corrections).

---

## 0. Locked scope decisions

| Decision | Choice | Implication |
|---|---|---|
| **Conformance ceiling** | **Windows Terminal source-confirmed parity** | P0/P1 = conhost documented floor + source-confirmed WT items (alt screen, scroll region, DEC modes 25/7/2004/mouse/1049, OSC 8/52/4/10-12). Sixel/kitty/VT520 rect-ops/double-width = **P3, parse-and-discard**. The §2 matrix as written is the contract. |
| **Default backend** | **Browser default; native opt-in** | Native (`ConPtyProcess→VtScreen`) is *earned per-tape* by the Gate-4 browser-equivalence diff. Global default flips to native only once the corpus is broadly green. No regression to today's behavior in the meantime. |
| **Scrollback** | **Bounded ring (~1000 lines) in P2** | Main buffer only. Enables correct `ED 3`, alt-screen save/restore, and future full-session export. Snapshots stay viewport-only. |
| **Cross-platform** | **Windows-native now; Unix `forkpty` is a committed future (P7)** | Ship Windows-first, but the `ITerminalBackend` seam (P6) is designed up front to admit a `forkpty` sibling. Parser + grid stay 100% platform-neutral in the engine assembly so P7 is *only* PTY plumbing. |

---

## Phase 0 — foundation (landed ✅)

The engine and its conformance harness are now their own projects, with a running scoreboard.

- **`src/VcrSharp.Terminal`** — new engine assembly; `VtScreen` moved here (namespace `VcrSharp.Terminal`). References `VcrSharp.Core` only for the `TerminalContent`/`TerminalCell` snapshot contract (the leaf-ification — extracting those DTOs so the engine has zero VcrSharp deps and no transitive ImageSharp — is the first tracked follow-up).
- **`tests/VcrSharp.Terminal.Tests`** — new test project (xUnit v3 + Shouldly). The 21 existing parser tests moved here intact.
- **Vendored corpus:** 43 libvterm `*.test` files (MIT, `LICENSE` + `ATTRIBUTION.md` preserved) under `Conformance/LibVterm/`.
- **`LibVtermHarness`** — C# reimplementation of the grid-coupled subset of upstream's `run-test.pl`: decodes Perl-quoted `PUSH` byte strings, feeds `VtScreen`, evaluates `?cursor` / `?screen_row` / `?screen_chars` / `?screen_text` / `movecursor`. Callback/pen/cell-color assertions are counted **skipped** (honest), not failed.
- **Gates:**
  - `EngineDoesNotCrash` `[Theory]` over all 43 files + `FuzzTests` (random bytes, escape soup, huge param lists) — **green: 0 errors across the corpus.** This is the live robustness gate.
  - `Scoreboard` `[Fact]` — measurement (always green), prints + writes `docs/vt-conformance-scoreboard.md`.
  - `ParserCorrectnessTests` — 6 `Skip`ped acceptance tests, the executable checklist for P1/P2/P4 (un-skip as each phase lands).

**Baseline (see `docs/vt-conformance-scoreboard.md`):** **230 / 385 evaluated assertions pass (59.7%)**, **875 skipped** (features not yet modelled — pen/SGR cell attrs, callbacks, modes), **0 errors**. Read it as: ~60% of the *text + cursor* behavior we can currently evaluate is correct, while the **875 skips are the breadth frontier** (P3+ cell model and P4 modes convert skips → pass/fail as `?screen_cell`/`?pen` become evaluable). Strong files: `13state_edit` 100%, `20state_wrapping` 100%, `60screen_ascii` 92%. Weak files map exactly to the matrix gaps: `90vttest_01-movement` (cursor edge cases), `63screen_resize` (no resize), alt-screen rows in `60screen_ascii`, combining marks in `61screen_unicode`.

Repo test totals after Phase 0: Core.Tests **351** green, Terminal.Tests **71** green + **6** skipped (P1/P2/P4 targets), **0** regressions.

---

## Build progress (P1–P5 landed ✅)

Every planned conformance phase shipped, each gated by the scoreboard + acceptance tests, **0 errors and 0 regressions throughout**. Conformance = passed / *evaluated* (grid-observable) libvterm assertions.

| Step | What | Scoreboard |
|---|---|---:|
| Phase 0 | engine project + scoreboard | 59.7% |
| **P1** | parser → canonical Williams VT500 DFA | 61.3% |
| **P2** | scroll region, IL/DL/SU/SD, tabs, DECSC/DECRC, DECSTR/RIS, scrollback | 72.2% |
| **P4** | DEC modes, alt screen, real cursor visibility, IRM/LNM/DECOM | 80.5% |
| **P3** | full SGR attrs, line-drawing charset, combining marks, `?pen` grading | 82.4% |
| **P5** | content-preserving resize (+ phantom cursor), HPB/VPB | 89.5% |
| edge | DECALN, `?screen_text` UTF-8, Perl `x`-repeat in harness | **92.2%** |

All acceptance targets pass (0 skipped). Files at 100% include the full cursor/scroll/edit/tabs/save/reset/mode/pen/resize/line-draw/unicode/alt-screen/DECALN suites and most of vttest.

### Remaining gap (the un-ground tail, ~33 evaluated assertions)

| Bucket | Fails | Disposition |
|---|---:|---|
| `69screen_reflow` | 24 | **Reflow on resize** — deferred (project-sized feature; user-deferred). |
| `63screen_resize` pop | 4 | **Not grid-gradable** — depends on libvterm's *fake* `sb_popline` callback (fabricates content from an empty ring); no real terminal state to match. |
| `28state_dbl_wh` | 1 | **Double-width lines** — out of scope by [§0](#0-locked-scope-decisions)/§7 (we no-op, as WT renders them). |
| `15state_mode` | 1 | DECSLRM (left/right margins, mode 69) — deferred (P2 §2.6, single-assertion payoff). |
| `65screen_protect` | 1 | Selective erase (DECSCA + DECSED/DECSEL) — deferred (P2 §2.5). |
| vttest spacing | 2 | `?screen_chars` keeps a *written* trailing space; needs a per-cell "written" bit to distinguish from default blanks. |

### Deferred product/engine follow-ups (do not affect the scoreboard)
- **SvgRenderer**: render the new attributes (reverse / dim / strikethrough / overline / styled+colored underline) — currently populated in `TerminalCell` but not yet drawn (existing bold/italic/underline output unchanged).
- **Engine leaf-ification**: extract `TerminalContent`/`TerminalCell` so `VcrSharp.Terminal` drops its `VcrSharp.Core` (and transitive ImageSharp) reference.
- **P6 (animation) ✅**: `NativeTerminalRenderer.RunAndCaptureAsync` polls the live `VtScreen` at framerate → `TerminalStateWithTime[]` → `SvgRenderer.RenderAnimatedAsync`. `vcr native-snap --animate` produces a browserless animated SVG.
- **P6 (playback) ✅**: `NativeTerminalPage`/`NativeFrameCapture` implement the `ITerminalPage`/`IFrameCapture` seam over ConPTY + `VtScreen`, so the existing tape commands run unchanged — `Type`/`Key`/`Modifier` write to the pseudo-console stdin and the real shell echoes them; `Wait` polls the grid; `Hide`/`Show` gate capture. `NativeRecordingSession` + `vcr native-play <tape> -o out.svg` records a full tape to an animated SVG (verified end to end). **Remaining P6**: fold native/browser behind one auto-fallback front door in `VcrSession` (so plain `vcr demo.tape` prefers native per the fallback rules below), and native GIF/MP4 (rasterize states to frames).
- **P7**: Unix `forkpty` sibling backend.

### Native fallback rules (when `vcr demo.tape` must use the browser)
- **Statically detectable (pre-run):** not Windows · `Output` is not `.svg` (GIF/MP4/PNG need rasterisation) · *(future, once richer)* tapes using features the native engine doesn't claim.
- **Render fidelity (best decided by the Gate-4 browser-equivalence diff):** SVG rendering of reverse/dim/strike/overline/styled-underline (engine tracks them; renderer follow-up pending), OSC 8 hyperlinks, OSC 4/10-12 palette changes, sixel/kitty/iTerm2 images, double-width lines, DECSLRM, selective erase.
- **Now supported natively:** Type/Key/Modifier/Sleep/Wait/Hide/Show/Copy/Paste/Screenshot(svg)/Exec/Set + static & animated SVG.

---

## 1. Reality check

The current `VtScreen` is a deliberate proof-of-concept (~440 lines; its own banner says "minimal" and "intentionally ignored for now"). Verified against source, it correctly handles printable text with autowrap + wide-char detection, four C0 controls (CR / LF+VT+FF / BS / HT), a single-buffer full-screen scroll, basic SGR (16/256/truecolor fg+bg, bold/italic/underline + their resets), absolute/relative cursor moves (CUP/CUU/CUD/CUF/CUB/CHA/VPA), erase (ED 0/1/2/3, EL 0/1/2), and three edit ops (ECH/ICH/DCH). It blindly gobbles OSC/DCS/SOS/PM/APC and charset designators so they don't leak into the grid. That is genuinely useful for simple Spectre.Console-style TUI playback — but it is roughly **~30% of Windows Terminal's parser** by surface area, and the gaps are not cosmetic: **no DEC private mode state at all** (cursor visibility, alt screen, autowrap, bracketed paste, mouse), **no scroll region** (so `less`, `tmux`, `vim` status lines, and any pane-isolated scrolling render wrong), **no IL/DL/SU/SD**, **no DECSC/DECRC**, **no charset *switching*** (line-drawing box characters render as letters), and a **cell model missing ~7 attributes** (reverse / dim / blink / strikethrough / double+colored underline / conceal / overline / hyperlink).

The parser model itself is the deeper problem. It is a hand-rolled 7-state machine (`Ground, Escape, CsiEntry, CsiParam, CsiIgnore, StringSeq, StringSeqEsc, Charset`) that diverges from the canonical Williams VT500 FSM in ways that *silently corrupt* real input: `StringSeqEsc` returns to Ground on **any** byte after ESC rather than re-entering escape dispatch — so an SGR sequence immediately following an OSC (`ESC ] 0 ; title ESC [ 31 m`) eats the `[` and prints `31m` as literal text **[verified bug]**; CSI param state has no colon (0x3A) sub-parameter support (so `38:2::r:g:b` styled/colored underlines and modern color forms are dropped); there is no parameter-count cap; malformed terminators fall through to Ground without clearing `_params` (stale-param leakage); and combining marks / ZWJ graphemes are clamped to width 1 (line 198), splitting emoji. Because every byte is *not* assigned a defined action in every state, the machine is not provably crash-proof on pathological input.

> **[corrected]** The first draft also claimed "no 8-bit C1 controls, which ConPTY emits." **ConPTY emits 7-bit ESC-introduced sequences by default** (CSI = `0x1B 0x5B`, OSC = `0x1B 0x5D`); 8-bit C1 (0x9B/0x9D) is opt-in via `S8C1T` and modern conhost actively drops C1 (PR #11690). So the missing 8-bit-C1 path is a **robustness** gap (a rare/legacy app could mis-render), **not** a parity-critical one — it drops from P0 to P2. See [§8](#8-verification-corrections).

"Complete & accurate" worth committing to means three concrete bars:
1. a **parser** that is the Williams VT500 FSM — every byte routed in every state, correct OSC/DCS C0 handling, colon sub-params, bounded params (and 7-bit⇄8-bit C1 equivalence for robustness);
2. **semantics** covering Microsoft Learn's documented conhost floor *plus* the source-confirmed Windows Terminal items (alt screen, scroll region, DEC modes 25/7/2004/1000/1002/1006/1004/1049, OSC 8/52/4/10/11/12, IL/DL/SU/SD, DECSC/DECRC, DECSTR, RIS, line-drawing charset);
3. a **cell/grid model + test suite** that can actually represent and *prove* the above — alt buffer, scrollback, scroll margins, full attribute set, and a vendored golden corpus run in CI.

Tiers 3–4 (sixel, kitty graphics, VT520 rectangle ops, double-width lines, play-sound) are explicitly out of scope and **parsed-and-discarded** (recognize the envelope, never render), exactly as a recorder — not a daily-driver emulator — requires.

---

## 2. Conformance matrix

WT-supported? column: **Y** = documented on Microsoft Learn and/or confirmed in `DispatchTypes.hpp`/`adaptDispatch.cpp`; **Y\*** = source-confirmed but understated/absent on the public doc; **part** = WT partial/gated; **no** = WT no-op/unsupported.

### 2.1 Parser / state machine (Williams VT500)

| State / behavior | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|
| `ground` print/execute | glyphs print; C0 execute | Y | done | P0 |
| `escape` + on-entry `clear` | ESC cancels in-progress seq | Y | partial (no clear of pending) | P0 |
| `escape intermediate` (0x20–0x2F) | collect intermediates → `esc_dispatch` | Y | missing (charset only) | P0 |
| `csi entry/param/intermediate/ignore` | param + intermediate collection | Y | partial (no intermediate collect, no `:`) | P0 |
| Colon sub-parameters (0x3A) | `38:2::r:g:b`, `4:3` styled underline | Y\* | missing | P0 |
| Parameter count cap (16, drop extras) | overlong-seq safety | Y | missing | P0 |
| `osc string` (`osc_start/put/end`) | OSC body, C0 ignored, ST/BEL end | Y | partial (gobble only) | P0 |
| `dcs entry/param/intermediate/passthrough/ignore` | DCS body, **C0 ignored** | Y | missing (gobbled as StringSeq) | P1 |
| `sos/pm/apc string` | ignore to ST | Y | partial (shared StringSeq) | P1 |
| 8-bit C1 introducers (0x90/0x9B/0x9D/0x9E/0x9F/0x9C) | jump straight into state | Y\* | missing | **P2 [corrected]** — ConPTY emits 7-bit; robustness only |
| CAN(0x18)/SUB(0x1A) cancel → ground | abort sequence anywhere | Y | missing | P0 |
| 7-bit ST `ESC \` (re-enter escape, don't drop next byte) | string terminator | Y | **buggy** [verified] | P0 |
| Stale-param reset on malformed final | no leakage to next CSI | Y | **buggy** | P0 |
| GR 0xA0–0xFF treated as GL | | Y | done (rune-based) | P1 |

### 2.2 C0 control codes

| Byte | Mnemonic | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|---|
| 0x00 | NUL | ignored | Y | done (`<0x20` path, no-op) | P1 |
| 0x05 | ENQ | answerback | Y | missing | P2 |
| 0x07 | BEL | bell (no-op visually) | Y | partial (StringSeq only) | P2 |
| 0x08 | BS | backspace | Y | done | P0 |
| 0x09 | HT | tab to next stop | Y | partial (clamps, no tab stops) | P0 |
| 0x0A/0x0B/0x0C | LF/VT/FF | line feed | Y | done (all → LF) | P0 |
| 0x0D | CR | carriage return | Y | done | P0 |
| 0x0E/0x0F | SO/SI | LS1/LS0 (invoke G1/G0) | Y | missing | P1 |
| 0x1A | SUB | cancel; prints `␦` | Y | missing | P1 |
| 0x1B | ESC | escape | Y | done | P0 |

### 2.3 ESC (non-CSI) sequences

| Sequence | Mnemonic | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|---|
| `ESC D` / `ESC E` / `ESC M` | IND / NEL / RI | index / next-line / reverse-index (in margins) | Y | missing | P0 |
| `ESC 7` / `ESC 8` | DECSC / DECRC | save / restore cursor + attrs + charset | Y | missing | P0 |
| `ESC c` | RIS | hard reset | Y | partial (gobbled, no reset) | P0 |
| `ESC H` | HTS | set tab stop | Y | missing | P0 |
| `ESC =` / `ESC >` | DECKPAM / DECKPNM | keypad app / numeric | Y | partial (gobbled) | P2 |
| `ESC ( C` / `) * +` | SCS | designate G0–G3 94-set | Y | partial (consumes id, no switch) | P0 |
| `ESC # 8` | DECALN | alignment test (fill `E`) | Y\* | missing | P2 |
| `ESC # 3/4/5/6` | DECDHL/DECDWL/DECSWL | double-h/w lines | no | missing | P3 |
| `ESC N/O`, `ESC n/o`, `ESC ~ } \|` | SS2/SS3, LS2/LS3, LSnR | single/locking shifts | Y\* | missing | P2 |
| `ESC SP F` / `ESC SP G` | S7C1T / S8C1T | send 7/8-bit C1 | Y\* | missing | P2 |
| `ESC % @` / `ESC % G` | select ISO-2022 / UTF-8 | Y\* | missing | P2 |

### 2.4 Cursor positioning (CSI)

| Sequence | Mnemonic | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|---|
| `CSI n A/B/C/D` | CUU/CUD/CUF/CUB | up/down/right/left | Y | done | P0 |
| `CSI n E` / `CSI n F` | CNL / CPL | next/prev line, col 1 | Y | missing | P0 |
| `CSI n G` / `` CSI n ` `` | CHA / HPA | column absolute | Y | done (G) / missing (HPA) | P0 |
| `CSI n d` | VPA | row absolute | Y | done | P0 |
| `CSI n a` / `CSI n e` | HPR / VPR | column/row relative | Y\* | missing | P1 |
| `CSI y;x H` / `f` | CUP / HVP | row;col | Y | done | P0 |
| `CSI s` / `CSI u` | ANSISYSSC/RC | save/restore (collides with DECSLRM) | Y | missing | P1 |
| `CSI n I` / `CSI n Z` | CHT / CBT | forward/back tab stops | Y | missing | P1 |
| `CSI n SP q` | DECSCUSR | cursor shape (0–6) | Y | missing | P1 |

### 2.5 Erase / edit (CSI)

| Sequence | Mnemonic | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|---|
| `CSI n J` | ED | erase display 0/1/2 (3=scrollback) | Y | done (3 == 2 today) | P0 |
| `CSI n K` | EL | erase line 0/1/2 | Y | done | P0 |
| `CSI n X` | ECH | erase n chars | Y | done | P0 |
| `CSI n @` | ICH | insert n blanks | Y | done | P0 |
| `CSI n P` | DCH | delete n chars | Y | done | P0 |
| `CSI n L` | IL | insert n lines (in margins) | Y | missing | P0 |
| `CSI n M` | DL | delete n lines (in margins) | Y | missing | P0 |
| `CSI n b` | REP | repeat last graphic char | Y | missing | P1 |
| `CSI ? n J/K` | DECSED/DECSEL | selective erase (respects DECSCA) | Y\* | missing | P2 |
| `CSI ... rect ops` | DECCRA/FRA/ERA/SERA/… | rectangle area ops | Y\* | missing | P3 |

### 2.6 Scroll region & tabs

| Sequence | Mnemonic | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|---|
| `CSI t;b r` | DECSTBM | set top/bottom margins | Y | **missing** | P0 |
| `CSI n S` / `CSI n T` | SU / SD | scroll up/down in margins | Y | missing | P0 |
| `CSI t;b s` | DECSLRM | left/right margins (**needs DEC mode 69**) | Y\* | missing | P2 |
| `CSI 0 g` / `CSI 3 g` | TBC | clear tab here / all | Y | missing | P0 |
| `ESC H` | HTS | set tab stop | Y | missing | P0 |
| `CSI ? 5 W` | DECST8C | reset tabs every 8 cols | Y\* | missing | P2 |

### 2.7 Modes (SM/RM ANSI; DECSET/DECRST private)

| Sequence | Mnemonic | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|---|
| `CSI 4 h/l` | IRM | insert/replace | Y\* | missing | P1 |
| `CSI 20 h/l` | LNM | linefeed/newline | Y\* | missing | P2 |
| `CSI ? 1 h/l` | DECCKM | cursor-keys application | Y | partial (recognized, ignored) | P1 |
| `CSI ? 6 h/l` | DECOM | origin mode (rel. to margins) | Y\* | missing | P1 |
| `CSI ? 7 h/l` | DECAWM | autowrap | Y | partial (always on) | P0 |
| `CSI ? 12 h/l` | ATT610 | cursor blink | Y | partial (recognized, ignored) | P1 |
| `CSI ? 25 h/l` | DECTCEM | show/hide cursor | Y | **partial (ignored)** | P0 |
| `CSI ? 1000/1002/1003 h/l` | mouse press/drag/any-motion | Y | missing (track only) | P1 |
| `CSI ? 1004 h/l` | focus reporting | Y | missing (track only) | P1 |
| `CSI ? 1006 h/l` | SGR mouse encoding | Y | missing (track only) | P1 |
| `CSI ? 47/1047/1048/1049 h/l` | alt screen (1049 = save+alt) | Y | **missing** | P0 |
| `CSI ? 2004 h/l` | bracketed paste | Y | missing (track only) | P0 |
| `CSI ? 5 h/l` | DECSCNM | reverse screen | Y\* | missing | P2 |
| `CSI ? 2026 h/l` | synchronized output | Y\* part | missing | P1 |
| `CSI Ps $ p` / `CSI ? Ps $ p` | DECRQM/DECRPM | request mode state (reply `…$y`) | Y\* | missing | P2 |
| `CSI ! p` | DECSTR | soft reset | Y | missing | P0 |

> **[corrected]** DECRQM: WT's `AdaptDispatch::RequestMode` switch is **exhaustive over the `ModeParams` enum** in `DispatchTypes.hpp` — every named mode reports a real set/reset value (1/2/3/4). DECRPM value `0` means **"mode not recognized"** (VT510 spec), i.e. a mode *outside* the enum — not "recognized but unsupported." (One edge case: `LNM` can report 0 when input/output line-feed modes disagree.)

### 2.8 SGR (`CSI Pm m`)

| Code(s) | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|
| 0 | reset all | Y | done | P0 |
| 1 | bold / intense | Y | done | P0 |
| 2 | faint / dim | Y\* | **missing** | P0 |
| 3 | italic | Y\* | done | P0 |
| 4 / 4:1–4:5 / 21 | underline / styled / double | Y | partial (single only) | P0 (4), P1 (styled) |
| 5 / 6 | slow / rapid blink | Y\* | **missing** | P1 |
| 7 | reverse / negative | Y | **missing** | P0 |
| 8 | conceal / hidden | Y\* | **missing** | P1 |
| 9 | strikethrough | Y\* | **missing** | P0 |
| 22–29 | reset bold/dim/italic/ul/blink/reverse/conceal/strike | Y | partial (22/23/24 only) | P0 |
| 30–37 / 39 | fg + default | Y | done | P0 |
| 38;5;n / 38;2;r;g;b (+ colon) | extended fg | Y | partial (semicolon only) | P0 |
| 40–47 / 49 | bg + default | Y | done | P0 |
| 48;5;n / 48;2;r;g;b | extended bg | Y | partial (semicolon only) | P0 |
| 53 / 55 | overline / not overline | Y\* | **missing** | P1 |
| 58 / 59 | underline color set / default | Y\* | **missing** | P1 |
| 90–97 / 100–107 | bright fg / bg | Y | done | P0 |

> **[corrected]** Truecolor: conhost renders **full 24-bit RGB since 2016** — identical to Windows Terminal (shared buffer/renderer). The nearest-16 down-sample table (`TextColor::GetLegacyIndex`) is used *only* to express a cell to legacy Win32 console APIs, never for visual rendering. There is no truecolor difference between conhost and WT.

### 2.9 OSC

| Ps | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|
| 0 / 1 / 2 | icon+title / icon / title | Y | partial (gobbled) | P1 |
| 4 / 104 | set/query / reset palette color | Y | missing | P1 |
| 8 | **hyperlink** (`OSC 8;params;URI ST`) | Y | **missing** | P1 |
| 10 / 11 / 12 | default fg / bg / cursor color | Y | missing | P1 |
| 52 | clipboard get/set (base64) | Y | missing (record) | P1 |
| 110/111/112 | reset fg/bg/cursor color | Y | missing | P2 |
| 7 | report cwd | Y\* | missing | P2 |
| 9 / 9;4 | notification / progress (taskbar) | Y\* | missing | P2 |
| 133 | shell-integration marks (A/B/C/D) | Y\* | missing | P2 |
| 1337 / 777 | iTerm2 / urxvt actions | Y\* part | missing | P3 |

### 2.10 DCS / graphics

| Sequence | Mnemonic | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|---|
| `DCS $ q … ST` | DECRQSS | request setting; reply active value | Y\* | missing | P2 |
| `DCS + q … ST` | XTGETTCAP | query termcap | Y\* | missing | P3 |
| `DCS … q … ST` | DECSIXEL | sixel raster graphics | **Y in conhost (PR #17421, 2024-07) + WT Preview 1.22** [corrected] | missing (parse-and-discard) | P3 |
| `DCS … { … ST` | DECDLD | soft fonts / DRCS | part | missing | P3 |
| `ESC _ G …` / `OSC 1337;File=` | Kitty / iTerm2 inline images | no | missing | P3 |

> **[corrected]** Sixel was **first implemented in conhost** (`microsoft/terminal` PR #17421, merged 2024-07-01, GDI renderer) and then surfaced in Windows Terminal Preview 1.22 (2024-08-27). It is **not gated** behind an enable mode — DEC private mode 80 (`DECSDM`) controls sixel *scrolling*, not enablement. We still treat raster image protocols as **P3 / parse-and-discard** (rendering rasters into a text-SVG pipeline is a different project), but the rationale is "different output model," not "WT doesn't support it."

### 2.11 Charsets

| Sequence | Meaning | WT? | VtScreen | Priority |
|---|---|---|---|---|
| `ESC ( 0` then print | DEC Special Graphics / line drawing (`j k l m n q t u v w x` → box chars) | Y | **missing (designator consumed, no switch/map)** | P0 |
| `ESC ( B` | G0 = US ASCII | Y | partial (consumes id) | P0 |
| SO / SI (0x0E/0x0F) | invoke G1 / G0 | Y | missing | P1 |
| SS2 / SS3 / LS2 / LS3 | single/locking shift | Y\* | missing | P2 |

### 2.12 Mouse / input encodings (terminal → app)

> **Input-sequence note:** VcrSharp's native path is a **one-way display** of recorded program output — it does not feed keystrokes back to the program (that path is the existing Playwright/ttyd `Type`/`Key` commands toward a live shell). So the entire input-encoding family (mouse reports, cursor/function-key encoding, win32-input-mode generation) is **P2/P3 for the VT engine**: we must *not be confused by* these bytes, but we never *generate* them. The exception is **mode tracking** (1000/1002/1006/1004/2004) so DECRQM stays honest and mode-dependent rendering is correct.

---

## 3. Cell / grid model changes

Today (verified): `TerminalCell` = `Character, ForegroundColor?, BackgroundColor?, IsBold, IsItalic, IsUnderline, IsCursor, Width`. `TerminalContent` = `Cols, Rows, Cells[][], CursorX, CursorY, CursorVisible`. `SvgRenderer.StyleRun` coalesces runs only on `Fg/Bg/IsBold/IsItalic/IsUnderline` (~lines 618–637) and the CSS class builder emits only `bold/italic/underline`. `VtScreen.ToTerminalContent` hardcodes `CursorVisible = false`. **The model literally cannot represent most of the gaps above.**

### 3.1 `TerminalCell` — new attributes

| New field | Drives | Notes |
|---|---|---|
| `IsReverse` | SGR 7/27 | render-time fg/bg swap (don't mutate stored colors) |
| `IsDim` | SGR 2/22 | render: reduce fg luminance/opacity |
| `Blink` (None/Slow/Rapid) | SGR 5/6/25 | static SVG renders steady (see non-goals) |
| `IsStrikethrough` | SGR 9/29 | SVG `<line>` overstrike |
| `IsConceal` | SGR 8/28 | render glyph as space, preserve cell |
| `IsOverline` | SGR 53/55 | SVG top `<line>` |
| `UnderlineStyle` (None/Single/Double/Curly/Dotted/Dashed) | SGR 4 / 4:1–4:5 / 21 | replaces bool `IsUnderline` (keep bool as derived) |
| `UnderlineColor?` | SGR 58/59 | null = follow fg |
| `IsProtected` | DECSCA | only if DECSED/DECSEL land (P2) |
| `HyperlinkId?` + content-side URI table | OSC 8 | per-cell id → `TerminalContent.Hyperlinks[id]`; SVG `<a xlink:href>` |
| charset-resolved glyph | line-drawing | resolve `ESC ( 0` mapping at *print* time, store final glyph |

Collapse the per-cell booleans into a `[Flags] CellAttributes` bitfield — cheaper to clone (today `Cell.Clone()` copies 7 fields by hand) and to compare for run-coalescing. Combining marks / ZWJ: stop clamping width-0 runes to 1 (line 198); append the combining rune to the *base* cell's `Character` (grapheme cluster), leaving width as the base width.

### 3.2 `TerminalContent` — new fields

| New field | Why |
|---|---|
| `Hyperlinks` (Dictionary<int,string>) | OSC 8 ids → URIs for SVG `<a>` |
| `CursorShape` (Block/Underline/Bar) + `CursorBlink` | DECSCUSR + ATT610; feeds animated cursor layer (SvgRenderer ~314–326) |
| `IsAltScreen` | which buffer is active (debug / golden diffs) |
| `Palette` overrides (index→hex) | OSC 4/104 + 10/11/12 so SVG resolves the *current* palette |
| `ReverseScreen` | DECSCNM (mode 5) global invert |

### 3.3 Engine-internal grid model (not snapshotted, but required)

- **Two buffers** `_main`/`_alt`, switched by DECSET 47/1047/1049 (1049 also saves/restores cursor + clears alt).
- **Scrollback ring** (main only): rows scrolled off the top pushed to a bounded ring (default ~1000); enables correct `ED 3` and future full-session capture.
- **Scroll region** (`_marginTop`/`_marginBottom`): LF/IND/RI/SU/SD/IL/DL operate within margins (today `ScrollUp` is hardcoded to `[0, _rows)`).
- **Tab-stop set** (`bool[_cols]`): HTS/TBC/CHT/CBT/DECST8C; default every 8.
- **Saved-cursor slot(s)**: DECSC/DECRC + ANSISYSSC/RC store `{row,col,sgr,charset,origin-mode}`.
- **Mode dictionary**: `Dictionary<int,bool>` for DEC private modes (+ ANSI modes) — single source for cursor visibility, autowrap, origin, bracketed paste, mouse; lets DECRQM answer and rendering branch.
- **Charset state**: `G0..G3` + active GL/GR + single-shift pending; resolve at print.

### 3.4 Flow into SvgRenderer (additive, low-risk — machinery already exists)

1. Add new attrs to `StyleRun` + coalescing comparison (~618–637) + run-key hash (~1009–1013).
2. Extend `BuildCssClasses` (~1036–1038) with `dim/reverse/strike/overline/conceal` + `text-decoration-style` (styled underline) + `text-decoration-color` (SGR 58).
3. Reverse video / dim / `ReverseScreen` are **render-time** transforms — don't mutate stored colors.
4. OSC 8: wrap the run's `<text>`/`<tspan>` in `<a xlink:href>` via `TerminalContent.Hyperlinks`.
5. Cursor: replace hardcoded `CursorVisible=false` with real DECTCEM state; honor `CursorShape`.
6. Palette: resolve color indices through `TerminalContent.Palette` before the existing index→hex lookup (~547–559).

---

## 4. Test-suite strategy

Goal: a **consumable, vendored conformance corpus** running in CI (xUnit v3 + Shouldly, the stack already in `tests/VcrSharp.Core.Tests`), measuring the engine against [§2](#2-conformance-matrix). Four complementary layers.

### 4.1 What to vendor / port (license verdicts — verified)

| Suite | License | Action | Covers |
|---|---|---|---|
| **libvterm `t/*.test`** (neovim fork) | **MIT** ✅ verified | **Vendor verbatim** — *do first* | structured golden grid/ops (40+ files) |
| **ghostty `src/terminal/*.zig` tests** | **MIT** ✅ verified | **Transcribe** 50–100 cases | broadest modern semantics (~375 tests in Terminal.zig) |
| **Paul Williams VT500 FSM** | spec CC BY-NC-SA (reimplement); Haberman C impl public-domain | encode table as data | parser oracle |
| **xterm.js `InputHandler.test.ts`** | MIT | transcribe (fallback to libvterm) | golden-grid, very readable |
| **alacritty `vte` `tests/demo.vte`** | Apache-2.0 OR MIT | vendor as **fuzz/no-crash input** | robustness/throughput |
| **esctest / esctest2** | **GPL-2.0** ⚠️ verified | **Do NOT copy.** Run **out-of-process**; mine the per-`.py` file list as a **checklist** | query/report roundtrips (DECRQSS/DECRQM/DA/DSR), rect ops |
| **microsoft/terminal `ut_adapter`** | MIT but **TAEF-locked** | mine `adapterTest.cpp` as checklist; port select cases | Windows/conpty edge cases |
| **vttest** | BSD | interactive — checklist only | manual sanity |

### 4.2 Architecture (4 gates in xUnit)

1. **Parser conformance (Williams oracle).** Encode the VT500 state/transition table as a data file; a `[Theory]` walks every byte through the parser and asserts state + action. Feed `demo.vte` and assert **no exception / bounded callbacks** (fuzz gate). *Gates Phase 1.*
2. **Golden-grid replay (libvterm).** Vendor `t/*.test`; a ~150-LOC harness replays each file and diffs the final **grid + cursor** (not libvterm's exact op names) against the expected lines. One `[Theory]` over the file set → hundreds of cases. *Gates Phases 2–3.*
3. **Ported unit corpus (ghostty/xterm.js).** Hand-translated `[Fact]`/`[Theory]` for features libvterm under-covers (alt screen, DECSTBM edges, styled underline, truecolor, DEC modes, line drawing). *Gates Phases 2–4.*
4. **Golden-diff vs the browser path (equivalence gate).** For a curated set of inputs, render once via Playwright/ttyd and once via `ConPtyProcess→VtScreen`, diff the `TerminalContent`/normalized SVG. This decides when the native backend is trustworthy enough to be default *for a given tape*. *Gates Phases 4–6.*

Optional 5th gate: **esctest out-of-process** in a separate CI job for DECRQSS/DECRQM/DA/DSR roundtrips, kept at arm's length from the MIT tree.

### 4.3 Highest-leverage picks

1. **libvterm `t/*.test`** — the only ready-made, permissive, *structured golden* corpus. Highest ROI; do first.
2. **ghostty Zig tests** — best breadth for modern features others miss.
3. **Williams FSM oracle + esctest (GPL arm's-length)** — rigorous parser correctness + the query/report roundtrips nobody else covers permissively.

---

## 5. Phased build plan

Effort: **S** ≈ days, **M** ≈ 1–2 weeks, **L** ≈ 3+ weeks. Each phase ships independently behind the existing native path.

| Phase | Scope | Gating tests | Effort |
|---|---|---|---|
| **P1 — Parser rewrite to Williams VT500** | Replace the 7-state hand-roll with the full table-driven DFA (ground/escape/esc-int/csi-{entry,param,int,ignore}/osc/dcs-{entry,param,int,passthrough,ignore}/sos-pm-apc). CAN/SUB cancel; correct 7-bit ST (re-enter escape); colon sub-params; 16-param cap; `hook/put/unhook` DCS seams; stale-param fix; 8-bit C1 path (robustness). **Pure refactor — same semantics out, correct routing in.** | Gate 1 (Williams oracle) + `demo.vte` fuzz; existing `VtScreenTests` stay green | **L** |
| **P2 — Core grid semantics** | Scroll region (DECSTBM) + margin-aware LF/IND/RI/SU/SD/IL/DL; CNL/CPL/HPA/HPR/VPR; tab stops (HTS/TBC/CHT/CBT); DECSC/DECRC + ANSISYSSC/RC; DECSTR + RIS; `ED 3` = real scrollback erase. | Gate 2 (libvterm scroll/edit/tabs) + Gate 3 | **L** |
| **P3 — Cell model + full SGR + charsets** | `CellAttributes` bitfield (reverse/dim/blink/strike/conceal/overline/styled+colored underline); SGR 2/5/6/7/8/9/21/25/27/28/29/53/55/58/59 + colon forms; combining-mark merge; line-drawing charset (`ESC ( 0`, SO/SI). Widen `TerminalCell`/`TerminalContent`/`StyleRun`/CSS. | Gate 2 (SGR) + Gate 3 (styled underline, line drawing) | **M** |
| **P4 — DEC modes + alt screen + cursor** | Mode dictionary; DECTCEM (real `CursorVisible`), DECAWM, DECOM, DECSCNM, DECSCUSR/ATT610; alt screen 47/1047/1048/1049 (two buffers + save/restore); DECRQM/DECRPM replies. | Gate 3 (alt screen, modes) + Gate 5 (esctest DECRQM) | **M** |
| **P5 — OSC + palette + hyperlink + mouse-mode tracking** | OSC 0/1/2 (title), 4/104 + 10/11/12/110-112 (palette → `TerminalContent.Palette`), OSC 8 hyperlinks (per-cell id + URI table → SVG `<a>`), OSC 52 (clipboard, recorded), 133 (optional). Track mouse modes 1000/1002/1006/1004/2004 for clean parse + honest DECRQM. | Gate 3 (OSC) + Gate 4 (browser equivalence) | **M** |
| **P6 — Animation + backend seam** | `ITerminalBackend` abstraction; poll-at-framerate loop snapshotting `VtScreen` into `TerminalStateWithTime[]` (type already exists) for animated SVG/GIF; auto-fallback to browser when a tape uses unclaimed features. | Gate 4 (browser golden-diff) across the sample tape corpus | **L** |
| **P7 — Unix PTY sibling (optional)** | `forkpty`-based `PtyProcess` implementing `ITerminalBackend` so the browserless path runs on Linux/macOS. Parser/grid are already platform-neutral (`VcrSharp.Core`); only PTY spawn is new. | All gates run cross-platform in CI | **M** |

Synchronized output (2026) and grapheme mode (2027) recognition fold into P3/P4 as mode tracking + a render hint.

---

## 6. Architecture seam

**`ITerminalBackend` (new, in `VcrSharp.Core`).** A backend produces a stream of `TerminalContent` snapshots (single for static, time-indexed for animation). Two implementations:
- `NativeTerminalBackend` — wraps `ConPtyProcess` (Windows) / future `PtyProcess` (Unix) feeding `VtScreen`. Parser + grid live in Core and are platform-neutral; only PTY spawn is platform-specific (today `ConPtyProcess` is the entire Windows dependency and `NativeTerminalRenderer` does **not** abstract it — this phase introduces the seam).
- `BrowserTerminalBackend` — wraps the existing Playwright/ttyd/`TerminalPage` path (current default, unchanged).

**Auto-fallback.** Before recording, scan the parsed tape (and optionally a dry run of the VT stream's sequence families) for features the native engine doesn't claim in [§2](#2-conformance-matrix) (sixel, kitty graphics, input-echo TUIs, anything P3). If found → `BrowserTerminalBackend`; else prefer native. **Gate 4 (browser-equivalence golden diff) is what *earns* a feature the right to be served natively.**

**Animation polling** lives in `NativeTerminalBackend`, not in `VtScreen` (the parser stays a pure byte→grid function). A `Timer`/`Monitor` loop snapshots `ToTerminalContent()` at the framerate while the ConPTY drain thread feeds bytes; snapshots become `TerminalStateWithTime` entries. The browser path keeps its existing external (Playwright screenshot) capture — the seam lets each backend own its capture strategy.

**Windows/Unix PTY split.** `ConPtyProcess` (already implemented) and a sibling `forkpty` backend share the `ITerminalBackend` contract and the same `VtScreen`. No parser fork — divergence is ~200 lines of PTY plumbing per platform.

---

## 7. Risks & non-goals

**Explicit non-goals (parse-and-discard, matching a recorder's needs):**
- **Sixel / ReGIS / Kitty graphics / iTerm2 inline images** — raster image protocols. Even though sixel now exists in conhost+WT, rendering rasters into a text-SVG pipeline is a different project. Recognize the DCS/APC envelope so it doesn't corrupt the stream; never render.
- **VT520/VT420 rectangle area ops** (DECCRA/FRA/ERA/SERA/RARA/RQCRA), **DECIC/DECDC columns** — large surface, rarely emitted; discard cleanly.
- **Double-width/height lines** (DECDHL/DECDWL/DECSWL) — WT dispatches but renders normal width; we match (no-op).
- **Blink animation, bell sound, DECPS play-sound, DECDLD soft fonts** — render blink as steady; accept and ignore the rest.
- **Interactive input encoding** (mouse reports, key encoding, win32-input-mode generation) — native engine is a one-way display; the interactive path stays with the existing ttyd/Playwright `Type`/`Key`. We *track* modes, we don't *emit* encodings.
- **VT52 sub-mode** (DECANM reset) — unlikely in ConPTY output.

**Risks:**
- **ConPTY's own rewriting.** ConPTY re-renders the child's output (sometimes redrawing whole regions). The native engine must be robust to *ConPTY's* sequence style; the browser-equivalence gate (Gate 4) is the safety net. (Note: ConPTY output is **7-bit** by default — 8-bit C1 robustness is a nicety, not a requirement.)
- **Parser rewrite regression.** P1 is a from-scratch FSM; land it behind the same `Feed` API and switch only when both the existing tests and the libvterm corpus are green.
- **Golden-corpus drift.** Diff *grid + cursor*, not libvterm's exact callback names, to avoid coupling to its internals. Vendored counts (e.g. ghostty test totals) are a moving target.
- **Scope creep into "real terminal."** The deliverable is "match Windows Terminal's *documented + source-confirmed* behavior for *playback rendering*," not a daily-driver emulator. [§2](#2-conformance-matrix) is the contract; P3 is deferred.
- **License hygiene.** esctest is **GPL-2.0** — out-of-process and out of the source tree; only its file-name checklist informs us. vtdn.dev data is AI-generated — use as a gap-finder, confirm specifics against xterm `ctlseqs` / ECMA-48.

---

## 8. Verification corrections

The following first-draft claims were **adversarially fact-checked and corrected** (primary sources in parentheses). They are already folded into the matrix and plan above; recorded here for traceability.

1. **8-bit C1 / ConPTY — REFUTED.** ConPTY emits **7-bit** ESC sequences by default; 8-bit C1 (0x9B/0x9D) is opt-in via `S8C1T`, and modern conhost drops C1 entirely (PR #11690). → 8-bit C1 handling is **P2 robustness**, not P0. (Microsoft Learn console-virtual-terminal-sequences; xterm ctlseqs; microsoft/terminal#11690.)
2. **conhost truecolor down-sampling — REFUTED.** conhost renders full 24-bit RGB since Win10 build 14931 (2016), same as WT; the nearest-16 table (`TextColor::GetLegacyIndex`) is only for the legacy Win32 attribute API. → No truecolor difference between conhost and WT. (devblogs "24-bit Color in the Windows Console"; microsoft/terminal `TextColor.h`.)
3. **Sixel "not in conhost" — REFUTED.** Sixel was **first implemented in conhost** (PR #17421, merged 2024-07-01) then surfaced in WT Preview 1.22; it is **not** gated by DECSDM (mode 80 controls sixel *scrolling*). → Still P3/parse-and-discard, but on output-model grounds, not "WT lacks it." (microsoft/terminal#17421; vt100.net VT3xx ch.14.)
4. **DECRQM "many modes report 0" — REFUTED.** WT's `RequestMode` is **exhaustive over the `ModeParams` enum** — every named mode reports a real 1/2/3/4 value; DECRPM `0` means "mode *not recognized*" (outside the enum). → Caveat inverted in the matrix. (microsoft/terminal `adaptDispatch.cpp`/`DispatchTypes.hpp`; vt100.net DECRPM.)

**Confirmed (high-confidence) claims:** the `StringSeqEsc` 7-bit-ST bug is real (VtScreen.cs ~113–115); libvterm is MIT and `t/*.test` is vendorable; esctest/esctest2 are GPL-2.0 and must stay out-of-process; DECSLRM is gated on DEC mode 69 (collides with `CSI s` save-cursor); Williams FSM ignores C0 in DCS/OSC strings; the current VtScreen capability partition in [§1](#1-reality-check) is accurate; ghostty is MIT and vendorable with attribution.
