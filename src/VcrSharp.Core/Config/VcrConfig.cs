namespace VcrSharp.Core.Config;

/// <summary>
/// A project-level configuration discovered from a <c>vcr.toml</c> file. Holds named presets
/// (the shared "house style" pulled in by <c>Use NAME</c>) and macro templates (reusable command
/// strings expanded by <c>Exec NAME arg</c>). One vcr.toml replaces the per-tape copy-pasted
/// Set block across a whole docs directory.
/// </summary>
public sealed class VcrConfig
{
    /// <summary>Presets by name (case-insensitive), pulled in via <c>Use NAME</c>.</summary>
    public Dictionary<string, VcrPreset> Presets { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Macro templates by name (case-insensitive), expanded via <c>Exec NAME [arg]</c>.</summary>
    public Dictionary<string, string> Macros { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The file this config was loaded from, for diagnostics. Null for in-memory configs.</summary>
    public string? SourcePath { get; init; }
}

/// <summary>
/// A named preset: a bundle of setting values (applied as <c>Set</c> defaults), an optional
/// parent it <see cref="Inherits"/> from, and an optional output location used to derive a tape's
/// <c>Output</c> path from its filename.
/// </summary>
public sealed class VcrPreset
{
    public required string Name { get; init; }

    /// <summary>Name of the preset this one inherits from (base-first resolution), or null.</summary>
    public string? Inherits { get; set; }

    /// <summary>
    /// Output directory used to derive <c>Output</c> as <c>{OutDir}/{tapeName}.svg</c> when a tape
    /// has no explicit Output command. Null disables derivation.
    /// </summary>
    public string? OutDir { get; set; }

    /// <summary>
    /// Explicit output template (e.g. <c>"assets/{name}.png"</c>); <c>{name}</c> is the tape
    /// basename. Takes precedence over <see cref="OutDir"/> when both are set.
    /// </summary>
    public string? OutputTemplate { get; set; }

    /// <summary>Setting name → typed value (string / bool / TimeSpan / numeric-string).</summary>
    public Dictionary<string, object> Settings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Source line of the preset's section header, for diagnostics.</summary>
    public int LineNumber { get; set; }
}

/// <summary>Thrown when a <c>vcr.toml</c> file cannot be parsed.</summary>
public sealed class VcrConfigException(string message, int line = 0, string? sourcePath = null)
    : Exception(message)
{
    public int Line { get; } = line;
    public string? SourcePath { get; } = sourcePath;
}
