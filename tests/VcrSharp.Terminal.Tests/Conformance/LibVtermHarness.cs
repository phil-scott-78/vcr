using System.Globalization;
using System.Text;
using VcrSharp.Core.Rendering;

namespace VcrSharp.Terminal.Tests.Conformance;

/// <summary>
/// Replays a vendored libvterm <c>*.test</c> file against <see cref="VtScreen"/>.
///
/// This is a C# reimplementation of the grid-coupled subset of upstream's <c>t/run-test.pl</c>:
/// it decodes the Perl double-quoted byte strings in <c>PUSH</c> lines, feeds them through the
/// engine, and evaluates the assertions that map onto our cell-grid snapshot
/// (<c>?cursor</c>, <c>?screen_row</c>, <c>?screen_chars</c>, <c>?screen_text</c>, and the
/// <c>movecursor</c> callback). Assertions that depend on libvterm's internal callback/event model
/// (putglyph/scrollrect/damage/settermprop/?pen/?lineinfo/?screen_cell colors/…) are counted as
/// <see cref="ConformanceResult.Skipped"/> rather than failed, so the scoreboard reflects genuine
/// text+cursor conformance without false negatives from features we don't yet model.
/// </summary>
public static class LibVtermHarness
{
    private const int FailureSampleCap = 5;

