using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace SubtitleToolkit.Model.FormatData;

public enum VttAlignment
{
    Start,
    Center,
    End,
    Left,
    Right
}

public enum VttVertical
{
    RightToLeft, // rl
    LeftToRight  // lr
}

public enum VttLineAlign
{
    Start,
    Center,
    End
}

public enum VttPositionAlign
{
    LineLeft,
    Center,
    LineRight,
    Auto
}

public sealed class VttLineSetting : IEquatable<VttLineSetting>
{
    public double Value { get; set; }
    public bool IsPercentage { get; set; }
    public VttLineAlign? Alignment { get; set; }

    public bool Equals(VttLineSetting? other)
    {
        if (other is null) return false;
        return Math.Abs(Value - other.Value) < 0.001
            && IsPercentage == other.IsPercentage
            && Alignment == other.Alignment;
    }

    public override bool Equals(object? obj) => Equals(obj as VttLineSetting);
    public override int GetHashCode() => Value.GetHashCode() ^ IsPercentage.GetHashCode();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Value.ToString("0.##", CultureInfo.InvariantCulture));
        if (IsPercentage) sb.Append('%');
        if (Alignment.HasValue)
        {
            sb.Append(',');
            sb.Append(Alignment.Value.ToString().ToLowerInvariant());
        }
        return sb.ToString();
    }
}

public sealed class VttPositionSetting : IEquatable<VttPositionSetting>
{
    public double Percentage { get; set; }
    public VttPositionAlign? Alignment { get; set; }

    public bool Equals(VttPositionSetting? other)
    {
        if (other is null) return false;
        return Math.Abs(Percentage - other.Percentage) < 0.001
            && Alignment == other.Alignment;
    }

    public override bool Equals(object? obj) => Equals(obj as VttPositionSetting);
    public override int GetHashCode() => Percentage.GetHashCode() ^ (Alignment?.GetHashCode() ?? 0);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Percentage.ToString("0.##", CultureInfo.InvariantCulture));
        sb.Append('%');
        if (Alignment.HasValue)
        {
            sb.Append(',');
            sb.Append(Alignment.Value switch
            {
                VttPositionAlign.LineLeft => "line-left",
                VttPositionAlign.LineRight => "line-right",
                _ => Alignment.Value.ToString().ToLowerInvariant()
            });
        }
        return sb.ToString();
    }
}

public sealed class VttCueSettings : IEquatable<VttCueSettings>
{
    public string? Region { get; set; }
    public VttLineSetting? Line { get; set; }
    public VttPositionSetting? Position { get; set; }
    public double? Size { get; set; }
    public VttAlignment? Align { get; set; }
    public VttVertical? Vertical { get; set; }
    public IReadOnlyDictionary<string, string> UnknownSettings { get; }

