using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SubtitleToolkit.Model;

namespace SubtitleToolkit.Diagnostics;

/// <summary>
/// Severity level of a conversion notice.
/// </summary>
public enum ConversionSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Machine-readable diagnostic codes for format conversion notices.
/// </summary>
public static class ConversionNoticeCode
{
    public const string StaleFormatDataDropped = "STALE_FORMAT_DATA_DROPPED";
    public const string StyleTableDropped = "STYLE_TABLE_DROPPED";
    public const string ScriptInfoDropped = "SCRIPT_INFO_DROPPED";
    public const string NonCueBlocksDropped = "NON_CUE_BLOCKS_DROPPED";
    public const string DrawingDropped = "DRAWING_DROPPED";
    public const string UnsupportedTagStripped = "UNSUPPORTED_TAG_STRIPPED";
    public const string PositioningConverted = "POSITIONING_CONVERTED";
    public const string PositioningDropped = "POSITIONING_DROPPED";
    public const string SnapToLinesConverted = "SNAP_TO_LINES_CONVERTED";
    public const string VoiceTagConverted = "VOICE_TAG_CONVERTED";
    public const string AlignmentConverted = "ALIGNMENT_CONVERTED";
}

/// <summary>
/// Represents a structured diagnostic notice produced during format conversion.
/// </summary>
public sealed class ConversionNotice : IEquatable<ConversionNotice>
{
    public ConversionSeverity Severity { get; }
    public string Code { get; }
    public int? CueIndex { get; }
    public string? FeatureName { get; }
    public string Message { get; }

    public ConversionNotice(
        ConversionSeverity severity,
        string code,
        string message,
        int? cueIndex = null,
        string? featureName = null)
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        CueIndex = cueIndex;
        FeatureName = featureName;
    }

    public bool Equals(ConversionNotice? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Severity == other.Severity
            && string.Equals(Code, other.Code, StringComparison.Ordinal)
            && CueIndex == other.CueIndex
            && string.Equals(FeatureName, other.FeatureName, StringComparison.Ordinal)
            && string.Equals(Message, other.Message, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as ConversionNotice);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Severity.GetHashCode();
            hash = (hash * 31) + Code.GetHashCode();
            hash = (hash * 31) + (CueIndex?.GetHashCode() ?? 0);
            hash = (hash * 31) + (FeatureName?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public override string ToString()
    {
        var cuePart = CueIndex.HasValue ? $" [Cue #{CueIndex.Value}]" : string.Empty;
        var featPart = !string.IsNullOrEmpty(FeatureName) ? $" ({FeatureName})" : string.Empty;
        return $"[{Severity}] {Code}{cuePart}{featPart}: {Message}";
    }
}

/// <summary>
/// Audit report summarizing all warnings, notices, and fidelity changes during conversion.
/// </summary>
public sealed class ConversionReport
{
    public IReadOnlyList<ConversionNotice> Notices { get; }

    public bool HasWarnings => Notices.Any(n => n.Severity == ConversionSeverity.Warning);
    public bool HasErrors => Notices.Any(n => n.Severity == ConversionSeverity.Error);
    public bool IsLossless => !HasWarnings && !HasErrors;

    public ConversionReport(IEnumerable<ConversionNotice>? notices = null)
    {
        Notices = new ReadOnlyCollection<ConversionNotice>(
            notices != null ? notices.ToList() : new List<ConversionNotice>());
    }
}

/// <summary>
/// Result of a subtitle format conversion containing the new document and audit report.
/// </summary>
public sealed class ConversionResult
{
    public SubtitleDocument Document { get; }
    public ConversionReport Report { get; }

    public ConversionResult(SubtitleDocument document, ConversionReport report)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public static implicit operator SubtitleDocument(ConversionResult result) => result.Document;
}

/// <summary>
/// Configuration options for subtitle format conversions.
/// </summary>
public sealed class ConversionOptions
{
    /// <summary>
    /// If true, throws <see cref="SubtitleConversionException"/> if any lossy warning occurs during conversion.
    /// Default is false.
    /// </summary>
    public bool ThrowOnLoss { get; set; } = false;

    /// <summary>
    /// Target PlayResX when converting to ASS/SSA (default 384 per libass specification).
    /// </summary>
    public int TargetPlayResX { get; set; } = 384;

    /// <summary>
    /// Target PlayResY when converting to ASS/SSA (default 288 per libass specification).
    /// </summary>
    public int TargetPlayResY { get; set; } = 288;

    /// <summary>
    /// Default style name when creating ASS/SSA cues.
    /// </summary>
    public string DefaultStyleName { get; set; } = "Default";
}

/// <summary>
/// Exception thrown when format conversion fails or when ThrowOnLoss is enabled and loss occurs.
/// </summary>
public sealed class SubtitleConversionException : Exception
{
    public ConversionReport Report { get; }

    public SubtitleConversionException(ConversionReport report, string message) : base(message)
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
    }
}
