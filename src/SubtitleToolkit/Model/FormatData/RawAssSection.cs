using System;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Stores an unparsed or custom section from an ASS/SSA script to ensure 100% round-trip retention.
/// </summary>
public sealed class RawAssSection : IEquatable<RawAssSection>
{
    /// <summary>Section header name without brackets (e.g. "Aegisub Project Garbage").</summary>
    public string SectionName { get; }

    /// <summary>Verbatim body text of the section.</summary>
    public string Content { get; }

    public RawAssSection(string sectionName, string content)
    {
        SectionName = sectionName ?? throw new ArgumentNullException(nameof(sectionName));
        Content = content ?? string.Empty;
    }

    public bool Equals(RawAssSection? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(SectionName, other.SectionName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Content, other.Content, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as RawAssSection);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (SectionName?.ToLowerInvariant().GetHashCode() ?? 0);
            hash = (hash * 31) + (Content?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