    public VttCueSettings(
        string? region = null,
        VttLineSetting? line = null,
        VttPositionSetting? position = null,
        double? size = null,
        VttAlignment? align = null,
        VttVertical? vertical = null,
        IDictionary<string, string>? unknownSettings = null)
    {
        Region = region;
        Line = line;
        Position = position;
        Size = size;
        Align = align;
        Vertical = vertical;
        UnknownSettings = new ReadOnlyDictionary<string, string>(
            unknownSettings != null ? new Dictionary<string, string>(unknownSettings, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>());
    }

    public static VttCueSettings Parse(string? settingsString)
    {
        if (string.IsNullOrWhiteSpace(settingsString))
            return new VttCueSettings();

        string? region = null;
        VttLineSetting? line = null;
        VttPositionSetting? position = null;
        double? size = null;
        VttAlignment? align = null;
        VttVertical? vertical = null;
        var unknowns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var tokens = settingsString.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var colonIdx = token.IndexOf(':');
            if (colonIdx <= 0 || colonIdx == token.Length - 1)
            {
                unknowns[token] = string.Empty;
                continue;
            }

            var name = token.Substring(0, colonIdx).Trim().ToLowerInvariant();
            var val = token.Substring(colonIdx + 1).Trim();

            switch (name)
            {
                case "region":
                    region = val;
                    break;
                case "line":
                    line = ParseLine(val);
                    break;
                case "position":
                    position = ParsePosition(val);
                    break;
                case "size":
                    if (val.EndsWith("%") && double.TryParse(val.Substring(0, val.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                        size = s;
                    break;
                case "align":
                    align = val.ToLowerInvariant() switch
                    {
                        "start" => VttAlignment.Start,
                        "center" => VttAlignment.Center,
                        "end" => VttAlignment.End,
                        "left" => VttAlignment.Left,
                        "right" => VttAlignment.Right,
                        _ => null
                    };
                    break;
                case "vertical":
                    vertical = val.ToLowerInvariant() switch
                    {
                        "rl" => VttVertical.RightToLeft,
                        "lr" => VttVertical.LeftToRight,
                        _ => null
                    };
                    break;
                default:
                    unknowns[name] = val;
                    break;
            }
        }

        return new VttCueSettings(region, line, position, size, align, vertical, unknowns);
    }

    private static VttLineSetting? ParseLine(string val)
    {
        var commaIdx = val.IndexOf(',');
        var numPart = commaIdx >= 0 ? val.Substring(0, commaIdx) : val;
        var alignPart = commaIdx >= 0 ? val.Substring(commaIdx + 1) : null;

        var isPct = numPart.EndsWith("%");
        var numStr = isPct ? numPart.Substring(0, numPart.Length - 1) : numPart;

        if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return null;

        VttLineAlign? lineAlign = alignPart?.ToLowerInvariant() switch
        {
            "start" => VttLineAlign.Start,
            "center" => VttLineAlign.Center,
            "end" => VttLineAlign.End,
            _ => null
        };

        return new VttLineSetting { Value = num, IsPercentage = isPct, Alignment = lineAlign };
    }

    private static VttPositionSetting? ParsePosition(string val)
    {
        var commaIdx = val.IndexOf(',');
        var numPart = commaIdx >= 0 ? val.Substring(0, commaIdx) : val;
        var alignPart = commaIdx >= 0 ? val.Substring(commaIdx + 1) : null;

        var numStr = numPart.EndsWith("%") ? numPart.Substring(0, numPart.Length - 1) : numPart;
        if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return null;

        VttPositionAlign? posAlign = alignPart?.ToLowerInvariant() switch
        {
            "line-left" => VttPositionAlign.LineLeft,
            "center" => VttPositionAlign.Center,
            "line-right" => VttPositionAlign.LineRight,
            "auto" => VttPositionAlign.Auto,
            _ => null
        };

        return new VttPositionSetting { Percentage = num, Alignment = posAlign };
    }

    public string ToSettingsString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Region)) parts.Add($"region:{Region}");
        if (Vertical.HasValue) parts.Add($"vertical:{(Vertical.Value == VttVertical.RightToLeft ? "rl" : "lr")}");
        if (Line != null) parts.Add($"line:{Line}");
        if (Position != null) parts.Add($"position:{Position}");
        if (Size.HasValue) parts.Add($"size:{Size.Value.ToString("0.##", CultureInfo.InvariantCulture)}%");
        if (Align.HasValue) parts.Add($"align:{Align.Value.ToString().ToLowerInvariant()}");

        foreach (var kvp in UnknownSettings)
        {
            parts.Add(string.IsNullOrEmpty(kvp.Value) ? kvp.Key : $"{kvp.Key}:{kvp.Value}");
        }

        return string.Join(" ", parts.ToArray());
    }

    public bool Equals(VttCueSettings? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Region, other.Region, StringComparison.Ordinal)
            && Equals(Line, other.Line)
            && Equals(Position, other.Position)
            && Nullable.Equals(Size, other.Size)
            && Align == other.Align
            && Vertical == other.Vertical;
    }

    public override bool Equals(object? obj) => Equals(obj as VttCueSettings);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (Region?.GetHashCode() ?? 0);
            hash = (hash * 31) + (Line?.GetHashCode() ?? 0);
            hash = (hash * 31) + (Position?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
