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

    private enum Charset { Ascii, SpecialGraphics }

    private sealed class Cell
    {
        public string Char = " ";
        public string? Fg;
        public string? Bg;
        public bool Bold;
        public bool Italic;
        public bool Dim;
        public bool Blink;
        public bool Reverse;
        public bool Conceal;
        public bool Strike;
        public bool Overline;
        public int UnderlineStyle; // 0 none, 1 single, 2 double, 3 curly, 4 dotted, 5 dashed
        public string? UnderlineColor;
        public int Width = 1;
    }

    private int _cols;            // mutable: changed by Resize
    private int _rows;
    private Cell[][] _grid;        // active buffer (points at _main or _alt)
    private Cell[][] _main;
    private Cell[][] _alt;
    private bool _onAlt;

    private int _row;
    private int _col;
    private bool _wrapPending; // deferred autowrap: set after writing the last column

    // Current SGR pen
    private string? _fg;
    private string? _bg;
    private bool _bold;
    private bool _italic;
    private bool _dim;
    private bool _blink;
    private bool _reverse;
    private bool _conceal;
    private bool _strike;
    private bool _overline;
    private int _underlineStyle;       // 0 none, 1 single, 2 double, 3 curly, 4 dotted, 5 dashed
    private string? _underlineColor;

    // Charset state (G0/G1 designations + active GL) for DEC line-drawing
    private readonly Charset[] _charsets = { Charset.Ascii, Charset.Ascii };
    private int _gl;

    // Current SGR pen, exposed for conformance/inspection.
    public bool PenBold => _bold;
    public bool PenItalic => _italic;
    public int PenUnderline => _underlineStyle;
    public bool PenBlink => _blink;
    public bool PenReverse => _reverse;

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
    private SavedCursor? _saved;     // DECSC / SCOSC
    private SavedCursor? _altSaved;  // alt-screen (1048/1049) save slot
    private readonly List<Cell[]> _scrollback = new();
    private const int ScrollbackMax = 1000;

    // Modes (DEC private + ANSI). Defaults match a freshly reset xterm.
    private bool _autoWrap = true;      // DECAWM (?7)
    private bool _cursorVisible = true; // DECTCEM (?25)
    private bool _originMode;           // DECOM (?6)
    private bool _insertMode;           // IRM (4)
    private bool _newlineMode;          // LNM (20)

    private readonly record struct Pen(
        string? Fg, string? Bg, bool Bold, bool Italic, bool Dim, bool Blink, bool Reverse,
        bool Conceal, bool Strike, bool Overline, int UnderlineStyle, string? UnderlineColor);

    private readonly record struct SavedCursor(int Row, int Col, Pen Pen, bool WrapPending);

    private Pen CapturePen() => new(_fg, _bg, _bold, _italic, _dim, _blink, _reverse, _conceal,
        _strike, _overline, _underlineStyle, _underlineColor);

    private void ApplyPen(Pen p)
    {
        _fg = p.Fg; _bg = p.Bg; _bold = p.Bold; _italic = p.Italic; _dim = p.Dim; _blink = p.Blink;
        _reverse = p.Reverse; _conceal = p.Conceal; _strike = p.Strike; _overline = p.Overline;
        _underlineStyle = p.UnderlineStyle; _underlineColor = p.UnderlineColor;
    }

    public VtScreen(int cols, int rows)
    {
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
        _main = NewBuffer();
        _alt = NewBuffer();
        _grid = _main;
        _bottom = _rows - 1;
        _tabs = BuildDefaultTabs();
    }

    private Cell[][] NewBuffer()
    {
        var g = new Cell[_rows][];
        for (var r = 0; r < _rows; r++)
        {
            g[r] = new Cell[_cols];
            for (var c = 0; c < _cols; c++)
                g[r][c] = new Cell();
        }
        return g;
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

    /// <summary>
    /// Resizes the screen, preserving content top-left-anchored (no reflow). Growing adds blank rows
    /// at the bottom / blank columns at the right. Shrinking truncates blank bottom rows/right columns;
    /// if the cursor or bottom content would be cut, the top is scrolled into the scrollback ring just
    /// enough to keep them on screen (matching xterm/libvterm without reflow).
    /// </summary>
    public void Resize(int newCols, int newRows)
    {
        newCols = Math.Max(1, newCols);
        newRows = Math.Max(1, newRows);
        if (newCols == _cols && newRows == _rows) return;

        if (newRows < _rows)
        {
            var scroll = Math.Max(0, Math.Max(LowestNonBlankRow(), _row) - (newRows - 1));
            for (var i = 0; i < scroll; i++)
            {
                PushScrollback(_grid[0]);
                for (var r = 0; r < _rows - 1; r++) _grid[r] = _grid[r + 1];
                _grid[_rows - 1] = NewBlankRow();
            }
            _row -= scroll;
        }

        _main = ResizeBuffer(_main, newCols, newRows);
        _alt = ResizeBuffer(_alt, newCols, newRows);
        _grid = _onAlt ? _alt : _main;
        _cols = newCols;
        _rows = newRows;
        _row = Math.Clamp(_row, 0, _rows - 1);
        _col = Math.Clamp(_col, 0, _cols - 1);
        _top = 0;
        _bottom = _rows - 1;
        _tabs = BuildDefaultTabs();
        // A deferred wrap (phantom cursor pinned at the right margin) becomes a real position when a
        // wider resize makes room — libvterm "doesn't cancel the phantom".
        if (_wrapPending && _col + 1 < _cols) { _col++; _wrapPending = false; }
    }

    private static Cell[][] ResizeBuffer(Cell[][] old, int newCols, int newRows)
    {
        var oldRows = old.Length;
        var oldCols = oldRows > 0 ? old[0].Length : 0;
        var g = new Cell[newRows][];
        for (var r = 0; r < newRows; r++)
        {
            g[r] = new Cell[newCols];
            for (var c = 0; c < newCols; c++)
                g[r][c] = r < oldRows && c < oldCols ? old[r][c] : new Cell();
        }
        return g;
    }

    private int LowestNonBlankRow()
    {
        for (var r = _rows - 1; r >= 0; r--)
            for (var c = 0; c < _cols; c++)
            {
                var ch = _grid[r][c].Char;
                if (ch.Length > 0 && ch != " ") return r;
            }
        return 0;
    }

    // ---- Williams VT500 parser ----

    private void FeedRune(Rune rune)
    {
        var cp = rune.Value;

        // "Anywhere" transitions: ESC always cancels the current sequence/string and re-enters escape
        // handling (so the byte after a string's terminating ESC is NOT dropped); CAN/SUB abort.
        if (cp == 0x1B) { _state = State.Escape; _interChar = '\0'; return; }
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
                if (cp is >= 0x20 and <= 0x2F) { _interChar = (char)cp; _state = State.EscapeIntermediate; return; }
                switch (cp)
                {
                    case '[': ClearParams(); _state = State.CsiEntry; return;   // CSI
                    case ']': _state = State.OscString; return;                 // OSC
                    case 'P': _state = State.StringConsume; return;             // DCS
                    case 'X': case '^': case '_': _state = State.StringConsume; return; // SOS/PM/APC
                }
                if (cp is >= 0x30 and <= 0x7E) { EscDispatch((char)cp); _state = State.Ground;
                }
                return;

            case State.EscapeIntermediate:
                if (cp is >= 0x20 and <= 0x2F) { _interChar = (char)cp; return; }
                if (cp is >= 0x30 and <= 0x7E) { EscIntermediateDispatch(_interChar, (char)cp); _state = State.Ground; }
                return;

            case State.CsiEntry:
            case State.CsiParam:
                // Saturate BEFORE multiplying: once at the cap, stop accumulating so `_cur * 10` can never
                // overflow int and wrap negative (a 10+ digit param otherwise defeated the Math.Min cap).
                if (cp is >= 0x30 and <= 0x39) { _cur = _cur >= 0x0FFFFFFF ? 0x0FFFFFFF : Math.Min(_cur * 10 + (cp - 0x30), 0x0FFFFFFF); _curHasDigits = true; _state = State.CsiParam; return; }
                if (cp == ';') { CommitParam(); _pendingSub = false; _state = State.CsiParam; return; }
                if (cp == ':') { CommitParam(); _pendingSub = true; _state = State.CsiParam; return; }
                if (cp is >= 0x3C and <= 0x3F) // private/leader bytes < = > ?
                {
                    if (_state == State.CsiEntry) { _privateMarker = cp == '?'; _state = State.CsiParam; return; }
                    _state = State.CsiIgnore; return;
                }
                if (cp is >= 0x20 and <= 0x2F) { _interChar = (char)cp; _state = State.CsiIntermediate; return; }
                if (cp is >= 0x40 and <= 0x7E) { CommitParam(); DispatchCsi((char)cp); _state = State.Ground; }
                return;

            case State.CsiIntermediate:
                if (cp is >= 0x20 and <= 0x2F) { _interChar = (char)cp; return; }
                if (cp is >= 0x40 and <= 0x7E) { CommitParam(); DispatchCsi((char)cp); _state = State.Ground; return; }
                if (cp is >= 0x30 and <= 0x3F) { _state = State.CsiIgnore; }
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
                if (_newlineMode) _col = 0; // LNM: LF also carriage-returns
                LineFeed();
                break;
            case 0x0E: _gl = 1; break; // SO: invoke G1 into GL
            case 0x0F: _gl = 0; break; // SI: invoke G0 into GL
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
        // Combining marks, emoji modifiers/joiners, and the emoji following a ZWJ all attach to the
        // previous base cell (one grapheme cluster) rather than occupying new cells.
        if (IsCombining(rune) || IsEmojiExtender(rune) || PrecededByZwj()) { AppendCombining(rune); return; }

        // DEC special-graphics (line drawing) maps ASCII into box-drawing glyphs when active.
        if (_charsets[_gl] == Charset.SpecialGraphics) rune = MapSpecialGraphics(rune);

        if (_wrapPending && _autoWrap)
        {
            _col = 0;
            LineFeed();
        }
        _wrapPending = false;

        var width = RuneWidth(rune);
        if (width == 0) width = 1;

        if (_col + width > _cols)
        {
            if (_autoWrap)
            {
                _col = 0;
                LineFeed();
            }
            else
            {
                _col = Math.Max(0, _cols - width); // no autowrap: overwrite at the right margin
            }
        }

        if (_insertMode) InsertChars(width); // IRM: shift right to make room before writing

        var cell = _grid[_row][_col];
        cell.Char = rune.ToString();
        cell.Fg = _fg;
        cell.Bg = _bg;
        cell.Bold = _bold;
        cell.Italic = _italic;
        cell.Dim = _dim;
        cell.Blink = _blink;
        cell.Reverse = _reverse;
        cell.Conceal = _conceal;
        cell.Strike = _strike;
        cell.Overline = _overline;
        cell.UnderlineStyle = _underlineStyle;
        cell.UnderlineColor = _underlineColor;
        cell.Width = width;

        if (width == 2 && _col + 1 < _cols)
        {
            var cont = _grid[_row][_col + 1];
            cont.Char = "";
            cont.Fg = _fg;
            cont.Bg = _bg;
            cont.Bold = false;
            cont.Italic = false;
            cont.Dim = false;
            cont.Blink = false;
            cont.Reverse = false;
            cont.Conceal = false;
            cont.Strike = false;
            cont.Overline = false;
            cont.UnderlineStyle = 0;
            cont.UnderlineColor = null;
            cont.Width = 0; // continuation
        }

        _col += width;
        if (_col >= _cols)
        {
            _col = _cols - 1;
            if (_autoWrap) _wrapPending = true; // deferred wrap only when DECAWM is on
        }
    }

    private void DispatchCsi(char final)
    {
        switch (final)
        {
            case 'm': ApplySgr(); break;
            case 'H':
            case 'f':
                _row = RowFromParam(Param(0, 1));
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
            case 'j': _col = Math.Max(0, _col - Param(0, 1)); _wrapPending = false; break;             // HPB
            case 'k': _row = Math.Max(0, _row - Param(0, 1)); _wrapPending = false; break;             // VPB
            case 'd': _row = RowFromParam(Param(0, 1)); _wrapPending = false; break;   // VPA
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
            case 'h': SetModes(true); break;              // SM / DECSET
            case 'l': SetModes(false); break;             // RM / DECRST
        }
    }

    private int CursorUpLimit() => _row >= _top ? _top : 0;
    private int CursorDownLimit() => _row <= _bottom ? _bottom : _rows - 1;

    /// <summary>Maps a 1-based row parameter to a 0-based row, honoring origin mode (relative to the
    /// scroll region) when DECOM is set.</summary>
    private int RowFromParam(int oneBased) =>
        _originMode
            ? Math.Clamp(_top + oneBased - 1, _top, _bottom)
            : Math.Clamp(oneBased - 1, 0, _rows - 1);

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
        _row = _originMode ? _top : 0; _col = 0; // DECSTBM homes the cursor
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

    private void SaveCursor() => _saved = new SavedCursor(_row, _col, CapturePen(), _wrapPending);

    private void RestoreCursor()
    {
        if (_saved is not { } s) { _row = 0; _col = 0; _wrapPending = false; return; }
        _row = Math.Clamp(s.Row, 0, _rows - 1);
        _col = Math.Clamp(s.Col, 0, _cols - 1);
        ApplyPen(s.Pen);
        _wrapPending = s.WrapPending;
    }

    private void SoftReset() // DECSTR
    {
        _top = 0; _bottom = _rows - 1;
        ResetSgr();
        _saved = null;
        _originMode = false;
        _insertMode = false;
        _autoWrap = true;
        _cursorVisible = true;
        _row = 0; _col = 0;
        _wrapPending = false;
    }

    private void HardReset() // RIS
    {
        _onAlt = false; _grid = _main;
        foreach (var buffer in new[] { _main, _alt })
            for (var r = 0; r < _rows; r++)
                for (var c = 0; c < _cols; c++)
                    buffer[r][c] = new Cell();
        _top = 0; _bottom = _rows - 1;
        _tabs = BuildDefaultTabs();
        _scrollback.Clear();
        _saved = null; _altSaved = null;
        _originMode = false; _insertMode = false; _newlineMode = false;
        _autoWrap = true; _cursorVisible = true;
        _charsets[0] = Charset.Ascii; _charsets[1] = Charset.Ascii; _gl = 0;
        ResetSgr();
        _row = 0; _col = 0;
        _wrapPending = false;
    }

    // ---- modes + alternate screen (P4) ----

    private void SetModes(bool on)
    {
        foreach (var p in _params)
        {
            if (p < 0) continue;
            if (_privateMarker) SetPrivateMode(p, on);
            else SetAnsiMode(p, on);
        }
    }

    private void SetPrivateMode(int code, bool on)
    {
        switch (code)
        {
            case 5:
                break;                                            // DECSCNM
            case 6: _originMode = on; _row = on ? _top : 0; _col = 0; _wrapPending = false; break; // DECOM
            case 7: _autoWrap = on; break;                                                 // DECAWM
            case 25: _cursorVisible = on; break;                                           // DECTCEM
            case 47: AltSwitch(on, clearEnter: false, clearLeave: false, save: false); break;
            case 1047: AltSwitch(on, clearEnter: false, clearLeave: true, save: false); break;
            case 1048: if (on) SaveAltCursor(); else RestoreAltCursor(); break;
            case 1049: AltSwitch(on, clearEnter: true, clearLeave: false, save: true); break;
        }
    }

    private void SetAnsiMode(int code, bool on)
    {
        switch (code)
        {
            case 4: _insertMode = on; break;   // IRM
            case 20: _newlineMode = on; break; // LNM
        }
    }

    private void AltSwitch(bool on, bool clearEnter, bool clearLeave, bool save)
    {
        if (on)
        {
            if (_onAlt) return;
            if (save) SaveAltCursor();
            _onAlt = true; _grid = _alt;
            _top = 0; _bottom = _rows - 1;       // alt screen uses the full page
            if (clearEnter) ClearActive();
        }
        else
        {
            if (!_onAlt) return;
            if (clearLeave) ClearActive();
            _onAlt = false; _grid = _main;
            _top = 0; _bottom = _rows - 1;
            if (save) RestoreAltCursor();
        }
    }

    private void ClearActive()
    {
        for (var r = 0; r < _rows; r++)
            for (var c = 0; c < _cols; c++)
                _grid[r][c] = BlankCell();
        _row = 0; _col = 0; _wrapPending = false;
    }

    private void SaveAltCursor() => _altSaved = new SavedCursor(_row, _col, CapturePen(), _wrapPending);

    private void RestoreAltCursor()
    {
        if (_altSaved is not { } s) { _row = 0; _col = 0; _wrapPending = false; return; }
        _row = Math.Clamp(s.Row, 0, _rows - 1);
        _col = Math.Clamp(s.Col, 0, _cols - 1);
        ApplyPen(s.Pen);
        _wrapPending = s.WrapPending;
    }

    private void EscIntermediateDispatch(char intermediate, char final)
    {
        if (intermediate == '#')
        {
            if (final == '8') DecAlign(); // DECALN: fill the screen with 'E' (vttest alignment pattern)
            return;                        // '#' 3/4/5/6 (double-width/height lines) are out of scope
        }

        // Charset designation: ESC ( c → G0, ESC ) c → G1 (c == '0' selects DEC special graphics).
        var cs = final == '0' ? Charset.SpecialGraphics : Charset.Ascii;
        if (intermediate == '(') _charsets[0] = cs;
        else if (intermediate == ')') _charsets[1] = cs;
        // '*'/'+' (G2/G3) accepted/ignored for now.
    }

    private void DecAlign()
    {
        for (var r = 0; r < _rows; r++)
            for (var c = 0; c < _cols; c++)
                _grid[r][c] = new Cell { Char = "E" };
        _row = 0; _col = 0; _wrapPending = false;
    }

    private static bool IsCombining(Rune rune)
    {
        var cat = Rune.GetUnicodeCategory(rune);
        return cat is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }

    /// <summary>Column of the most recently written base cell (skipping wide-char continuations), or -1.</summary>
    private int LastBaseCol()
    {
        var col = _wrapPending ? _col : _col - 1;
        while (col >= 0 && _grid[_row][col].Width == 0) col--;
        return col;
    }

    private void AppendCombining(Rune rune)
    {
        var col = LastBaseCol();
        if (col >= 0) _grid[_row][col].Char += rune.ToString();
    }

    /// <summary>True if the previous grapheme ends with a ZWJ, so the next emoji joins it (e.g. 👨‍👩‍👧).</summary>
    private bool PrecededByZwj()
    {
        var col = LastBaseCol();
        return col >= 0 && _grid[_row][col].Char.Length > 0 && _grid[_row][col].Char[^1] == (char)0x200D;
    }

    /// <summary>Zero-width emoji extenders: ZWJ, skin-tone modifiers, and variation selectors.</summary>
    private static bool IsEmojiExtender(Rune rune)
    {
        var cp = rune.Value;
        return cp == 0x200D
            || (cp >= 0x1F3FB && cp <= 0x1F3FF)
            || (cp >= 0xFE00 && cp <= 0xFE0F);
    }

    private static Rune MapSpecialGraphics(Rune rune)
    {
        var cp = rune.Value;
        if (cp is >= 0x5F and <= 0x7E)
        {
            var mapped = DecSpecialGraphics[cp - 0x5F];
            if (mapped != '\0') return new Rune(mapped);
        }
        return rune;
    }

    // DEC Special Graphics (line drawing) for code points 0x5F..0x7E.
    private static readonly char[] DecSpecialGraphics =
    {
        ' ', // _ blank
        '◆', // ` diamond
        '▒', // a checkerboard
        '␉', // b HT
        '␌', // c FF
        '␍', // d CR
        '␊', // e LF
        '°', // f degree
        '±', // g plus/minus
        '␤', // h NL
        '␋', // i VT
        '┘', // j ┘
        '┐', // k ┐
        '┌', // l ┌
        '└', // m └
        '┼', // n ┼
        '⎺', // o scan 1
        '⎻', // p scan 3
        '─', // q ─
        '⎼', // r scan 7
        '⎽', // s scan 9
        '├', // t ├
        '┤', // u ┤
        '┴', // v ┴
        '┬', // w ┬
        '│', // x │
        '≤', // y ≤
        '≥', // z ≥
        'π', // { π
        '≠', // | ≠
        '£', // } £
        '·', // ~ ·
    };

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
            if (code is 38 or 48 or 58) { i = ApplyExtendedColor(code, i); continue; }

            // A colon group is the code plus any following colon-joined sub-parameters.
            var groupEnd = i;
            while (groupEnd + 1 < count && _sub[groupEnd + 1]) groupEnd++;
            ApplySimpleSgr(code, i, groupEnd);
            i = groupEnd + 1;
        }
    }

    private void ResetSgr()
    {
        _fg = null; _bg = null;
        _bold = false; _italic = false; _dim = false; _blink = false; _reverse = false;
        _conceal = false; _strike = false; _overline = false;
        _underlineStyle = 0; _underlineColor = null;
    }

    private void ApplySimpleSgr(int code, int start, int groupEnd)
    {
        switch (code)
        {
            case 0: ResetSgr(); break;
            case 1: _bold = true; break;
            case 2: _dim = true; break;
            case 3: _italic = true; break;
            case 4: _underlineStyle = groupEnd > start ? Val(start + 1) : 1; break; // 4 / 4:0 off / 4:n style
            case 5: case 6: _blink = true; break;
            case 7: _reverse = true; break;
            case 8: _conceal = true; break;
            case 9: _strike = true; break;
            case 21: _underlineStyle = 2; break; // doubly underlined
            case 22: _bold = false; _dim = false; break;
            case 23: _italic = false; break;
            case 24: _underlineStyle = 0; break;
            case 25: _blink = false; break;
            case 27: _reverse = false; break;
            case 28: _conceal = false; break;
            case 29: _strike = false; break;
            case >= 30 and <= 37: _fg = (code - 30).ToString(CultureInfo.InvariantCulture); break;
            case 39: _fg = null; break;
            case >= 40 and <= 47: _bg = (code - 40).ToString(CultureInfo.InvariantCulture); break;
            case 49: _bg = null; break;
            case 53: _overline = true; break;
            case 55: _overline = false; break;
            case 59: _underlineColor = null; break;
            case >= 90 and <= 97: _fg = (code - 90 + 8).ToString(CultureInfo.InvariantCulture); break;
            case >= 100 and <= 107: _bg = (code - 100 + 8).ToString(CultureInfo.InvariantCulture); break;
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
        switch (code)
        {
            case 38: _fg = value; break;
            case 48: _bg = value; break;
            case 58: _underlineColor = value; break;
        }
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
                    IsUnderline = s.UnderlineStyle > 0,
                    UnderlineStyle = s.UnderlineStyle,
                    UnderlineColor = s.UnderlineColor,
                    IsDim = s.Dim,
                    IsBlink = s.Blink,
                    IsReverse = s.Reverse,
                    IsConceal = s.Conceal,
                    IsStrikethrough = s.Strike,
                    IsOverline = s.Overline,
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
            CursorVisible = _cursorVisible,
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
