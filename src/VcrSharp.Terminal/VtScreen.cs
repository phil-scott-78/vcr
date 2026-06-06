using System.Globalization;
using System.Text;
using VcrSharp.Core.Rendering;

namespace VcrSharp.Terminal;

/// <summary>
/// An in-process VT/ANSI terminal screen. Feed it the byte/char stream a program writes (or that
/// ConPTY re-emits) and it maintains a fixed Cols×Rows cell grid with SGR attributes and a cursor.
/// Snapshot it to a <see cref="TerminalContent"/> — the same cell grid the SVG pipeline already
/// consumes — so the existing <c>SvgRenderer</c> can render it with no browser involved.
///
/// The parser is the canonical Paul Williams VT500 state machine
/// (https://vt100.net/emu/dec_ansi_parser): every byte is routed in every state, ESC always cancels
/// and re-enters escape handling (no dropped byte after a string's terminating ESC), CAN/SUB abort,
/// CSI parameters support colon sub-parameters (e.g. <c>38:2::r:g:b</c>) and are bounded, and
/// OSC/DCS/SOS/PM/APC strings are consumed without leaking. 8-bit C1 introducers are recognised for
/// robustness (ConPTY itself emits 7-bit).
///
/// Semantics implemented today: printable text (autowrap + wide-char width), C0 controls
/// (CR/LF/BS/HT), SGR (16 / 256 / truecolor fg+bg, bold/italic/underline, semicolon AND colon forms),
/// cursor positioning (CUP/CUU/CUD/CUF/CUB/CHA/VPA), erase (ED/EL/ECH), and insert/delete chars
/// (ICH/DCH). Scroll region, alt screen, DEC modes, IL/DL/SU/SD, charset switching, and the full
/// attribute set are deliberately later phases — see docs/vt-engine-conformance.md. Such sequences are
/// parsed and discarded (routed correctly), never mis-printed.
/// </summary>
public sealed class VtScreen
{
    private const int MaxParams = 32; // overlong-parameter-list safety cap

    private enum State
    {
        Ground,
        Escape,
        EscapeIntermediate,
        CsiEntry,
        CsiParam,
        CsiIntermediate,
        CsiIgnore,
        OscString,      // OSC: ends on BEL / ST / ESC
        StringConsume,  // DCS / SOS / PM / APC: ends on ST / ESC
    }

    private sealed class Cell
    {
        public string Char = " ";
        public string? Fg;
        public string? Bg;
        public bool Bold;
        public bool Italic;
        public bool Underline;
        public int Width = 1;

        public Cell Clone() => new()
        {
            Char = Char, Fg = Fg, Bg = Bg, Bold = Bold, Italic = Italic, Underline = Underline, Width = Width,
        };
    }

    private readonly int _cols;
    private readonly int _rows;
    private readonly Cell[][] _grid;

    private int _row;
    private int _col;
    private bool _wrapPending; // deferred autowrap: set after writing the last column

    // Current SGR state
    private string? _fg;
    private string? _bg;
    private bool _bold;
    private bool _italic;
    private bool _underline;

    // Parser state
    private State _state = State.Ground;
    private readonly List<int> _params = new();   // -1 = empty/omitted parameter
    private readonly List<bool> _sub = new();     // _sub[i] = parameter i is colon-joined to i-1
    private int _cur;
    private bool _curHasDigits;
    private bool _pendingSub;                      // the separator before the next parameter was ':'
    private bool _privateMarker;                   // CSI '?' leader
    private char _interChar;                       // last CSI/ESC intermediate (e.g. '!' in DECSTR)

    // Scroll region (inclusive, 0-based), tab stops, saved cursor, and a bounded scrollback ring.
    private int _top;
    private int _bottom;
    private bool[] _tabs;
    private SavedCursor? _saved;
    private readonly List<Cell[]> _scrollback = new();
    private const int ScrollbackMax = 1000;

    private readonly record struct SavedCursor(
        int Row, int Col, string? Fg, string? Bg, bool Bold, bool Italic, bool Underline, bool WrapPending);

    public VtScreen(int cols, int rows)
    {
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
        _grid = new Cell[_rows][];
        for (var r = 0; r < _rows; r++)
        {
            _grid[r] = new Cell[_cols];
            for (var c = 0; c < _cols; c++)
                _grid[r][c] = new Cell();
        }
        _bottom = _rows - 1;
        _tabs = BuildDefaultTabs();
    }

