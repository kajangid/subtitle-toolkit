using System.Text;
using SubtitleToolkit.Model;

namespace SubtitleToolkit.IO;

/// <summary>
/// Controls parser behavior on invalid inputs or malformed cues.
/// </summary>
public enum ParseMode
{
    /// <summary>
    /// Recovers from malformed lines, missing IDs, or bad timestamps, logging warnings in diagnostics.
    /// </summary>
    Lenient,

    /// <summary>
    /// Throws SubtitleParseException on the first malformed cue or unsupported syntax.
    /// </summary>
    Strict
}

/// <summary>
/// Options for configuring subtitle parsing and decoding.
/// </summary>
public sealed class SubtitleReadOptions
{
    /// <summary>Parsing mode: Lenient (default) or Strict.</summary>
    public ParseMode Mode { get; set; } = ParseMode.Lenient;

    /// <summary>
    /// Fallback encoding to use when input stream has no BOM and strict UTF-8 fails.
    /// If null, defaults to ISO-8859-1 (Latin-1) to avoid silent byte corruption.
    /// </summary>
    public Encoding? FallbackEncoding { get; set; }

    /// <summary>
    /// Explicit format override. If null, format is automatically detected from stream/file contents.
    /// </summary>
    public SubtitleFormat? Format { get; set; }
}
