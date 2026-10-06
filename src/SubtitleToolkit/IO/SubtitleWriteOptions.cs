using System.Text;

namespace SubtitleToolkit.IO;

/// <summary>
/// Options for configuring subtitle serialization.
/// </summary>
public sealed class SubtitleWriteOptions
{
    /// <summary>
    /// Line ending string to use when serializing text. Defaults to Windows CRLF ("\r\n").
    /// </summary>
    public string LineEnding { get; set; } = "\r\n";

    /// <summary>
    /// Whether to emit a UTF-8 BOM when saving. Defaults to false.
    /// Note: WebVTT specification strictly mandates UTF-8 without BOM; this option is ignored for WebVTT.
    /// </summary>
    public bool EmitBom { get; set; }

    /// <summary>
    /// Encoding to use when writing streams. Defaults to UTF-8.
    /// </summary>
    public Encoding? Encoding { get; set; }
}
