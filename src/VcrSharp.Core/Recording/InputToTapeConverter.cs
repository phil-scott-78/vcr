using System.Globalization;
using System.Text;
using VcrSharp.Core.Session;

namespace VcrSharp.Core.Recording;

/// <summary>
/// Converts a captured stream of terminal input events into <c>.tape</c> file text.
/// <para>
/// This is a pure function (no I/O, no clock, no Playwright) so it is trivially unit-testable.
/// It is a port of VHS's <c>inputToTape</c>, improved in two ways:
/// </para>
/// <list type="bullet">
///   <item>It uses the precise per-keystroke timestamps captured from xterm.js to emit
///   <c>Sleep</c> commands that reflect the user's real pauses, instead of VHS's fixed 500ms heuristic.</item>
///   <item>It is shell-agnostic: it only interprets the input byte-stream (identical across pwsh,
///   cmd, bash, zsh, fish), never shell output, so it is not limited to bash like VHS.</item>
/// </list>
/// </summary>
public static class InputToTapeConverter
{
    /// <summary>
    /// Converts captured input events to tape text.
    /// </summary>
    /// <param name="events">The captured input events, in arrival order.</param>
    /// <param name="options">Conversion options. Defaults are used when null.</param>
    /// <returns>Valid <c>.tape</c> text (header <c>Set</c> commands followed by action commands).</returns>
    public static string Convert(IReadOnlyList<InputEvent> events, InputToTapeOptions? options = null)
    {
        options ??= new InputToTapeOptions();

        var atoms = Tokenize(events);
        atoms = StripQueryResponses(atoms);
        if (options.StripExit)
        {
            atoms = StripTrailingExit(atoms);
        }

        var lines = new List<string>();
        lines.AddRange(BuildHeader(options));

        var body = BuildBody(atoms, options);

        // Separate header and body with a blank line for readability (only when both exist).
        if (lines.Count > 0 && body.Count > 0)
        {
            lines.Add(string.Empty);
        }
        lines.AddRange(body);

        return string.Join("\n", lines);
    }

    // ---- Atom model -------------------------------------------------------

    private enum AtomKind { Printable, Key, Modifier, Paste }

    private readonly record struct Atom(
        AtomKind Kind,
        string Text,           // Printable: the char(s); Key: key name; Paste: pasted text; Modifier: base key
        bool Ctrl,
        bool Alt,
        bool Shift,
        TimeSpan Time);

    private static Atom Printable(string text, TimeSpan t) => new(AtomKind.Printable, text, false, false, false, t);
    private static Atom Key(string name, TimeSpan t) => new(AtomKind.Key, name, false, false, false, t);
    private static Atom Modifier(string key, bool ctrl, bool alt, bool shift, TimeSpan t)
        => new(AtomKind.Modifier, key, ctrl, alt, shift, t);
    private static Atom Paste(string text, TimeSpan t) => new(AtomKind.Paste, text, false, false, false, t);

    // ---- Tokenization -----------------------------------------------------

