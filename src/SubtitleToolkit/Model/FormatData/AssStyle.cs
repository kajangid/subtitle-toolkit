using System;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Represents a style definition from an ASS or SSA script.
/// </summary>
public sealed class AssStyle : IEquatable<AssStyle>
{
    public string Name { get; set; } = "Default";
    public string FontName { get; set; } = "Arial";
    public double FontSize { get; set; } = 20.0;
    public string PrimaryColour { get; set; } = "&H00FFFFFF";
    public string SecondaryColour { get; set; } = "&H00000000";
    public string OutlineColour { get; set; } = "&H00000000";
    public string BackColour { get; set; } = "&H00000000";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool StrikeOut { get; set; }
    public double ScaleX { get; set; } = 100.0;
    public double ScaleY { get; set; } = 100.0;
    public double Spacing { get; set; } = 0.0;
    public double Angle { get; set; } = 0.0;
    public int BorderStyle { get; set; } = 1;
    public double Outline { get; set; } = 2.0;
    public double Shadow { get; set; } = 2.0;
    public int Alignment { get; set; } = 2; // Default bottom-center (numpad 2 in ASS, 2 in SSA)
    public int MarginL { get; set; } = 10;
    public int MarginR { get; set; } = 10;
    public int MarginV { get; set; } = 10;
    public int Encoding { get; set; } = 1;

    /// <summary>Original raw style line if preserved.</summary>
    public string? RawLine { get; set; }

    public bool Equals(AssStyle? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(FontName, other.FontName, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(FontSize - other.FontSize) < 0.001
            && string.Equals(PrimaryColour, other.PrimaryColour, StringComparison.OrdinalIgnoreCase)
            && Bold == other.Bold
            && Italic == other.Italic
            && Alignment == other.Alignment;
    }

    public override bool Equals(object? obj) => Equals(obj as AssStyle);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (Name?.ToLowerInvariant().GetHashCode() ?? 0);
            hash = (hash * 31) + FontSize.GetHashCode();
            hash = (hash * 31) + Alignment.GetHashCode();
            return hash;
        }
    }
}
