using System.Security.Cryptography;
using System.Text;
using VcrSharp.Core.Recording;
using VcrSharp.Core.Rendering;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Recording;

namespace VcrSharp.Infrastructure.Rendering.Encoders;

/// <summary>
/// Encoder that renders SVG output with text-based animation.
/// Follows AgentStation/vHS approach: text rendered as SVG elements, consecutive frame deduplication, CSS animations.
/// See - https://github.com/agentstation/vhs/blob/main/svg.go
///
/// Uses consecutive frame deduplication (not global) to preserve animation quality while reducing file size.
/// This works surprisingly well, but still rough around the edges especially with the cursor.
/// </summary>
public class SvgEncoder(SessionOptions options, FrameStorage storage) : EncoderBase(options, storage)
{
    // State management for consecutive frame deduplication
    private readonly List<TerminalState> _uniqueStates = [];
    private readonly List<KeyframeStop> _timeline = [];
    private string? _previousHash;

    public override bool SupportsPath(string outputPath)
    {
        return Path.GetExtension(outputPath).Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<string> RenderAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateFramesExist();

        progress?.Report("Loading terminal content snapshots...");

        // Get terminal content snapshots
        var allSnapshots = GetTerminalSnapshots();
        var frameMetadata = GetFrameMetadata();

        if (allSnapshots.Count == 0)
        {
            throw new InvalidOperationException("No terminal content snapshots found. SVG encoder requires terminal content capture during recording.");
        }

        // If frames were trimmed, filter snapshots to only those that were kept
        // TrimmedFirstFrame and TrimmedLastFrame are the ORIGINAL frame numbers (before renumbering)
        List<TerminalContentSnapshot> snapshots;
        if (Options.TrimmedFirstFrame.HasValue && Options.TrimmedLastFrame.HasValue)
        {
            // Filter to only snapshots within the trimmed range
            snapshots = allSnapshots
                .Where(s => s.FrameNumber >= Options.TrimmedFirstFrame.Value &&
                           s.FrameNumber <= Options.TrimmedLastFrame.Value)
                .OrderBy(s => s.Timestamp)
                .ToList();
        }
        else
        {
            // No trimming occurred, use all visible snapshots
            snapshots = allSnapshots
                .Where(s =>
                {
                    var meta = frameMetadata.FirstOrDefault(m => m.FrameNumber == s.FrameNumber);
                    return meta?.IsVisible ?? false;
                })
                .OrderBy(s => s.Timestamp)
                .ToList();
        }

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("No snapshots found after filtering.");
        }

        // Calculate baseline timestamp from first kept frame to adjust all timestamps to start from 0
        var baselineTimestamp = snapshots[0].Timestamp;

        progress?.Report($"Processing {snapshots.Count} frames (trimmed from {allSnapshots.Count})...");

        // Process frames and build timeline with adjusted timestamps
        ProcessFrames(snapshots, frameMetadata, baselineTimestamp);

        progress?.Report($"Consecutive frame deduplication: {snapshots.Count} frames -> {_uniqueStates.Count} unique states");

        // Calculate animation duration (timeline timestamps are already adjusted)
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
    /// <param name="snapshots">Terminal content snapshots (already filtered to visible frames).</param>
    /// <param name="metadata">Frame metadata for all frames.</param>
    /// <param name="baselineTimestamp">Timestamp of the first visible frame to subtract from all timestamps.</param>
    private void ProcessFrames(IReadOnlyList<TerminalContentSnapshot> snapshots, IReadOnlyList<FrameMetadata> metadata, TimeSpan baselineTimestamp)
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

            // Adjust timestamp relative to baseline (first visible frame)
            var adjustedTimestamp = snapshot.Timestamp - baselineTimestamp;

            // Cursor idle detection (using adjusted timestamp)
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
                        cursorIdleStartTime = adjustedTimestamp;
                    }
                    else
                    {
                        // Check if idle for > 0.5 seconds
                        var idleDuration = adjustedTimestamp - cursorIdleStartTime.Value;
                        if (idleDuration.TotalSeconds > 0.5)
                        {
                            isCursorIdle = true;
                        }
                    }
                }
            }

            // Compute hash for consecutive frame deduplication
            var hash = ComputeFrameHash(content, isCursorIdle, Options.DisableCursor);

            // Only deduplicate consecutive identical frames (not all identical frames globally)
            int stateIndex;
            if (_previousHash == hash && _uniqueStates.Count > 0)
            {
                // Same as previous frame - reuse last state
                stateIndex = _uniqueStates.Count - 1;
            }
            else
            {
                // Different from previous frame - create new state
                stateIndex = _uniqueStates.Count;
                _uniqueStates.Add(new TerminalState
                {
                    Content = content,
                    IsCursorIdle = isCursorIdle
                });
                _previousHash = hash;
            }

            // Add to timeline with adjusted timestamp
            var snapshot1 = snapshot;
            var frameMeta = metadata.FirstOrDefault(m => m.FrameNumber == snapshot1.FrameNumber);
            if (frameMeta is { IsVisible: true })
            {
                _timeline.Add(new KeyframeStop
                {
                    Timestamp = adjustedTimestamp,
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
    private static string ComputeFrameHash(TerminalContent content, bool isCursorIdle, bool disableCursor)
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

        // Only include cursor data in hash if cursor will be rendered
        if (!disableCursor)
        {
            sb.Append($"{content.CursorX},{content.CursorY},{content.CursorVisible},{isCursorIdle}");
        }

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