    private static List<Atom> Tokenize(IReadOnlyList<InputEvent> events)
    {
        // Flatten into a single scan string with parallel per-character timestamp + event-index maps.
        // Concatenation naturally reassembles escape sequences split across events (e.g. ESC then "[A").
        var sb = new StringBuilder();
        var times = new List<TimeSpan>();
        var eventIndex = new List<int>();
        for (var e = 0; e < events.Count; e++)
        {
            var data = events[e].Data;
            foreach (var ch in data)
            {
                sb.Append(ch);
                times.Add(events[e].Timestamp);
                eventIndex.Add(e);
            }
        }

        var input = sb.ToString();
        var atoms = new List<Atom>();
        var pos = 0;

        while (pos < input.Length)
        {
            var t = times[pos];
            var c = input[pos];

            // Escape-introduced sequences.
            if (c == '\x1b')
            {
                // Bracketed paste: ESC [ 200 ~ ... ESC [ 201 ~
                if (StartsWith(input, pos, "\x1b[200~"))
                {
                    var contentStart = pos + 6;
                    var closeIdx = input.IndexOf("\x1b[201~", contentStart, StringComparison.Ordinal);
                    if (closeIdx >= 0)
                    {
                        var pasted = input.Substring(contentStart, closeIdx - contentStart);
                        if (pasted.Length > 0)
                        {
                            atoms.Add(Paste(pasted, t));
                        }
                        pos = closeIdx + 6;
                        continue;
                    }
                    // No closing marker: skip the opening marker and continue scanning.
                    pos += 6;
                    continue;
                }

                // CSI: ESC [ <params> <final>
                if (pos + 1 < input.Length && input[pos + 1] == '[')
                {
                    var consumed = TryReadCsi(input, pos, t, out var csiAtom);
                    if (consumed > 0)
                    {
                        if (csiAtom.HasValue)
                        {
                            atoms.Add(csiAtom.Value);
                        }
                        pos += consumed;
                        continue;
                    }
                    // Malformed CSI: fall through to treat ESC as lone Escape.
                }
                // SS3: ESC O <final>  (application cursor key mode)
                else if (pos + 2 < input.Length && input[pos + 1] == 'O')
                {
                    var final = input[pos + 2];
                    var key = MapCursorFinal(final);
                    if (key != null)
                    {
                        atoms.Add(Key(key, t));
                    }
                    pos += 3;
                    continue;
                }
                // Alt+<char>: ESC immediately followed by a printable char, from the SAME event.
                // The same-event check distinguishes a real Alt combo (sent atomically) from the user
                // pressing Escape and then typing a character (delivered as two separate events).
                else if (pos + 1 < input.Length
                         && IsPrintable(input[pos + 1])
                         && input[pos + 1] != '['
                         && input[pos + 1] != 'O'
                         && eventIndex[pos] == eventIndex[pos + 1])
                {
                    atoms.Add(Modifier(input[pos + 1].ToString(), ctrl: false, alt: true, shift: false, t));
                    pos += 2;
                    continue;
                }

                // Lone Escape.
                atoms.Add(Key("Escape", t));
                pos += 1;
                continue;
            }

            // Control codes and DEL.
            if (c < ' ' || c == '\x7f')
            {
                var ctrlAtom = MapControl(c, t);
                if (ctrlAtom.HasValue)
                {
                    atoms.Add(ctrlAtom.Value);
                }
                pos += 1;
                continue;
            }

            // Printable character.
            atoms.Add(Printable(c.ToString(), t));
            pos += 1;
        }

        return atoms;
    }

    /// <summary>
    /// Reads a CSI sequence starting at <paramref name="pos"/> (which points at the ESC).
    /// Returns the number of characters consumed (0 if malformed/incomplete).
    /// </summary>
    private static int TryReadCsi(string input, int pos, TimeSpan t, out Atom? atom)
    {
        atom = null;

        // pos -> ESC, pos+1 -> '['
        var i = pos + 2;
        var paramStart = i;
        while (i < input.Length && (char.IsAsciiDigit(input[i]) || input[i] == ';'))
        {
            i++;
        }
        if (i >= input.Length)
        {
            return 0; // incomplete
        }

        var final = input[i];
        var parameters = input.Substring(paramStart, i - paramStart);
        var consumed = (i - pos) + 1;

        // Decode modifier (CSI uses "<code>;<m>" where m-1 is a bitmask: 1=Shift, 2=Alt, 4=Ctrl).
        var mod = 0;
        var semi = parameters.IndexOf(';');
        var codePart = semi >= 0 ? parameters[..semi] : parameters;
        if (semi >= 0 && int.TryParse(parameters[(semi + 1)..], out var m))
        {
            mod = m - 1;
        }

        string? key = final switch
        {
            'A' or 'B' or 'C' or 'D' or 'H' or 'F' => MapCursorFinal(final),
            'Z' => "Tab", // back-tab (Shift+Tab); shift applied below
            '~' => MapTildeCode(codePart),
            _ => null
        };

        if (final == 'Z')
        {
            atom = Modifier("Tab", ctrl: false, alt: false, shift: true, t);
            return consumed;
        }

        if (key == null)
        {
            // Unknown / unmappable (function keys, query responses, etc.) — drop it.
            return consumed;
        }

        if (mod > 0)
        {
            var hasShift = (mod & 1) != 0;
            var hasAlt = (mod & 2) != 0;
            var hasCtrl = (mod & 4) != 0;
            atom = Modifier(key, hasCtrl, hasAlt, hasShift, t);
        }
        else
        {
            atom = Key(key, t);
        }

        return consumed;
    }

