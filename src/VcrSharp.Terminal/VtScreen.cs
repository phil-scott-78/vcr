using System.Globalization;
using System.Text;
using VcrSharp.Core.Rendering;

namespace VcrSharp.Terminal;

/// <summary>
/// A minimal in-process VT/ANSI terminal screen. Feed it the byte/char stream a program writes (or
/// that ConPTY re-emits) and it maintains a fixed Cols×Rows cell grid with SGR attributes and a
/// cursor. Snapshot it to a <see cref="TerminalContent"/> — the same cell grid the SVG pipeline
/// already consumes — so the existing <c>SvgRenderer</c> can render it with no browser involved.
///
/// Implements the common subset: printable text (with autowrap + wide-char width), C0 controls
/// (CR/LF/BS/HT), SGR colors/bold/italic/underline (16 / 256 / truecolor), cursor positioning
/// (CUP/CUU/CUD/CUF/CUB/CHA), and erase (ED/EL). DEC private modes, scroll regions, OSC, and the
/// alternate screen are intentionally ignored for now (flagged for later).
/// </summary>
public sealed class VtScreen
{
    private enum State { Ground, Escape, CsiEntry, CsiParam, CsiIgnore, StringSeq, StringSeqEsc, Charset }

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

    private State _state = State.Ground;
    private readonly List<int> _params = new();
    private int _curParam;
    private bool _hasParam;
    private bool _privateMarker;

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
    }

    public int Cols => _cols;
    public int Rows => _rows;

    /// <summary>Feeds decoded text (the PTY reader handles byte→char UTF-8 decoding across reads).</summary>
    public void Feed(string text)
    {
        foreach (var rune in text.EnumerateRunes())
            FeedRune(rune);
    }

    private void FeedRune(Rune rune)
    {
        var cp = rune.Value;

        switch (_state)
        {
            case State.Ground:
                if (cp == 0x1B) { _state = State.Escape; return; }
                if (cp < 0x20) { ExecuteC0(cp); return; }
                Print(rune);
                return;

            case State.Escape:
                if (cp == '[') { BeginCsi(); return; }
                // OSC (]), DCS (P), SOS (X), PM (^), APC (_): string sequences ending in BEL or ST.
                if (cp is ']' or 'P' or 'X' or '^' or '_') { _state = State.StringSeq; return; }
                // Charset designators ESC ( ) * + consume one more (the charset id).
                if (cp is >= 0x28 and <= 0x2B) { _state = State.Charset; return; }
                // ST terminator or any other single-char escape (ESC M, 7, 8, =, >, c, ...) -> ignore.
                _state = State.Ground;
                return;

            case State.Charset:
                _state = State.Ground; // consume the charset id (e.g. 'B')
                return;

            case State.StringSeq:
                if (cp == 0x07) { _state = State.Ground; return; }   // BEL terminates OSC
                if (cp == 0x1B) { _state = State.StringSeqEsc; return; } // possible ST (ESC \)
                return; // consume string content (title text, etc.)

            case State.StringSeqEsc:
                _state = State.Ground; // ESC \ = ST (or an aborted sequence) -> back to ground
                return;

            case State.CsiEntry:
            case State.CsiParam:
                if (cp is >= 0x30 and <= 0x39) { _curParam = _curParam * 10 + (cp - 0x30); _hasParam = true; _state = State.CsiParam; return; }
                if (cp == ';') { _params.Add(_hasParam ? _curParam : 0); _curParam = 0; _hasParam = false; _state = State.CsiParam; return; }
                if (cp == '?' && _state == State.CsiEntry) { _privateMarker = true; return; }
                if (cp is >= 0x20 and <= 0x2F) { _state = State.CsiIgnore; return; } // intermediate bytes -> ignore
                if (cp is >= 0x40 and <= 0x7E) { _params.Add(_hasParam ? _curParam : 0); DispatchCsi((char)cp); _state = State.Ground; return; }
                _state = State.Ground;
                return;

            case State.CsiIgnore:
                if (cp is >= 0x40 and <= 0x7E) _state = State.Ground;
                return;
        }
    }

    private void BeginCsi()
    {
        _state = State.CsiEntry;
        _params.Clear();
        _curParam = 0;
        _hasParam = false;
        _privateMarker = false;
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
                _col = Math.Min(_cols - 1, (_col / 8 + 1) * 8);
                _wrapPending = false;
                break;
        }
    }

    private void LineFeed()
    {
        _wrapPending = false;
        if (_row >= _rows - 1)
            ScrollUp();
        else
            _row++;
    }

    private void ScrollUp()
    {
        var top = _grid[0];
        for (var r = 0; r < _rows - 1; r++)
            _grid[r] = _grid[r + 1];
        // reuse the top row array, cleared, as the new bottom row
        for (var c = 0; c < _cols; c++)
            top[c] = BlankCell();
        _grid[_rows - 1] = top;
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
        if (width == 0) width = 1; // treat zero-width/combining as 1 for the PoC

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
            case 'A': _row = Math.Max(0, _row - Param(0, 1)); _wrapPending = false; break;
            case 'B': _row = Math.Min(_rows - 1, _row + Param(0, 1)); _wrapPending = false; break;
            case 'C': _col = Math.Min(_cols - 1, _col + Param(0, 1)); _wrapPending = false; break;
            case 'D': _col = Math.Max(0, _col - Param(0, 1)); _wrapPending = false; break;
            case 'G': _col = Math.Clamp(Param(0, 1) - 1, 0, _cols - 1); _wrapPending = false; break;
            case 'd': _row = Math.Clamp(Param(0, 1) - 1, 0, _rows - 1); _wrapPending = false; break;
            case 'J': EraseInDisplay(Param(0, 0)); break;
            case 'K': EraseInLine(Param(0, 0)); break;
            case 'X': EraseChars(Param(0, 1)); break;     // ECH: erase N chars from cursor (Spectre uses this)
            case '@': InsertChars(Param(0, 1)); break;    // ICH: insert N blanks, shift right
            case 'P': DeleteChars(Param(0, 1)); break;    // DCH: delete N chars, shift left
            // h/l (mode set/reset), r (scroll region), etc. ignored for now.
        }
    }

    private int Param(int index, int fallback)
    {
        if (index < _params.Count)
        {
            var v = _params[index];
            return v == 0 && fallback != 0 ? fallback : v;
        }
        return fallback;
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
            case 3:
                for (var r = 0; r < _rows; r++) EraseLineRange(r, 0, _cols - 1);
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

    private void ApplySgr()
    {
        var p = _params;
        for (var i = 0; i < p.Count; i++)
        {
            var code = p[i];
            switch (code)
            {
                case 0: _fg = null; _bg = null; _bold = false; _italic = false; _underline = false; break;
                case 1: _bold = true; break;
                case 3: _italic = true; break;
                case 4: _underline = true; break;
                case 22: _bold = false; break;
                case 23: _italic = false; break;
                case 24: _underline = false; break;
                case >= 30 and <= 37: _fg = (code - 30).ToString(CultureInfo.InvariantCulture); break;
                case 39: _fg = null; break;
                case >= 40 and <= 47: _bg = (code - 40).ToString(CultureInfo.InvariantCulture); break;
                case 49: _bg = null; break;
                case >= 90 and <= 97: _fg = (code - 90 + 8).ToString(CultureInfo.InvariantCulture); break;
                case >= 100 and <= 107: _bg = (code - 100 + 8).ToString(CultureInfo.InvariantCulture); break;
                case 38: _fg = ReadExtendedColor(p, ref i); break;
                case 48: _bg = ReadExtendedColor(p, ref i); break;
            }
        }
    }

    /// <summary>Reads a 38/48 extended color (<c>;5;n</c> = 256-palette, <c>;2;r;g;b</c> = truecolor).</summary>
    private static string? ReadExtendedColor(List<int> p, ref int i)
    {
        if (i + 1 >= p.Count) return null;
        var kind = p[i + 1];
        if (kind == 5 && i + 2 < p.Count)
        {
            var idx = p[i + 2];
            i += 2;
            return idx.ToString(CultureInfo.InvariantCulture);
        }
        if (kind == 2 && i + 4 < p.Count)
        {
            var r = p[i + 2] & 0xFF;
            var g = p[i + 3] & 0xFF;
            var b = p[i + 4] & 0xFF;
            i += 4;
            return $"#{r:x2}{g:x2}{b:x2}";
        }
        return null;
    }

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