    private bool[] BuildDefaultTabs()
    {
        var t = new bool[_cols];
        for (var c = 8; c < _cols; c += 8) t[c] = true; // a tab stop every 8 columns
        return t;
    }

    public int Cols => _cols;
    public int Rows => _rows;

    /// <summary>Feeds decoded text (the PTY reader handles byte→char UTF-8 decoding across reads).</summary>
    public void Feed(string text)
    {
        foreach (var rune in text.EnumerateRunes())
            FeedRune(rune);
    }

    // ---- Williams VT500 parser ----

    private void FeedRune(Rune rune)
    {
        var cp = rune.Value;

        // "Anywhere" transitions: ESC always cancels the current sequence/string and re-enters escape
        // handling (so the byte after a string's terminating ESC is NOT dropped); CAN/SUB abort.
        if (cp == 0x1B) { _state = State.Escape; return; }
        if (cp == 0x18 || cp == 0x1A) { _state = State.Ground; return; }

        // String-collecting states handle their own byte set (and are exited by the ESC above).
        switch (_state)
        {
            case State.OscString:
                if (cp == 0x07 || cp == 0x9C) _state = State.Ground; // BEL or 8-bit ST ends OSC
                return;                                              // otherwise consume (discarded)
            case State.StringConsume:
                if (cp == 0x9C) _state = State.Ground;               // 8-bit ST ends DCS/SOS/PM/APC
                return;
        }

        // 8-bit C1 controls (robustness; ConPTY emits 7-bit). Introducers jump straight into a state.
        if (cp is >= 0x80 and <= 0x9F)
        {
            switch (cp)
            {
                case 0x9B: ClearParams(); _state = State.CsiEntry; return; // CSI
                case 0x9D: _state = State.OscString; return;              // OSC
                case 0x90: _state = State.StringConsume; return;          // DCS
                case 0x98: case 0x9E: case 0x9F: _state = State.StringConsume; return; // SOS/PM/APC
                case 0x9C: _state = State.Ground; return;                 // ST with no string
                default: return;                                          // other C1: ignore, keep state
            }
        }

        // C0 controls execute in every non-string state and do not change the parser state.
        if (cp < 0x20) { ExecuteC0(cp); return; }
        if (cp == 0x7F) return; // DEL ignored

        switch (_state)
        {
            case State.Ground:
                Print(rune);
                return;

            case State.Escape:
                if (cp is >= 0x20 and <= 0x2F) { _state = State.EscapeIntermediate; return; } // intermediates
                switch (cp)
                {
                    case '[': ClearParams(); _state = State.CsiEntry; return;   // CSI
                    case ']': _state = State.OscString; return;                 // OSC
                    case 'P': _state = State.StringConsume; return;             // DCS
                    case 'X': case '^': case '_': _state = State.StringConsume; return; // SOS/PM/APC
                }
                if (cp is >= 0x30 and <= 0x7E) { EscDispatch((char)cp); _state = State.Ground; return; }
                return;

            case State.EscapeIntermediate:
                if (cp is >= 0x20 and <= 0x2F) return;                  // collect more intermediates
                if (cp is >= 0x30 and <= 0x7E) { _state = State.Ground; return; } // esc_dispatch (no-op)
                return;

            case State.CsiEntry:
            case State.CsiParam:
                if (cp is >= 0x30 and <= 0x39) { _cur = Math.Min(_cur * 10 + (cp - 0x30), 0x0FFFFFFF); _curHasDigits = true; _state = State.CsiParam; return; }
                if (cp == ';') { CommitParam(); _pendingSub = false; _state = State.CsiParam; return; }
                if (cp == ':') { CommitParam(); _pendingSub = true; _state = State.CsiParam; return; }
                if (cp is >= 0x3C and <= 0x3F) // private/leader bytes < = > ?
                {
                    if (_state == State.CsiEntry) { _privateMarker = cp == '?'; _state = State.CsiParam; return; }
                    _state = State.CsiIgnore; return;
                }
                if (cp is >= 0x20 and <= 0x2F) { _interChar = (char)cp; _state = State.CsiIntermediate; return; }
                if (cp is >= 0x40 and <= 0x7E) { CommitParam(); DispatchCsi((char)cp); _state = State.Ground; return; }
                return;

            case State.CsiIntermediate:
                if (cp is >= 0x20 and <= 0x2F) { _interChar = (char)cp; return; }
                if (cp is >= 0x40 and <= 0x7E) { CommitParam(); DispatchCsi((char)cp); _state = State.Ground; return; }
                if (cp is >= 0x30 and <= 0x3F) { _state = State.CsiIgnore; return; }
                return;

            case State.CsiIgnore:
                if (cp is >= 0x40 and <= 0x7E) _state = State.Ground;
                return;
        }
    }

