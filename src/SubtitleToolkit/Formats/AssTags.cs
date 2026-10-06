using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace SubtitleToolkit.Formats.Ass;

/// <summary>
/// Abstract base record for strongly-typed ASS override tags.
/// </summary>
public abstract record AssTag
{
    /// <summary>Tag name identifier (e.g. "b", "pos", "c").</summary>
    public abstract string Name { get; }

    /// <summary>Renders the tag as a verbatim ASS override tag (e.g. "\b1" or "\pos(100,200)").</summary>
    public abstract override string ToString();
}

/// <summary>Bold style tag (\b1, \b0, or \b&lt;weight&gt;).</summary>
public sealed record BoldTag(bool? Enabled = null, int? Weight = null) : AssTag
{
    public override string Name => "b";

    public override string ToString()
    {
        if (Weight.HasValue) return $"\\b{Weight.Value}";
        if (Enabled.HasValue) return $"\\b{(Enabled.Value ? 1 : 0)}";
        return "\\b";
    }
}

/// <summary>Italic style tag (\i1 or \i0).</summary>
public sealed record ItalicTag(bool Enabled) : AssTag
{
    public override string Name => "i";
    public override string ToString() => $"\\i{(Enabled ? 1 : 0)}";
}

/// <summary>Underline style tag (\u1 or \u0).</summary>
public sealed record UnderlineTag(bool Enabled) : AssTag
{
    public override string Name => "u";
    public override string ToString() => $"\\u{(Enabled ? 1 : 0)}";
}

/// <summary>Strikeout style tag (\s1 or \s0).</summary>
public sealed record StrikeoutTag(bool Enabled) : AssTag
{
    public override string Name => "s";
    public override string ToString() => $"\\s{(Enabled ? 1 : 0)}";
}

/// <summary>Color override tag (\c, \1c, \2c, \3c, \4c).</summary>
public sealed record ColorTag(
    int Target,
    string RawColor,
    bool UseShorthand = false,
    byte? Red = null,
    byte? Green = null,
    byte? Blue = null) : AssTag
{
    public override string Name => UseShorthand ? "c" : $"{Target}c";
    public override string ToString() => $"\\{Name}{RawColor}";
}

/// <summary>Alpha transparency tag (\alpha, \1a, \2a, \3a, \4a).</summary>
public sealed record AlphaTag(
    int Target,
    string RawAlpha,
    byte? Alpha = null) : AssTag
{
    public override string Name => Target == 0 ? "alpha" : $"{Target}a";
    public override string ToString() => $"\\{Name}{RawAlpha}";
}

/// <summary>Position tag (\pos(x, y)).</summary>
public sealed record PosTag(double X, double Y) : AssTag
{
    public override string Name => "pos";
    public override string ToString() =>
        $"\\pos({X.ToString("0.##", CultureInfo.InvariantCulture)},{Y.ToString("0.##", CultureInfo.InvariantCulture)})";
}

/// <summary>Movement tag (\move(x1, y1, x2, y2) or \move(x1, y1, x2, y2, t1, t2)).</summary>
public sealed record MoveTag(
    double X1,
    double Y1,
    double X2,
    double Y2,
    int? T1 = null,
    int? T2 = null) : AssTag
{
    public override string Name => "move";

    public override string ToString()
    {
        var x1 = X1.ToString("0.##", CultureInfo.InvariantCulture);
        var y1 = Y1.ToString("0.##", CultureInfo.InvariantCulture);
        var x2 = X2.ToString("0.##", CultureInfo.InvariantCulture);
        var y2 = Y2.ToString("0.##", CultureInfo.InvariantCulture);

        return T1.HasValue && T2.HasValue
            ? $"\\move({x1},{y1},{x2},{y2},{T1.Value},{T2.Value})"
            : $"\\move({x1},{y1},{x2},{y2})";
    }
}

/// <summary>Origin / pivot tag (\org(x, y)).</summary>
public sealed record OrgTag(double X, double Y) : AssTag
{
    public override string Name => "org";
    public override string ToString() =>
        $"\\org({X.ToString("0.##", CultureInfo.InvariantCulture)},{Y.ToString("0.##", CultureInfo.InvariantCulture)})";
}

/// <summary>Fade tag (\fad(t1, t2) or \fade(a1, a2, a3, t1, t2, t3, t4)).</summary>
public sealed record FadeTag(
    bool IsSimple,
    int? FadeIn = null,
    int? FadeOut = null,
    int? A1 = null,
    int? A2 = null,
    int? A3 = null,
    int? T1 = null,
    int? T2 = null,
    int? T3 = null,
    int? T4 = null,
    string? RawArgs = null) : AssTag
{
    public override string Name => IsSimple ? "fad" : "fade";

    public override string ToString()
    {
        if (RawArgs != null) return $"\\{Name}{RawArgs}";
        if (IsSimple) return $"\\fad({FadeIn ?? 0},{FadeOut ?? 0})";
        return $"\\fade({A1 ?? 0},{A2 ?? 0},{A3 ?? 0},{T1 ?? 0},{T2 ?? 0},{T3 ?? 0},{T4 ?? 0})";
    }
}

