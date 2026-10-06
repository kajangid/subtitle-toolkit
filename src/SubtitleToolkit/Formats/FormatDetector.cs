using System;
using SubtitleToolkit.Model;

namespace SubtitleToolkit.Formats;

/// <summary>
/// Helper for auto-detecting subtitle format from file contents.
/// </summary>
public static class FormatDetector
{
    /// <summary>
    /// Detects subtitle format from string content.
    /// </summary>
    public static SubtitleFormat Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return SubtitleFormat.SubRip;

        var sample = content.Length > 2048 ? content.Substring(0, 2048) : content;

        if (sample.TrimStart().StartsWith("WEBVTT", StringComparison.Ordinal))
            return SubtitleFormat.WebVtt;

        if (sample.IndexOf("[V4+ Styles]", StringComparison.OrdinalIgnoreCase) >= 0 ||
            sample.IndexOf("ScriptType: v4.00+", StringComparison.OrdinalIgnoreCase) >= 0)
            return SubtitleFormat.Ass;

        if (sample.IndexOf("[V4 Styles]", StringComparison.OrdinalIgnoreCase) >= 0 ||
            sample.IndexOf("ScriptType: v4.00", StringComparison.OrdinalIgnoreCase) >= 0)
            return SubtitleFormat.Ssa;

        if (sample.IndexOf("[Script Info]", StringComparison.OrdinalIgnoreCase) >= 0)
            return SubtitleFormat.Ass;

        if (sample.IndexOf("-->", StringComparison.Ordinal) >= 0)
            return SubtitleFormat.SubRip;

        return SubtitleFormat.SubRip;
    }
}
