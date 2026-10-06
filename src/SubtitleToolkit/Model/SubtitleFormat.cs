namespace SubtitleToolkit.Model;

/// <summary>
/// Supported subtitle file formats.
/// </summary>
public enum SubtitleFormat
{
    /// <summary>SubRip subtitle format (.srt).</summary>
    SubRip,

    /// <summary>W3C WebVTT format (.vtt).</summary>
    WebVtt,

    /// <summary>Advanced SubStation Alpha v4.00+ format (.ass).</summary>
    Ass,

    /// <summary>SubStation Alpha v4.00 format (.ssa).</summary>
    Ssa
}