    private void ClearParams()
    {
        _params.Clear();
        _sub.Clear();
        _cur = 0;
        _curHasDigits = false;
        _pendingSub = false;
        _privateMarker = false;
        _interChar = '\0';
    }

    private void CommitParam()
    {
        if (_params.Count < MaxParams)
        {
            _params.Add(_curHasDigits ? _cur : -1);
            _sub.Add(_pendingSub);
        }
        _cur = 0;
        _curHasDigits = false;
    }

    private void ExecuteC0(int cp)
    {
        switch (cp)
        {
            case 0x0D: // CR
                _col = 0;
                _wrapPending = false;
                break;
            case 0x0A: // LF
            case 0x0B: // VT
            case 0x0C: // FF
                LineFeed();
                break;
            case 0x08: // BS
                if (_col > 0) _col--;
                _wrapPending = false;
                break;
            case 0x09: // HT
                _col = NextTabStop(_col);
                _wrapPending = false;
                break;
        }
    }

    private void LineFeed()
    {
        _wrapPending = false;
        if (_row == _bottom)
            ScrollRegionUp(1);
        else if (_row < _rows - 1)
            _row++;
    }

    /// <summary>Scrolls the scroll region [<see cref="_top"/>, <see cref="_bottom"/>] up by
    /// <paramref name="n"/> lines. Lines leaving the top of a region anchored at the screen top go to
    /// the scrollback ring; the bottom is filled with blanks.</summary>
    private void ScrollRegionUp(int n)
    {
        n = Math.Min(n, _bottom - _top + 1);
        for (var i = 0; i < n; i++)
        {
            var removed = _grid[_top];
            for (var r = _top; r < _bottom; r++) _grid[r] = _grid[r + 1];
            _grid[_bottom] = NewBlankRow();
            if (_top == 0) PushScrollback(removed); else { /* discarded */ }
        }
    }

    /// <summary>Scrolls the scroll region down by <paramref name="n"/> lines (blanks at the top).</summary>
    private void ScrollRegionDown(int n)
    {
        n = Math.Min(n, _bottom - _top + 1);
        for (var i = 0; i < n; i++)
        {
            for (var r = _bottom; r > _top; r--) _grid[r] = _grid[r - 1];
            _grid[_top] = NewBlankRow();
        }
    }

    private void PushScrollback(Cell[] row)
    {
        _scrollback.Add(row);
        if (_scrollback.Count > ScrollbackMax) _scrollback.RemoveAt(0);
    }

    private Cell[] NewBlankRow()
    {
        var row = new Cell[_cols];
        for (var c = 0; c < _cols; c++) row[c] = BlankCell();
        return row;
    }

    private Cell BlankCell() => new() { Bg = _bg }; // erased cells keep the current background

    private void Print(Rune rune)
    {
        if (_wrapPending)
        {
            _col = 0;
            LineFeed();
            _wrapPending = false;
        }

        var width = RuneWidth(rune);
        if (width == 0) width = 1; // treat zero-width/combining as 1 for now (P3: grapheme merge)

        if (_col + width > _cols)
        {
            // not enough room for a wide char at the right margin -> wrap first
            _col = 0;
            LineFeed();
        }

        var cell = _grid[_row][_col];
        cell.Char = rune.ToString();
        cell.Fg = _fg;
        cell.Bg = _bg;
        cell.Bold = _bold;
        cell.Italic = _italic;
        cell.Underline = _underline;
        cell.Width = width;

        if (width == 2 && _col + 1 < _cols)
        {
            var cont = _grid[_row][_col + 1];
            cont.Char = "";
            cont.Fg = _fg;
            cont.Bg = _bg;
            cont.Bold = false;
            cont.Italic = false;
            cont.Underline = false;
            cont.Width = 0; // continuation
        }

        _col += width;
        if (_col >= _cols)
        {
            _col = _cols - 1;
            _wrapPending = true;
        }
    }

