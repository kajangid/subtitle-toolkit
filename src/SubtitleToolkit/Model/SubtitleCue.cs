using System;
using SubtitleToolkit.Common;
using SubtitleToolkit.Model.FormatData;

namespace SubtitleToolkit.Model;

/// <summary>
/// Represents a single immutable subtitle cue with timing and text.
/// </summary>
public sealed class SubtitleCue : IEquatable<SubtitleCue>
{
    private string? _plainText;

    /// <summary>Cue start timestamp.</summary>
    public TimeSpan Start { get; }

    /// <summary>Cue end timestamp.</summary>
    public TimeSpan End { get; }

    /// <summary>
    /// Canonical raw text payload. Multiline breaks are normalized to '\n'.
    /// </summary>
    public string RawText { get; }

    /// <summary>Format-specific extension data, or null if none.</summary>
    public CueFormatData? FormatData { get; }

    /// <summary>Calculated duration (End - Start), or Zero if End &lt; Start.</summary>
    public TimeSpan Duration => End >= Start ? End - Start : TimeSpan.Zero;

    /// <summary>
    /// Lazily extracted plain text with formatting tags stripped according to format rules.
    /// </summary>
    public string PlainText => _plainText ??= SubtitleTextHelper.ExtractPlainText(RawText, FormatData?.Format);

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleCue"/> class.
    /// </summary>
    public SubtitleCue(TimeSpan start, TimeSpan end, string rawText, CueFormatData? formatData = null)
    {
        if (rawText == null) throw new ArgumentNullException(nameof(rawText));

        Start = start;
        End = end;
        // Normalize CRLF and lone CR to standard LF
        RawText = rawText.Replace("\r\n", "\n").Replace('\r', '\n');
        FormatData = formatData;
    }

    /// <summary>
    /// Creates a copy of this cue with adjusted start and end times.
    /// </summary>
    public SubtitleCue WithTimes(TimeSpan newStart, TimeSpan newEnd)
        => new(newStart, newEnd, RawText, FormatData);

    /// <summary>
    /// Creates a copy of this cue with adjusted raw text.
    /// </summary>
    public SubtitleCue WithText(string newRawText)
        => new(Start, End, newRawText, FormatData);

    /// <inheritdoc/>
    public bool Equals(SubtitleCue? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Start.Equals(other.Start)
            && End.Equals(other.End)
            && string.Equals(RawText, other.RawText, StringComparison.Ordinal)
            && Equals(FormatData, other.FormatData);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SubtitleCue);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Start.GetHashCode();
            hash = (hash * 31) + End.GetHashCode();
            hash = (hash * 31) + (RawText?.GetHashCode() ?? 0);
            hash = (hash * 31) + (FormatData?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"[{Start:hh\\:mm\\:ss\\.fff} -> {End:hh\\:mm\\:ss\\.fff}] {RawText}";
}
