namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Abstract base class for format-specific cue data.
/// </summary>
public abstract class CueFormatData
{
    /// <summary>The subtitle format this cue data belongs to.</summary>
    public abstract SubtitleFormat Format { get; }
}

/// <summary>
/// Abstract base class for format-specific document-level metadata and sections.
/// </summary>
public abstract class DocumentFormatData
{
    /// <summary>The subtitle format this document data belongs to.</summary>
    public abstract SubtitleFormat Format { get; }
}
