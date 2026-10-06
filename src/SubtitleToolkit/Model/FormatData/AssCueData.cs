using System;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Event types supported in ASS/SSA scripts.
/// </summary>
public enum AssEventType
{
    Dialogue,
    Comment,
    Picture,
    Sound,
    Movie,
    Command
}

/// <summary>
/// Format-specific data for ASS/SSA cues.
/// </summary>
public sealed class AssCueData : CueFormatData, IEquatable<AssCueData>
{
    public override SubtitleFormat Format => IsSsa ? SubtitleFormat.Ssa : SubtitleFormat.Ass;

    /// <summary>Whether this cue belongs to an SSA v4 script (uses Marked= rather than Layer).</summary>
    public bool IsSsa { get; set; }

    /// <summary>Event type (Dialogue, Comment, etc.).</summary>
    public AssEventType EventType { get; set; } = AssEventType.Dialogue;

    /// <summary>Subtitles layer (ASS v4+).</summary>
    public int Layer { get; set; }

    /// <summary>Marked flag (SSA v4).</summary>
    public bool Marked { get; set; }

    /// <summary>Name of style applied to this cue.</summary>
    public string StyleName { get; set; } = "Default";

    /// <summary>Actor or character speaking this line.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Left margin override in pixels.</summary>
    public int MarginL { get; set; }

    /// <summary>Right margin override in pixels.</summary>
    public int MarginR { get; set; }

    /// <summary>Vertical margin override in pixels.</summary>
    public int MarginV { get; set; }

    /// <summary>Transition effect (e.g. "Banner;...").</summary>
    public string Effect { get; set; } = string.Empty;

    public bool Equals(AssCueData? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return IsSsa == other.IsSsa
            && EventType == other.EventType
            && Layer == other.Layer
            && Marked == other.Marked
            && string.Equals(StyleName, other.StyleName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ActorName, other.ActorName, StringComparison.Ordinal)
            && MarginL == other.MarginL
            && MarginR == other.MarginR
            && MarginV == other.MarginV
            && string.Equals(Effect, other.Effect, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as AssCueData);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + EventType.GetHashCode();
            hash = (hash * 31) + Layer.GetHashCode();
            hash = (hash * 31) + (StyleName?.ToLowerInvariant().GetHashCode() ?? 0);
            return hash;
        }
    }
}