    public static ConformanceResult Run(string path)
    {
        var ctx = new Ctx(Path.GetFileName(path));
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            ctx.LineNum = i + 1;
            var trimmed = lines[i].TrimStart();
            if (trimmed == "__END__") break;
            try
            {
                DoLine(trimmed, ctx);
            }
            catch (Exception ex)
            {
                // An exception here is either the engine throwing on real input (a robustness bug we
                // WANT the no-crash gate to catch) or a harness parse hiccup. Either way record it as an
                // error and keep going so the rest of the file still measures.
                ctx.Result.Errors++;
                ctx.AddFailure($"line {ctx.LineNum}: error: {ex.GetType().Name}: {ex.Message}");
            }
        }
        ctx.FlushCursor();
        return ctx.Result;
    }

    private static void DoLine(string line, Ctx ctx)
    {
        if (line.Length == 0 || line[0] == '#') return;

        switch (line[0])
        {
            case '!': ctx.FlushCursor(); ctx.Section = line[1..].Trim(); return;
            case '?': ctx.FlushCursor(); HandleQuery(line, ctx); return;
            case '$': HandleDollar(line, ctx); return;
        }

        if (char.IsUpper(line[0])) { ctx.FlushCursor(); HandleCommand(line, ctx); return; }
        if (char.IsLower(line[0])) { HandleExpectation(line, ctx); return; }
        // anything else: ignore
    }

    // ---- commands (uppercase) ----

    private static void HandleCommand(string line, Ctx ctx)
    {
        var sp = line.IndexOf(' ');
        var verb = sp < 0 ? line : line[..sp];
        var rest = sp < 0 ? "" : line[(sp + 1)..];

        switch (verb)
        {
            case "INIT":
                ctx.Cols = 80; ctx.Rows = 25; ctx.Screen = new VtScreen(ctx.Cols, ctx.Rows);
                break;
            case "RESET":
                ctx.Screen = new VtScreen(ctx.Cols, ctx.Rows);
                break;
            case "RESIZE":
            {
                // "RESIZE rows, cols" — content-preserving.
                var nums = ParseInts(rest);
                if (nums.Count >= 2) { ctx.Rows = nums[0]; ctx.Cols = nums[1]; }
                if (ctx.Screen is null) ctx.Screen = new VtScreen(ctx.Cols, ctx.Rows);
                else ctx.Screen.Resize(ctx.Cols, ctx.Rows);
                break;
            }
            case "PUSH":
            {
                ctx.Screen ??= new VtScreen(ctx.Cols, ctx.Rows);
                var bytes = PerlBytes.Decode(rest);
                ctx.Screen.Feed(Encoding.UTF8.GetString(bytes));
                break;
            }
            // Recognised-but-unmodelled commands (input generation, encoding probes, screen config).
            // They never affect our display grid; consume silently. Their lowercase result-expectations
            // (output/encout/...) are counted as skipped in HandleExpectation.
            case "UTF8":
            case "WANTPARSER":
            case "WANTSTATE":
            case "WANTSCREEN":
            case "WANTENCODING":
            case "ENCIN":
            case "INCHAR":
            case "INKEY":
            case "PASTE":
            case "MOUSEMOVE":
            case "MOUSEBTN":
            case "FOCUS":
            case "SELECTION":
            case "DAMAGEMERGE":
            case "DAMAGEFLUSH":
            case "SETDEFAULTCOL":
                break;
            default:
                break; // unknown command verb: ignore
        }
    }

    // ---- expectations (lowercase) ----

    private static void HandleExpectation(string line, Ctx ctx)
    {
        if (line.StartsWith("movecursor", StringComparison.Ordinal))
        {
            var nums = ParseInts(line["movecursor".Length..]);
            if (nums.Count >= 2) ctx.PendingCursor = (nums[0], nums[1]); // deferred until block end
            return;
        }
        // putglyph / scrollrect / moverect / premove / erase / damage / settermprop / sb_* / text /
        // control / escape / csi / osc / dcs / apc / pm / sos / output / encout / selection-* …
        ctx.Result.Skipped++;
    }

    // ---- queries (?) ----

    private static void HandleQuery(string line, Ctx ctx)
    {
        var eq = line.IndexOf('=');
        if (eq < 0) { ctx.Result.Skipped++; return; }

        var lhs = line[1..eq].Trim();              // drop leading '?'
        var rhs = line[(eq + 1)..].Trim();
        var sp = lhs.IndexOf(' ');
        var verb = sp < 0 ? lhs : lhs[..sp];
        var args = sp < 0 ? "" : lhs[(sp + 1)..];

        switch (verb)
        {
            case "cursor": AssertCursor(rhs, ctx); return;
            case "pen": AssertPen(args, rhs, ctx); return;
            case "screen_row":
            {
                var nums = ParseInts(args);
                if (nums.Count < 1) { ctx.Result.Skipped++; return; }
                var row = nums[0];
                AssertText(row, 0, row + 1, ctx.Cols, rhs, $"screen_row {row}", ctx, utf8Bytes: false);
                return;
            }
            case "screen_chars":
            case "screen_text":
            {
                var nums = ParseInts(args);
                int sr, sc, er, ec;
                if (nums.Count >= 4) { sr = nums[0]; sc = nums[1]; er = nums[2]; ec = nums[3]; }
                else if (nums.Count == 1) { sr = nums[0]; sc = 0; er = sr + 1; ec = ctx.Cols; } // screen_chars row form
                else { ctx.Result.Skipped++; return; }
                // screen_text encodes the expected as UTF-8 bytes; screen_chars/screen_row use codepoints.
                AssertText(sr, sc, er, ec, rhs, $"{verb} {args}", ctx, utf8Bytes: verb == "screen_text");
                return;
            }
            // ?pen / ?lineinfo / ?screen_cell / ?screen_eol / ?screen_attrs_extent — not yet modelled.
            default: ctx.Result.Skipped++; return;
        }
    }

    private static void AssertPen(string attr, string rhs, Ctx ctx)
    {
        var s = ctx.Screen;
        if (s is null) { ctx.Result.Skipped++; return; }
        // We grade the boolean/level pen attributes we model; color/font/small/baseline are not graded.
        var actual = attr switch
        {
            "bold" => s.PenBold ? "on" : "off",
            "italic" => s.PenItalic ? "on" : "off",
            "blink" => s.PenBlink ? "on" : "off",
            "reverse" => s.PenReverse ? "on" : "off",
            "underline" => s.PenUnderline.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
        if (actual is null) { ctx.Result.Skipped++; return; }
        if (actual == rhs.Trim()) ctx.Result.Passed++;
        else ctx.Fail($"?pen {attr} expected {rhs.Trim()} actual {actual}");
    }

    private static void AssertCursor(string rhs, Ctx ctx)
    {
        var snap = ctx.Snapshot();
        var nums = ParseInts(rhs);
        if (nums.Count < 2) { ctx.Result.Skipped++; return; }
        var (wantRow, wantCol) = (nums[0], nums[1]);
        if (snap.CursorY == wantRow && snap.CursorX == wantCol)
            ctx.Result.Passed++;
        else
            ctx.Fail($"?cursor expected {wantRow},{wantCol} actual {snap.CursorY},{snap.CursorX}");
    }

    private static void AssertText(int sr, int sc, int er, int ec, string rhs, string label, Ctx ctx, bool utf8Bytes)
    {
        var expected = ParseExpectedText(rhs, utf8Bytes);
        var actual = BuildText(ctx.Snapshot(), sr, sc, er, ec);
        if (actual == expected)
            ctx.Result.Passed++;
        else
            ctx.Fail($"?{label} expected {Quote(expected)} actual {Quote(actual)}");
    }

    /// <summary>Renders cells [sr,er) × [sc,ec) to text: blank cells become spaces, wide-char
    /// continuation cells are dropped, each row is trailing-trimmed, rows joined by '\n'.</summary>
    private static string BuildText(TerminalContent c, int sr, int sc, int er, int ec)
    {
        sr = Math.Clamp(sr, 0, c.Rows); er = Math.Clamp(er, 0, c.Rows);
        var rows = new List<string>();
        for (var r = sr; r < er; r++)
        {
            var sb = new StringBuilder();
            var lo = Math.Clamp(sc, 0, c.Cols);
            var hi = Math.Clamp(ec, 0, c.Cols);
            for (var col = lo; col < hi; col++)
            {
                var ch = c.Cells[r][col].Character;
                if (ch.Length == 0) continue; // continuation of a wide char
                sb.Append(ch);
            }
            rows.Add(sb.ToString().TrimEnd(' '));
        }
        return string.Join("\n", rows);
    }

    // ---- $SEQ / $REP expansion ----

    private static void HandleDollar(string line, Ctx ctx)
    {
        if (line.StartsWith("$SEQ", StringComparison.Ordinal))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) { ctx.Result.Skipped++; return; }
            var header = ParseInts(line[..colon]);          // $SEQ low high
            var body = line[(colon + 1)..].TrimStart();
            if (header.Count < 2) { ctx.Result.Skipped++; return; }
            for (var v = header[0]; v <= header[1]; v++)
                DoLine(body.Replace("\\#", v.ToString(CultureInfo.InvariantCulture)).TrimStart(), ctx);
            return;
        }
        if (line.StartsWith("$REP", StringComparison.Ordinal))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) { ctx.Result.Skipped++; return; }
            var header = ParseInts(line[..colon]);          // $REP count
            var body = line[(colon + 1)..].TrimStart();
            if (header.Count < 1) { ctx.Result.Skipped++; return; }
            for (var i = 0; i < header[0]; i++) DoLine(body, ctx);
            return;
        }
        ctx.Result.Skipped++;
    }

    // ---- expected-value parsing ----

    /// <summary>Expected RHS is a Perl string ("…"), a comma list of 0xNN values, or empty. For
    /// screen_text the 0xNN values are UTF-8 bytes; for screen_chars/screen_row they are codepoints.</summary>
    private static string ParseExpectedText(string rhs, bool utf8Bytes)
    {
        rhs = rhs.Trim();
        if (rhs.Length == 0) return "";
        if (rhs[0] == '"') return PerlBytes.DecodeToString(rhs);

        var values = new List<int>();
        foreach (var tokRaw in rhs.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var tok = tokRaw.Trim();
            if (tok.Length == 0) continue;
            values.Add(tok.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.Parse(tok[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : int.Parse(tok, CultureInfo.InvariantCulture));
        }

        if (utf8Bytes)
            return Encoding.UTF8.GetString(values.Select(v => (byte)v).ToArray());

        var sb = new StringBuilder();
        foreach (var cp in values) sb.Append(char.ConvertFromUtf32(cp));
        return sb.ToString();
    }

    private static List<int> ParseInts(string s)
    {
        var result = new List<int>();
        var sb = new StringBuilder();
        var neg = false;
        void Flush()
        {
            if (sb.Length > 0) { result.Add((neg ? -1 : 1) * int.Parse(sb.ToString(), CultureInfo.InvariantCulture)); sb.Clear(); }
            neg = false;
        }
        foreach (var ch in s)
        {
            if (char.IsDigit(ch)) sb.Append(ch);
            else if (ch == '-' && sb.Length == 0) neg = true;
            else Flush();
        }
        Flush();
        return result;
    }

    private static string Quote(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\"", "\\\"").Replace("\u001b", "\\e") + "\"";

    private sealed class Ctx(string file)
    {
        public ConformanceResult Result { get; } = new() { File = file };
        public VtScreen? Screen;
        public int Cols = 80;
        public int Rows = 25;
        public string Section = "";
        public int LineNum;
        public (int Row, int Col)? PendingCursor;

        public TerminalContent Snapshot() => (Screen ??= new VtScreen(Cols, Rows)).ToTerminalContent();

        /// <summary>Evaluate the most recent <c>movecursor</c> expectation (deferred so only the final
        /// cursor position of a command block is checked, matching how the engine settles).</summary>
        public void FlushCursor()
        {
            if (PendingCursor is not { } want) return;
            PendingCursor = null;
            var snap = Snapshot();
            if (snap.CursorY == want.Row && snap.CursorX == want.Col)
                Result.Passed++;
            else
                Fail($"movecursor expected {want.Row},{want.Col} actual {snap.CursorY},{snap.CursorX}");
        }

        public void Fail(string message)
        {
            Result.Failed++;
            AddFailure($"[{Section}] line {LineNum}: {message}");
        }

        public void AddFailure(string message)
        {
            if (Result.Failures.Count < FailureSampleCap) Result.Failures.Add(message);
        }
    }
}

/// <summary>Per-file conformance tally.</summary>
public sealed class ConformanceResult
{
    public string File { get; init; } = "";
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> Failures { get; } = new();
    public int Evaluated => Passed + Failed;
    public double PassRate => Evaluated == 0 ? 0 : (double)Passed / Evaluated;
}

/// <summary>Decoder for the Perl double-quoted byte/string literals used in libvterm test files.</summary>
internal static class PerlBytes
{
    /// <summary>Evaluates a Perl string expression as upstream's run-test.pl does: one or more quoted
    /// literals joined by '.', each optionally repeated with 'xN' (e.g. <c>"\n"x24</c>, <c>"a"."b"x3</c>).</summary>
    public static byte[] Decode(string expr)
    {
        var s = expr.Trim();
        var bytes = new List<byte>();
        var i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) break;
            if (s[i] != '"' && s[i] != '\'') break; // not a string term — stop

            var quote = s[i++];
            var content = new StringBuilder();
            while (i < s.Length && s[i] != quote)
            {
                if (s[i] == '\\' && i + 1 < s.Length) { content.Append(s[i]); content.Append(s[i + 1]); i += 2; }
                else content.Append(s[i++]);
            }
            if (i < s.Length) i++; // closing quote

            var term = DecodeContent(content.ToString());

            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i < s.Length && s[i] == 'x') // repetition: "..."xN
            {
                i++;
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                var n = 0; var has = false;
                while (i < s.Length && char.IsDigit(s[i])) { n = n * 10 + (s[i] - '0'); i++; has = true; }
                if (has) { var rep = new List<byte>(n * term.Length); for (var k = 0; k < n; k++) rep.AddRange(term); term = rep.ToArray(); }
            }

            bytes.AddRange(term);

            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i < s.Length && s[i] == '.') i++; // concatenation
        }
        return bytes.ToArray();
    }

    /// <summary>Decodes the escape sequences inside a single (already unquoted) Perl string literal.</summary>
    private static byte[] DecodeContent(string s)
    {
        var bytes = new List<byte>(s.Length);
        for (var i = 0; i < s.Length;)
        {
            var ch = s[i];
            if (ch != '\\') { AppendUtf8(bytes, char.ConvertToUtf32(s, i)); i += char.IsHighSurrogate(ch) ? 2 : 1; continue; }

            i++; // consume backslash
            if (i >= s.Length) { bytes.Add((byte)'\\'); break; }
            var e = s[i++];
            switch (e)
            {
                case 'e': bytes.Add(0x1b); break;
                case 'a': bytes.Add(0x07); break;
                case 'b': bytes.Add(0x08); break;
                case 't': bytes.Add(0x09); break;
                case 'n': bytes.Add(0x0a); break;
                case 'f': bytes.Add(0x0c); break;
                case 'r': bytes.Add(0x0d); break;
                case '\\': bytes.Add((byte)'\\'); break;
                case '"': bytes.Add((byte)'"'); break;
                case '\'': bytes.Add((byte)'\''); break;
                case 'x':
                {
                    if (i < s.Length && s[i] == '{')
                    {
                        var close = s.IndexOf('}', i);
                        var hex = s[(i + 1)..close];
                        i = close + 1;
                        AppendUtf8(bytes, int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        var hex = TakeHex(s, ref i, 2);
                        bytes.Add((byte)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    }
                    break;
                }
                case >= '0' and <= '7':
                {
                    var oct = e.ToString();
                    while (oct.Length < 3 && i < s.Length && s[i] is >= '0' and <= '7') oct += s[i++];
                    bytes.Add((byte)Convert.ToInt32(oct, 8));
                    break;
                }
                default: AppendUtf8(bytes, e); break;
            }
        }
        return bytes.ToArray();
    }

    public static string DecodeToString(string literal) => Encoding.UTF8.GetString(Decode(literal));

    private static string TakeHex(string s, ref int i, int max)
    {
        var start = i;
        while (i < s.Length && i - start < max && Uri.IsHexDigit(s[i])) i++;
        return i == start ? "0" : s[start..i];
    }

    private static void AppendUtf8(List<byte> bytes, int codepoint) =>
        bytes.AddRange(Encoding.UTF8.GetBytes(char.ConvertFromUtf32(codepoint)));
}