/// <summary>Karaoke timing tag (\k, \K, \kf, \ko, \kt).</summary>
public sealed record KaraokeTag(string Type, int DurationCentiseconds) : AssTag
{
    public override string Name => Type;
    public override string ToString() => $"\\{Type}{DurationCentiseconds}";
}

/// <summary>Vector drawing mode tag (\p&lt;scale&gt; or \pbo&lt;offset&gt;).</summary>
public sealed record DrawingTag(int Scale, int? BaselineOffset = null) : AssTag
{
    public override string Name => BaselineOffset.HasValue ? "pbo" : "p";
    public override string ToString() =>
        BaselineOffset.HasValue ? $"\\pbo{BaselineOffset.Value}" : $"\\p{Scale}";
}

/// <summary>Alignment override tag (\an&lt;1-9&gt; or legacy SSA \a&lt;1-11&gt;).</summary>
public sealed record AlignmentTag(int Alignment, bool IsLegacy = false) : AssTag
{
    public override string Name => IsLegacy ? "a" : "an";
    public override string ToString() => $"\\{Name}{Alignment}";
}

/// <summary>Font size tag (\fs&lt;size&gt;).</summary>
public sealed record FontSizeTag(double Size, string? RawArg = null) : AssTag
{
    public override string Name => "fs";
    public override string ToString() =>
        RawArg != null ? $"\\fs{RawArg}" : $"\\fs{Size.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Font name tag (\fn&lt;font&gt;).</summary>
public sealed record FontNameTag(string FontName) : AssTag
{
    public override string Name => "fn";
    public override string ToString() => $"\\fn{FontName}";
}

/// <summary>Font scale tag (\fscx&lt;scale&gt; or \fscy&lt;scale&gt;).</summary>
public sealed record FontScaleTag(char Axis, double Scale) : AssTag
{
    public override string Name => $"fsc{Axis}";
    public override string ToString() => $"\\{Name}{Scale.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Font letter spacing tag (\fsp&lt;spacing&gt;).</summary>
public sealed record FontSpacingTag(double Spacing) : AssTag
{
    public override string Name => "fsp";
    public override string ToString() => $"\\fsp{Spacing.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Font rotation tag (\fr, \frx, \fry, \frz).</summary>
public sealed record FontRotationTag(char? Axis, double Angle) : AssTag
{
    public override string Name => Axis.HasValue ? $"fr{Axis.Value}" : "fr";
    public override string ToString() => $"\\{Name}{Angle.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Blur effect tag (\blur&lt;strength&gt; or \be&lt;strength&gt;).</summary>
public sealed record BlurTag(double Strength, bool Edges = false) : AssTag
{
    public override string Name => Edges ? "be" : "blur";
    public override string ToString() =>
        Edges ? $"\\be{(int)Strength}" : $"\\blur{Strength.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Border outline thickness tag (\bord, \xbord, \ybord).</summary>
public sealed record BorderTag(double Width, char? Axis = null) : AssTag
{
    public override string Name => Axis.HasValue ? $"{Axis.Value}bord" : "bord";
    public override string ToString() => $"\\{Name}{Width.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Shadow depth tag (\shad, \xshad, \yshad).</summary>
public sealed record ShadowTag(double Depth, char? Axis = null) : AssTag
{
    public override string Name => Axis.HasValue ? $"{Axis.Value}shad" : "shad";
    public override string ToString() => $"\\{Name}{Depth.ToString("0.##", CultureInfo.InvariantCulture)}";
}

/// <summary>Clip rectangular or vector tag (\clip(...) or \iclip(...)).</summary>
public sealed record ClipTag(
    bool Inverse,
    string RawArgs,
    double? X1 = null,
    double? Y1 = null,
    double? X2 = null,
    double? Y2 = null,
    string? DrawingCommands = null) : AssTag
{
    public override string Name => Inverse ? "iclip" : "clip";
    public override string ToString() => $"\\{Name}{RawArgs}";
}

/// <summary>Transform animation tag (\t(...)).</summary>
public sealed record TransformTag(
    string RawArgs,
    int? T1 = null,
    int? T2 = null,
    double? Accel = null,
    IReadOnlyList<AssTag>? NestedTags = null) : AssTag
{
    public override string Name => "t";
    public override string ToString() => $"\\t{RawArgs}";
}

/// <summary>Style reset tag (\r or \r&lt;style&gt;).</summary>
public sealed record ResetTag(string? StyleName = null) : AssTag
{
    public override string Name => "r";
    public override string ToString() => string.IsNullOrEmpty(StyleName) ? "\\r" : $"\\r{StyleName}";
}

/// <summary>Fallback representation for unrecognized, custom, or extension override tags.</summary>
public sealed record UnknownTag(string TagName, string RawArgs) : AssTag
{
    public override string Name => TagName;
    public override string ToString() =>
        string.IsNullOrEmpty(TagName) ? RawArgs : $"\\{TagName}{RawArgs}";
}