    private void DispatchCsi(char final)
    {
        switch (final)
        {
            case 'm': ApplySgr(); break;
            case 'H':
            case 'f':
                _row = Math.Clamp(Param(0, 1) - 1, 0, _rows - 1);
                _col = Math.Clamp(Param(1, 1) - 1, 0, _cols - 1);
                _wrapPending = false;
                break;
            case 'A': _row = Math.Max(CursorUpLimit(), _row - Param(0, 1)); _wrapPending = false; break;   // CUU (stops at top margin)
            case 'B': _row = Math.Min(CursorDownLimit(), _row + Param(0, 1)); _wrapPending = false; break; // CUD (stops at bottom margin)
            case 'C': _col = Math.Min(_cols - 1, _col + Param(0, 1)); _wrapPending = false; break;
            case 'D': _col = Math.Max(0, _col - Param(0, 1)); _wrapPending = false; break;
            case 'E': _row = Math.Min(CursorDownLimit(), _row + Param(0, 1)); _col = 0; _wrapPending = false; break; // CNL
            case 'F': _row = Math.Max(CursorUpLimit(), _row - Param(0, 1)); _col = 0; _wrapPending = false; break;   // CPL
            case 'G':
            case '`': _col = Math.Clamp(Param(0, 1) - 1, 0, _cols - 1); _wrapPending = false; break;  // CHA / HPA
            case 'a': _col = Math.Min(_cols - 1, _col + Param(0, 1)); _wrapPending = false; break;     // HPR
            case 'd': _row = Math.Clamp(Param(0, 1) - 1, 0, _rows - 1); _wrapPending = false; break;   // VPA
            case 'e': _row = Math.Min(_rows - 1, _row + Param(0, 1)); _wrapPending = false; break;     // VPR
            case 'J': EraseInDisplay(Param(0, 0)); break;
            case 'K': EraseInLine(Param(0, 0)); break;
            case 'X': EraseChars(Param(0, 1)); break;     // ECH
            case '@': InsertChars(Param(0, 1)); break;    // ICH
            case 'P': DeleteChars(Param(0, 1)); break;    // DCH
            case 'L': InsertLines(Param(0, 1)); break;    // IL
            case 'M': DeleteLines(Param(0, 1)); break;    // DL
            case 'S': ScrollRegionUp(Param(0, 1)); break;    // SU
            case 'T': ScrollRegionDown(Param(0, 1)); break;  // SD
            case 'r': if (!_privateMarker) SetScrollRegion(); break; // DECSTBM
            case 'g': ClearTabs(Param(0, 0)); break;      // TBC
            case 'I': TabForward(Param(0, 1)); break;     // CHT
            case 'Z': TabBackward(Param(0, 1)); break;    // CBT
            case 's': if (!_privateMarker) SaveCursor(); break;  // SCOSC (DECSLRM mode 69 not enabled)
            case 'u': RestoreCursor(); break;             // SCORC
            case 'p': if (_interChar == '!') SoftReset(); break; // DECSTR
            // h/l (DEC modes), alt screen, mouse, etc. are P4.
        }
    }

    private int CursorUpLimit() => _row >= _top ? _top : 0;
    private int CursorDownLimit() => _row <= _bottom ? _bottom : _rows - 1;

    /// <summary>Reads parameter <paramref name="index"/>. Empty (-1) and (for 1-defaulting commands)
    /// explicit 0 fall back to <paramref name="fallback"/>.</summary>
    private int Param(int index, int fallback)
    {
        if (index >= _params.Count) return fallback;
        var v = _params[index];
        if (v < 0) return fallback;                       // omitted
        if (v == 0 && fallback != 0) return fallback;     // 0 means 1 for cursor/edit counts
        return v;
    }

