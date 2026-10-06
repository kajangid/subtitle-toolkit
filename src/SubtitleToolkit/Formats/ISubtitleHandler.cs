using System.IO;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;

namespace SubtitleToolkit.Formats;

/// <summary>
/// Internal interface implemented by format-specific parsers and writers.
/// </summary>
public interface ISubtitleHandler
{
    /// <summary>The subtitle format handled.</summary>
    SubtitleFormat Format { get; }

    /// <summary>Parses string content into a subtitle document and diagnostics.</summary>
    ParseResult Parse(string content, SubtitleReadOptions options);

    /// <summary>Serializes a subtitle document to the specified TextWriter.</summary>
    void Write(SubtitleDocument doc, TextWriter writer, SubtitleWriteOptions options);
}
