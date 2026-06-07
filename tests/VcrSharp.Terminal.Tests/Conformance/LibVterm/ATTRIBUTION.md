# Vendored conformance corpus — libvterm

These `*.test` files are the **conformance corpus from libvterm**, vendored verbatim from the
neovim fork (`neovim/libvterm`, branch `nvim`, directory `t/`).

- **Upstream:** https://github.com/neovim/libvterm  (original by Paul "LeoNerd" Evans)
- **License:** MIT — see `LICENSE` in this directory (preserved verbatim, as MIT requires).
- **What they are:** a DSL of `PUSH "<bytes>"` feeds and `?screen_row` / `?cursor` /
  `?screen_chars` / `?screen_text` / `?screen_cell` assertions, originally driven by
  upstream's `t/harness.c` + `t/run-test.pl`.

## How VcrSharp consumes them

We do **not** vendor or build the upstream C harness. Instead `LibVtermHarness.cs` reimplements
the relevant subset of `run-test.pl` in C#: it decodes the Perl-quoted `PUSH` byte strings, feeds
them through our `VtScreen`, and evaluates the **grid-coupled** assertions
(`?cursor`, `?screen_row`, `?screen_chars`, `?screen_text`, and `movecursor`) against the snapshot.

Assertions that depend on libvterm's internal callback/event model rather than the final grid
(`putglyph`, `scrollrect`, `damage`, `settermprop`, `?pen`, `?lineinfo`, `?screen_cell` colors,
`?screen_eol`, `?screen_attrs_extent`, …) are **counted as skipped**, not failed — see the
scoreboard. As the engine's cell model grows (P3+), more of these become evaluable.

This keeps us clean of the GPL-licensed esctest suite and of any C build dependency, while reusing
hundreds of permissively-licensed golden cases. See `docs/vt-conformance-scoreboard.md`.