    private void EraseInDisplay(int mode)
    {
        switch (mode)
        {
            case 0: // cursor to end of screen
                EraseLineRange(_row, _col, _cols - 1);
                for (var r = _row + 1; r < _rows; r++) EraseLineRange(r, 0, _cols - 1);
                break;
            case 1: // start of screen to cursor
                for (var r = 0; r < _row; r++) EraseLineRange(r, 0, _cols - 1);
                EraseLineRange(_row, 0, _col);
                break;
            case 2: // whole screen
                for (var r = 0; r < _rows; r++) EraseLineRange(r, 0, _cols - 1);
                break;
            case 3: // whole screen + scrollback
                for (var r = 0; r < _rows; r++) EraseLineRange(r, 0, _cols - 1);
                _scrollback.Clear();
                break;
        }
    }

    private void EraseInLine(int mode)
    {
        switch (mode)
        {
            case 0: EraseLineRange(_row, _col, _cols - 1); break;
            case 1: EraseLineRange(_row, 0, _col); break;
            case 2: EraseLineRange(_row, 0, _cols - 1); break;
        }
    }

    private void EraseLineRange(int row, int from, int to)
    {
        for (var c = from; c <= to && c < _cols; c++)
            _grid[row][c] = BlankCell();
    }

    private void EraseChars(int count)
    {
        // ECH: blank `count` cells from the cursor; the cursor does not move.
        for (var i = 0; i < count && _col + i < _cols; i++)
            _grid[_row][_col + i] = BlankCell();
        _wrapPending = false;
    }

    private void InsertChars(int count)
    {
        // ICH: shift cells at/after the cursor right by `count`, filling with blanks.
        var row = _grid[_row];
        for (var c = _cols - 1; c >= _col + count; c--)
            row[c] = row[c - count];
        for (var c = _col; c < _col + count && c < _cols; c++)
            row[c] = BlankCell();
        _wrapPending = false;
    }

    private void DeleteChars(int count)
    {
        // DCH: shift cells after the cursor left by `count`, filling the tail with blanks.
        var row = _grid[_row];
        for (var c = _col; c < _cols; c++)
            row[c] = c + count < _cols ? row[c + count] : BlankCell();
        _wrapPending = false;
    }

    // ---- line edits, scroll region, tabs, cursor save/restore, resets (P2) ----

    private void InsertLines(int n)
    {
        if (_row < _top || _row > _bottom) return;       // IL only acts inside the margins
        n = Math.Min(n, _bottom - _row + 1);
        for (var r = _bottom; r >= _row + n; r--) _grid[r] = _grid[r - n];
        for (var r = _row; r < _row + n; r++) _grid[r] = NewBlankRow();
        // NB: cursor column is NOT reset — ECMA-48 says move to line home, but xterm (and libvterm)
        // leave the cursor put, and we match xterm.
        _wrapPending = false;
    }

    private void DeleteLines(int n)
    {
        if (_row < _top || _row > _bottom) return;       // DL only acts inside the margins
        n = Math.Min(n, _bottom - _row + 1);
        for (var r = _row; r <= _bottom - n; r++) _grid[r] = _grid[r + n];
        for (var r = _bottom - n + 1; r <= _bottom; r++) _grid[r] = NewBlankRow();
        // cursor column unchanged (see InsertLines)
        _wrapPending = false;
    }

    private void SetScrollRegion()
    {
        var top = Param(0, 1) - 1;
        var bottom = Param(1, _rows) - 1;
        if (top < 0) top = 0;
        if (bottom > _rows - 1) bottom = _rows - 1;
        if (top >= bottom) { top = 0; bottom = _rows - 1; } // invalid -> full screen
        _top = top;
        _bottom = bottom;
        _row = 0; _col = 0; // DECSTBM homes the cursor
        _wrapPending = false;
    }

    private void ReverseIndex()
    {
        _wrapPending = false;
        if (_row == _top) ScrollRegionDown(1);
        else if (_row > 0) _row--;
    }

    private int NextTabStop(int from)
    {
        for (var c = from + 1; c < _cols; c++) if (_tabs[c]) return c;
        return _cols - 1;
    }

    private int PrevTabStop(int from)
    {
        for (var c = from - 1; c > 0; c--) if (_tabs[c]) return c;
        return 0;
    }

    private void TabForward(int n) { for (var i = 0; i < n; i++) _col = NextTabStop(_col); _wrapPending = false; }
    private void TabBackward(int n) { for (var i = 0; i < n; i++) _col = PrevTabStop(_col); _wrapPending = false; }

    private void ClearTabs(int mode)
    {
        if (mode == 3) Array.Clear(_tabs);            // clear all
        else if (_col < _cols) _tabs[_col] = false;   // clear at cursor (mode 0)
    }

