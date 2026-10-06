using System;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Format-specific data for a SubRip (.srt) cue.
/// </summary>
public sealed class SrtCueData : CueFormatData, IEquatable<SrtCueData>
{
    public override SubtitleFormat Format => SubtitleFormat.SubRip;

    /// <summary>Original sequential index in the input file.</summary>
    public int? OriginalIndex { get; set; }

    /// <summary>Optional coordinate position string (e.g. X1:000 X2:000 Y1:000 Y2:000).</summary>
    public string? Coordinates { get; set; }

    public SrtCueData(int? originalIndex = null, string? coordinates = null)
    {
        OriginalIndex = originalIndex;
        Coordinates = coordinates;
    }

    public bool Equals(SrtCueData? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return OriginalIndex == other.OriginalIndex
            && string.Equals(Coordinates, other.Coordinates, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as SrtCueData);

    public override int GetHashCode() => (OriginalIndex?.GetHashCode() ?? 0) ^ (Coordinates?.GetHashCode() ?? 0);
}