    private static string? MapCursorFinal(char final) => final switch
    {
        'A' => "Up",
        'B' => "Down",
        'C' => "Right",
        'D' => "Left",
        'H' => "Home",
        'F' => "End",
        _ => null
    };

    private static string? MapTildeCode(string code) => code switch
    {
        "1" or "7" => "Home",
        "2" => "Insert",
        "3" => "Delete",
        "4" or "8" => "End",
        "5" => "PageUp",
        "6" => "PageDown",
        _ => null // F-keys (11..24) and others have no tape command — drop.
    };

    private static Atom? MapControl(char c, TimeSpan t)
    {
        switch (c)
        {
            case '\x7f': // DEL
            case '\x08': // BS
                return Key("Backspace", t);
            case '\t': // 0x09
                return Key("Tab", t);
            case '\n': // 0x0a
            case '\r': // 0x0d
                return Key("Enter", t);
            case '\x00':
            case '\x1c':
            case '\x1d':
            case '\x1e':
            case '\x1f':
                return null; // no representable tape command
            default:
                // 0x01..0x1a (excluding the special cases above) -> Ctrl+<letter>.
                if (c >= '\x01' && c <= '\x1a')
                {
                    var letter = (char)('A' + (c - 1));
                    return Modifier(letter.ToString(), ctrl: true, alt: false, shift: false, t);
                }
                return null;
        }
    }

    // ---- Cleanup passes ---------------------------------------------------

    /// <summary>
    /// Drops terminal query responses (DSR cursor-position / DA replies). A user never types these;
    /// they can only appear if an application queried the terminal during the session.
    /// </summary>
    private static List<Atom> StripQueryResponses(List<Atom> atoms)
    {
        // Query responses are already dropped during CSI tokenization (unknown finals such as 'R'/'c'
        // map to null). This pass is a no-op placeholder kept for clarity/extension.
        return atoms;
    }

    /// <summary>
    /// Removes a trailing shell-exit sequence used to end the recording session:
    /// either <c>exit</c> (case-insensitive, surrounding spaces allowed) followed by Enter, or Ctrl+D.
    /// </summary>
    private static List<Atom> StripTrailingExit(List<Atom> atoms)
    {
        if (atoms.Count == 0)
        {
            return atoms;
        }

        var last = atoms[^1];

        // Ctrl+D (EOF).
        if (last is { Kind: AtomKind.Modifier, Ctrl: true, Alt: false, Shift: false }
            && string.Equals(last.Text, "D", StringComparison.Ordinal))
        {
            return atoms.GetRange(0, atoms.Count - 1);
        }

        // "exit" + Enter.
        if (last is { Kind: AtomKind.Key } && last.Text == "Enter")
        {
            var j = atoms.Count - 2; // index just before the Enter
            var runEnd = j;
            while (j >= 0 && atoms[j].Kind == AtomKind.Printable)
            {
                j--;
            }
            // Printable run is atoms[(j+1)..=runEnd].
            if (runEnd > j)
            {
                var runText = string.Concat(
                    atoms.GetRange(j + 1, runEnd - j).Select(a => a.Text));
                if (runText.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    return atoms.GetRange(0, j + 1);
                }
            }
        }

        return atoms;
    }

    // ---- Emission ---------------------------------------------------------