    private void SaveCursor() =>
        _saved = new SavedCursor(_row, _col, _fg, _bg, _bold, _italic, _underline, _wrapPending);

    private void RestoreCursor()
    {
        if (_saved is not { } s) { _row = 0; _col = 0; _wrapPending = false; return; }
        _row = Math.Clamp(s.Row, 0, _rows - 1);
        _col = Math.Clamp(s.Col, 0, _cols - 1);
        _fg = s.Fg; _bg = s.Bg; _bold = s.Bold; _italic = s.Italic; _underline = s.Underline;
        _wrapPending = s.WrapPending;
    }

    private void SoftReset() // DECSTR
    {
        _top = 0; _bottom = _rows - 1;
        ResetSgr();
        _saved = null;
        _row = 0; _col = 0;
        _wrapPending = false;
    }

    private void HardReset() // RIS
    {
        for (var r = 0; r < _rows; r++)
            for (var c = 0; c < _cols; c++)
                _grid[r][c] = new Cell();
        _top = 0; _bottom = _rows - 1;
        _tabs = BuildDefaultTabs();
        _scrollback.Clear();
        _saved = null;
        ResetSgr();
        _row = 0; _col = 0;
        _wrapPending = false;
    }

    private void EscDispatch(char final)
    {
        switch (final)
        {
            case 'D': LineFeed(); break;                 // IND
            case 'E': _col = 0; LineFeed(); break;       // NEL
            case 'M': ReverseIndex(); break;             // RI
            case '7': SaveCursor(); break;               // DECSC
            case '8': RestoreCursor(); break;            // DECRC
            case 'c': HardReset(); break;                // RIS
            case 'H': if (_col < _cols) _tabs[_col] = true; break; // HTS
            // '=', '>', charset designators, etc. are no-ops / later phases.
        }
    }

    // ---- SGR (handles both `38;2;r;g;b` and the ITU colon form `38:2::r:g:b`) ----

    private void ApplySgr()
    {
        var count = _params.Count;
        if (count == 0) { ResetSgr(); return; }

        var i = 0;
        while (i < count)
        {
            var code = Val(i);
            if (code == 38 || code == 48) { i = ApplyExtendedColor(code, i); continue; }

            // A colon group is the code plus any following colon-joined sub-parameters.
            var groupEnd = i;
            while (groupEnd + 1 < count && _sub[groupEnd + 1]) groupEnd++;
            ApplySimpleSgr(code, i, groupEnd);
            i = groupEnd + 1;
        }
    }

    private void ResetSgr()
    {
        _fg = null; _bg = null; _bold = false; _italic = false; _underline = false;
    }

    private void ApplySimpleSgr(int code, int start, int groupEnd)
    {
        switch (code)
        {
            case 0: ResetSgr(); break;
            case 1: _bold = true; break;
            case 3: _italic = true; break;
            case 4: _underline = groupEnd <= start || Val(start + 1) != 0; break; // 4 / 4:0 (off) / 4:n (on)
            case 21: _underline = true; break; // doubly underlined → underline (no style model yet)
            case 22: _bold = false; break;
            case 23: _italic = false; break;
            case 24: _underline = false; break;
            case >= 30 and <= 37: _fg = (code - 30).ToString(CultureInfo.InvariantCulture); break;
            case 39: _fg = null; break;
            case >= 40 and <= 47: _bg = (code - 40).ToString(CultureInfo.InvariantCulture); break;
            case 49: _bg = null; break;
            case >= 90 and <= 97: _fg = (code - 90 + 8).ToString(CultureInfo.InvariantCulture); break;
            case >= 100 and <= 107: _bg = (code - 100 + 8).ToString(CultureInfo.InvariantCulture); break;
            // 2 (dim), 5/6 (blink), 7 (reverse), 8 (conceal), 9 (strike), 53/55 (overline), 58/59
            // (underline color): no cell-model field yet (P3). Accepted and ignored.
        }
    }

