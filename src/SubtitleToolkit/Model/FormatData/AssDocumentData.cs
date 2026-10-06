using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Format-specific document data for ASS and SSA subtitle files.
/// </summary>
public sealed class AssDocumentData : DocumentFormatData, IEquatable<AssDocumentData>
{
    public override SubtitleFormat Format => IsSsa ? SubtitleFormat.Ssa : SubtitleFormat.Ass;

    /// <summary>Whether this document represents an SSA v4 script rather than ASS v4+.</summary>
    public bool IsSsa { get; set; }

    /// <summary>Script Info headers (e.g. Title, ScriptType, PlayResX, PlayResY, WrapStyle).</summary>
    public IReadOnlyDictionary<string, string> ScriptInfo { get; }

    /// <summary>Style definitions from [V4+ Styles] or [V4 Styles].</summary>
    public IReadOnlyList<AssStyle> Styles { get; }

    /// <summary>Original column ordering from the 'Format:' line in the styles section.</summary>
    public string? StylesFormatOrder { get; set; }

    /// <summary>Original column ordering from the 'Format:' line in the [Events] section.</summary>
    public string? EventsFormatOrder { get; set; }

    /// <summary>Unparsed or custom sections preserved verbatim (e.g. [Aegisub Project Garbage], [Fonts]).</summary>
    public IReadOnlyList<RawAssSection> UnknownSections { get; }

    public AssDocumentData(
        IDictionary<string, string>? scriptInfo = null,
        IEnumerable<AssStyle>? styles = null,
        IEnumerable<RawAssSection>? unknownSections = null,
        bool isSsa = false)
    {
        IsSsa = isSsa;
        ScriptInfo = new ReadOnlyDictionary<string, string>(
            scriptInfo != null ? new Dictionary<string, string>(scriptInfo, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>());

        Styles = new ReadOnlyCollection<AssStyle>(
            styles != null ? styles.ToList() : new List<AssStyle>());

        UnknownSections = new ReadOnlyCollection<RawAssSection>(
            unknownSections != null ? unknownSections.ToList() : new List<RawAssSection>());
    }

    public bool Equals(AssDocumentData? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (IsSsa != other.IsSsa) return false;
        if (Styles.Count != other.Styles.Count) return false;
        for (var i = 0; i < Styles.Count; i++)
        {
            if (!Styles[i].Equals(other.Styles[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as AssDocumentData);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + IsSsa.GetHashCode();
            hash = (hash * 31) + Styles.Count.GetHashCode();
            return hash;
        }
    }
}