    private static List<string> BuildHeader(InputToTapeOptions options)
    {
        var header = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.Shell)
            && !string.Equals(options.Shell, options.DefaultShell, StringComparison.OrdinalIgnoreCase))
        {
            header.Add($"Set Shell \"{options.Shell}\"");
        }

        if (options.Header is { } h)
        {
            var def = new SessionOptions();

            if (h.Cols.HasValue)
            {
                header.Add($"Set Cols {h.Cols.Value.ToString(CultureInfo.InvariantCulture)}");
            }
            if (h.Rows.HasValue)
            {
                header.Add($"Set Rows {h.Rows.Value.ToString(CultureInfo.InvariantCulture)}");
            }
            if (h.FontSize != def.FontSize)
            {
                header.Add($"Set FontSize {h.FontSize.ToString(CultureInfo.InvariantCulture)}");
            }
            if (h.Theme is { } theme
                && !string.Equals(theme.Name, def.Theme.Name, StringComparison.OrdinalIgnoreCase))
            {
                header.Add($"Set Theme \"{theme.Name}\"");
            }
        }

        return header;
    }

    private static List<string> BuildBody(List<Atom> atoms, InputToTapeOptions options)
    {
        var lines = new List<string>();
        var pendingType = new StringBuilder();
        string? pendingKey = null;
        var pendingKeyCount = 0;

        void FlushType()
        {
            if (pendingType.Length > 0)
            {
                lines.Add($"Type \"{EscapeForTypeLiteral(pendingType.ToString())}\"");
                pendingType.Clear();
            }
        }

        void FlushKey()
        {
            if (pendingKey != null)
            {
                lines.Add(pendingKeyCount > 1 ? $"{pendingKey} {pendingKeyCount}" : pendingKey);
                pendingKey = null;
                pendingKeyCount = 0;
            }
        }

        for (var idx = 0; idx < atoms.Count; idx++)
        {
            var atom = atoms[idx];

            // Insert a Sleep when the user paused before this atom.
            if (idx > 0)
            {
                var gap = atom.Time - atoms[idx - 1].Time;
                if (gap >= options.SleepThreshold)
                {
                    FlushType();
                    FlushKey();
                    lines.Add($"Sleep {FormatSleep(gap, options)}");
                }
            }

            switch (atom.Kind)
            {
                case AtomKind.Printable:
                    FlushKey();
                    pendingType.Append(atom.Text);
                    break;

                case AtomKind.Key:
                    FlushType();
                    if (pendingKey == atom.Text)
                    {
                        pendingKeyCount++;
                    }
                    else
                    {
                        FlushKey();
                        pendingKey = atom.Text;
                        pendingKeyCount = 1;
                    }
                    break;

                case AtomKind.Modifier:
                    FlushType();
                    FlushKey();
                    lines.Add(FormatModifier(atom));
                    break;

                case AtomKind.Paste:
                    FlushType();
                    FlushKey();
                    lines.Add($"Type \"{EscapeForTypeLiteral(atom.Text)}\"");
                    break;
            }
        }

        FlushType();
        FlushKey();
        return lines;
    }

    private static string FormatModifier(Atom atom)
    {
        var parts = new List<string>(4);
        if (atom.Ctrl) parts.Add("Ctrl");
        if (atom.Alt) parts.Add("Alt");
        if (atom.Shift) parts.Add("Shift");
        parts.Add(atom.Text);
        return string.Join("+", parts);
    }

    private static string FormatSleep(TimeSpan gap, InputToTapeOptions options)
    {
        var ms = Math.Clamp(gap.TotalMilliseconds, options.MinSleep.TotalMilliseconds, options.MaxSleep.TotalMilliseconds);

        if (ms < 1000)
        {
            var rounded = (int)(Math.Round(ms / 50.0) * 50);
            rounded = Math.Max(rounded, (int)options.MinSleep.TotalMilliseconds);
            return $"{rounded.ToString(CultureInfo.InvariantCulture)}ms";
        }

        var roundedMs = Math.Round(ms / 100.0) * 100;
        var seconds = roundedMs / 1000.0;
        return $"{seconds.ToString("0.#", CultureInfo.InvariantCulture)}s";
    }

    /// <summary>
    /// Escapes text for inclusion inside a double-quoted <c>Type "..."</c> literal, matching the
    /// parser's supported escapes (<c>\n \t \r \\ \"</c>).
    /// </summary>
    private static string EscapeForTypeLiteral(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static bool IsPrintable(char c) => c >= ' ' && c != '\x7f';

    private static bool StartsWith(string s, int pos, string value)
        => pos + value.Length <= s.Length && string.CompareOrdinal(s, pos, value, 0, value.Length) == 0;
}
