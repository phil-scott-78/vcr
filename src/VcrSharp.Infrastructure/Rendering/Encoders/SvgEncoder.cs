using System.Security.Cryptography;
using System.Text;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that renders SVG output with text-based animation.
/// Follows AgentStation/vHS approach: text rendered as SVG elements, frame deduplication, CSS animations.
/// See - https://github.com/agentstation/vhs/blob/main/svg.go
///
/// This works surprisingly well, but still rough around the edges especially with the cursor.
/// </summary>
public class SvgEncoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    // State management for deduplication
    private readonly Dictionary<string, int> _stateHashes = new();
    private readonly List<TerminalState> _uniqueStates = [];
    private readonly List<KeyframeStop> _timeline = [];

    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();

        progress?.Report("Loading terminal content snapshots...");

        // Get terminal content snapshots
        var snapshots = GetTerminalSnapshots();
        var frameMetadata = GetFrameMetadata();

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("No terminal content snapshots found. SVG encoder requires terminal content capture during recording.");
        }

        progress?.Report($"Processing {snapshots.Count} frames...");

        // Process frames and build timeline
        ProcessFrames(snapshots, frameMetadata);

        progress?.Report($"Deduplication: {snapshots.Count} frames -> {_uniqueStates.Count} unique states");

        // Calculate animation duration
        var totalDuration = _timeline.Count > 0 ? _timeline[^1].Timestamp.TotalSeconds / Options.PlaybackSpeed : 1.0;

        // Convert to format for SvgRenderer
        var statesWithTime = _uniqueStates.Select((state, index) =>
        {
            // Find first timeline entry for this state
            var firstAppearance = _timeline.FirstOrDefault(t => t.StateIndex == index);
            var timestampSeconds = firstAppearance != null
                ? firstAppearance.Timestamp.TotalSeconds / Options.PlaybackSpeed
                : 0.0;

            return new TerminalStateWithTime
            {
                Content = state.Content,
                TimestampSeconds = timestampSeconds,
                IsCursorIdle = state.IsCursorIdle
            };
        }).ToList();

        // Generate SVG
        progress?.Report("Generating SVG...");
        var renderer = new SvgRenderer(Options);
        await renderer.RenderAnimatedAsync(outputPath, statesWithTime, totalDuration, cancellationToken);

        progress?.Report($"SVG exported to {outputPath}");

        return outputPath;
    }

    /// <summary>
    /// Processes frames, performs deduplication, and builds animation timeline.
    /// </summary>
    private void ProcessFrames(IReadOnlyList<TerminalContentSnapshot> snapshots, IReadOnlyList<FrameMetadata> metadata)
    {
        TerminalContent? previousContent = null;
        int? previousCursorX = null;
        int? previousCursorY = null;
        TimeSpan? cursorIdleStartTime = null;

        foreach (var snapshot in snapshots)
        {
            var content = snapshot.Content;

            // Skip null, empty, or invalid content
            if (content == null || content.Rows == 0 || content.Cols == 0 || content.Cells.Length == 0)
                continue;

            // Cursor idle detection
            var isCursorIdle = false;
            if (previousContent != null)
            {
                // Check if cursor moved or text changed at cursor position
                var cursorMoved = content.CursorX != previousCursorX || content.CursorY != previousCursorY;
                var textChanged = HasTextChangedAtCursor(previousContent, content);

                if (cursorMoved || textChanged)
                {
                    // Cursor activity - reset idle timer
                    cursorIdleStartTime = null;
                }
                else
                {
                    // Cursor stationary
                    if (cursorIdleStartTime == null)
                    {
                        cursorIdleStartTime = snapshot.Timestamp;
                    }
                    else
                    {
                        // Check if idle for > 0.5 seconds
                        var idleDuration = snapshot.Timestamp - cursorIdleStartTime.Value;
                        if (idleDuration.TotalSeconds > 0.5)
                        {
                            isCursorIdle = true;
                        }
                    }
                }
            }

            // Compute hash for deduplication
            var hash = ComputeFrameHash(content, isCursorIdle);

            // Check if this state already exists
            if (!_stateHashes.TryGetValue(hash, out var stateIndex))
            {
                // New unique state
                stateIndex = _uniqueStates.Count;
                _stateHashes[hash] = stateIndex;
                _uniqueStates.Add(new TerminalState
                {
                    Content = content,
                    IsCursorIdle = isCursorIdle
                });
            }

            // Add to timeline
            var snapshot1 = snapshot;
            var frameMeta = metadata.FirstOrDefault(m => m.FrameNumber == snapshot1.FrameNumber);
            if (frameMeta is { IsVisible: true })
            {
                _timeline.Add(new KeyframeStop
                {
                    Timestamp = snapshot.Timestamp,
                    StateIndex = stateIndex
                });
            }

            previousContent = content;
            previousCursorX = content.CursorX;
            previousCursorY = content.CursorY;
        }
    }

    /// <summary>
    /// Checks if text changed at the cursor position.
    /// </summary>
    private static bool HasTextChangedAtCursor(TerminalContent prev, TerminalContent curr)
    {
        if (prev.CursorY >= prev.Rows || curr.CursorY >= curr.Rows)
            return false;

        if (prev.CursorX >= prev.Cols || curr.CursorX >= curr.Cols)
            return false;

        var prevCell = prev.Cells[prev.CursorY][prev.CursorX];
        var currCell = curr.Cells[curr.CursorY][curr.CursorX];

        return prevCell.Character != currCell.Character;
    }


    /// <summary>
    /// Computes MD5 hash of terminal state for frame deduplication.
    /// </summary>
    private static string ComputeFrameHash(TerminalContent content, bool isCursorIdle)
    {
        var sb = new StringBuilder();

        for (var row = 0; row < content.Rows; row++)
        {
            for (var col = 0; col < content.Cols; col++)
            {
                var cell = content.Cells[row][col];
                sb.Append(cell.Character);
                sb.Append(cell.ForegroundColor ?? "");
                sb.Append(cell.BackgroundColor ?? "");
                sb.Append(cell.IsBold ? "b" : "");
                sb.Append(cell.IsItalic ? "i" : "");
                sb.Append(cell.IsUnderline ? "u" : "");
            }
            sb.AppendLine();
        }

        sb.Append($"{content.CursorX},{content.CursorY},{content.CursorVisible},{isCursorIdle}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Represents a unique terminal state.
    /// </summary>
    private sealed class TerminalState
    {
        public required TerminalContent Content { get; init; }
        public bool IsCursorIdle { get; init; }
    }

    /// <summary>
    /// Represents a keyframe in the animation timeline.
    /// </summary>
    private sealed class KeyframeStop
    {
        public required TimeSpan Timestamp { get; init; }
        public required int StateIndex { get; init; }
    }
}