    /// <summary>Applies SGR 38/48 (extended fg/bg) starting at parameter <paramref name="i"/>, supporting
    /// both the legacy semicolon form and the ITU colon sub-parameter form. Returns the next index.</summary>
    private int ApplyExtendedColor(int code, int i)
    {
        var count = _params.Count;
        if (i + 1 >= count) return i + 1;

        if (_sub[i + 1]) // colon form: 38:2::r:g:b or 38:5:n
        {
            var groupEnd = i + 1;
            while (groupEnd + 1 < count && _sub[groupEnd + 1]) groupEnd++;
            var selector = Val(i + 1);
            if (selector == 2)
            {
                // sub-values after the selector: [colorspace?] r g b
                var n = groupEnd - (i + 1); // count of values after selector
                var baseIdx = n >= 4 ? i + 3 : i + 2; // skip optional color-space id when present
                if (baseIdx + 2 <= groupEnd)
                    SetColor(code, Rgb(Val(baseIdx), Val(baseIdx + 1), Val(baseIdx + 2)));
            }
            else if (selector == 5 && i + 2 <= groupEnd)
            {
                SetColor(code, Val(i + 2).ToString(CultureInfo.InvariantCulture));
            }
            return groupEnd + 1;
        }

        // semicolon form
        var sel = Val(i + 1);
        if (sel == 2)
        {
            SetColor(code, Rgb(Val(i + 2), Val(i + 3), Val(i + 4)));
            return i + 5;
        }
        if (sel == 5)
        {
            SetColor(code, Val(i + 2).ToString(CultureInfo.InvariantCulture));
            return i + 3;
        }
        return i + 2;
    }

    private static string Rgb(int r, int g, int b) =>
        $"#{r & 0xFF:x2}{g & 0xFF:x2}{b & 0xFF:x2}";

    private void SetColor(int code, string value)
    {
        if (code == 38) _fg = value; else _bg = value;
    }

    /// <summary>Parameter value with empty (-1) and out-of-range normalised to 0 (for color
    /// components / indices / SGR codes).</summary>
    private int Val(int index) => index < _params.Count && _params[index] > 0 ? _params[index] : 0;

    /// <summary>Snapshots the current screen to a <see cref="TerminalContent"/>.</summary>
    public TerminalContent ToTerminalContent()
    {
        var cells = new TerminalCell[_rows][];
        for (var r = 0; r < _rows; r++)
        {
            cells[r] = new TerminalCell[_cols];
            for (var c = 0; c < _cols; c++)
            {
                var s = _grid[r][c];
                cells[r][c] = new TerminalCell
                {
                    Character = s.Char.Length == 0 ? "" : s.Char,
                    ForegroundColor = s.Fg,
                    BackgroundColor = s.Bg,
                    IsBold = s.Bold,
                    IsItalic = s.Italic,
                    IsUnderline = s.Underline,
                    Width = s.Width,
                };
            }
        }

        return new TerminalContent
        {
            Cols = _cols,
            Rows = _rows,
            Cells = cells,
            CursorX = _col,
            CursorY = _row,
            CursorVisible = false,
        };
    }

    /// <summary>Approximate display width of a rune: 2 for common East-Asian-wide/emoji ranges, else 1.</summary>
    private static int RuneWidth(Rune rune)
    {
        var cp = rune.Value;
        if (cp == 0) return 0;
        // Common wide ranges (CJK, Hangul, fullwidth, common emoji blocks).
        if ((cp >= 0x1100 && cp <= 0x115F) ||   // Hangul Jamo
            (cp >= 0x2E80 && cp <= 0x303E) ||   // CJK radicals / Kangxi
            (cp >= 0x3041 && cp <= 0x33FF) ||   // Hiragana..CJK symbols
            (cp >= 0x3400 && cp <= 0x4DBF) ||   // CJK Ext A
            (cp >= 0x4E00 && cp <= 0x9FFF) ||   // CJK Unified
            (cp >= 0xA000 && cp <= 0xA4CF) ||   // Yi
            (cp >= 0xAC00 && cp <= 0xD7A3) ||   // Hangul syllables
            (cp >= 0xF900 && cp <= 0xFAFF) ||   // CJK compat
            (cp >= 0xFF00 && cp <= 0xFF60) ||   // Fullwidth forms
            (cp >= 0xFFE0 && cp <= 0xFFE6) ||
            (cp >= 0x1F300 && cp <= 0x1FAFF) || // emoji / symbols
            (cp >= 0x20000 && cp <= 0x3FFFD))   // CJK Ext B+
            return 2;
        return 1;
    }
}
