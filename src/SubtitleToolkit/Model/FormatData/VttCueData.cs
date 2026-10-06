using System;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Format-specific data for a WebVTT cue.
/// </summary>
public sealed class VttCueData : CueFormatData, IEquatable<VttCueData>
{
    public override SubtitleFormat Format => SubtitleFormat.WebVtt;

    /// <summary>Optional cue identifier appearing on the line above timestamps.</summary>
    public string? Identifier { get; set; }

    /// <summary>Verbatim raw cue settings string for lossless round-trips.</summary>
    public string RawSettings { get; set; }

    /// <summary>Parsed structured cue settings.</summary>
    public VttCueSettings Settings { get; set; }

    public VttCueData(string? identifier = null, string? rawSettings = null, VttCueSettings? settings = null)
    {
        Identifier = identifier;
        RawSettings = rawSettings ?? string.Empty;
        Settings = settings ?? (string.IsNullOrEmpty(rawSettings) ? new VttCueSettings() : VttCueSettings.Parse(rawSettings));
    }

    public bool Equals(VttCueData? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Identifier, other.Identifier, StringComparison.Ordinal)
            && string.Equals(RawSettings, other.RawSettings, StringComparison.Ordinal)
            && Equals(Settings, other.Settings);
    }

    public override bool Equals(object? obj) => Equals(obj as VttCueData);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (Identifier?.GetHashCode() ?? 0);
            hash = (hash * 31) + (RawSettings?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
