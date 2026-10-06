using System;
using System.Globalization;

namespace SubtitleToolkit.Common;

/// <summary>
/// High-performance timestamp parsing and formatting utilities across subtitle formats.
/// </summary>
public static class TimestampHelper
{
    /// <summary>
    /// Parses an SRT timestamp (hh:mm:ss,fff or hh:mm:ss.fff).
    /// </summary>
    public static bool TryParseSrtTimestamp(string text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim().Replace('.', ',');
        var commaIdx = text.IndexOf(',');
        if (commaIdx < 0) return false;

        var hms = text.Substring(0, commaIdx);
        var msStr = text.Substring(commaIdx + 1);

        if (!TryParseHms(hms, out var hours, out var minutes, out var seconds))
            return false;

        if (!int.TryParse(msStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return false;

        // Normalize if ms has fewer or more digits
        if (msStr.Length == 1) ms *= 100;
        else if (msStr.Length == 2) ms *= 10;
        else if (msStr.Length > 3) ms = int.Parse(msStr.Substring(0, 3));

        time = new TimeSpan(0, hours, minutes, seconds, ms);
        return true;
    }

    /// <summary>
    /// Parses a WebVTT timestamp (hh:mm:ss.fff or mm:ss.fff).
    /// </summary>
    public static bool TryParseVttTimestamp(string text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        var dotIdx = text.IndexOf('.');
        if (dotIdx < 0) return false;

        var timePart = text.Substring(0, dotIdx);
        var msStr = text.Substring(dotIdx + 1);

        var colonParts = timePart.Split(':');
        int hours = 0, minutes = 0, seconds = 0;

        if (colonParts.Length == 3)
        {
            if (!int.TryParse(colonParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hours)
                || !int.TryParse(colonParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes)
                || !int.TryParse(colonParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                return false;
        }
        else if (colonParts.Length == 2)
        {
            if (!int.TryParse(colonParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes)
                || !int.TryParse(colonParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                return false;
        }
        else
        {
            return false;
        }

        if (!int.TryParse(msStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return false;

        if (msStr.Length == 1) ms *= 100;
        else if (msStr.Length == 2) ms *= 10;
        else if (msStr.Length > 3) ms = int.Parse(msStr.Substring(0, 3));

        time = new TimeSpan(0, hours, minutes, seconds, ms);
        return true;
    }

    /// <summary>
    /// Parses an ASS/SSA timestamp (h:mm:ss.cc).
    /// </summary>
    public static bool TryParseAssTimestamp(string text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        var dotIdx = text.IndexOf('.');
        if (dotIdx < 0) return false;

        var hms = text.Substring(0, dotIdx);
        var csStr = text.Substring(dotIdx + 1);

        if (!TryParseHms(hms, out var hours, out var minutes, out var seconds))
            return false;

        if (!int.TryParse(csStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cs))
            return false;

        // Centiseconds to milliseconds (1 cs = 10 ms)
        var ms = cs * 10;
        time = new TimeSpan(0, hours, minutes, seconds, ms);
        return true;
    }

    private static bool TryParseHms(string hms, out int hours, out int minutes, out int seconds)
    {
        hours = minutes = seconds = 0;
        var parts = hms.Split(':');
        if (parts.Length != 3) return false;

        return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hours)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds);
    }

    /// <summary>
    /// Formats a TimeSpan as an SRT timestamp: hh:mm:ss,fff.
    /// </summary>
    public static string FormatSrt(TimeSpan time)
    {
        var totalHours = (int)time.TotalHours;
        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2},{3:D3}",
            totalHours, time.Minutes, time.Seconds, time.Milliseconds);
    }

    /// <summary>
    /// Formats a TimeSpan as a WebVTT timestamp: hh:mm:ss.fff.
    /// </summary>
    public static string FormatVtt(TimeSpan time)
    {
        var totalHours = (int)time.TotalHours;
        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}.{3:D3}",
            totalHours, time.Minutes, time.Seconds, time.Milliseconds);
    }

    /// <summary>
    /// Formats a TimeSpan as an ASS timestamp: h:mm:ss.cc with standard centisecond rounding.
    /// </summary>
    public static string FormatAss(TimeSpan time)
    {
        var totalHours = (int)time.TotalHours;
        // Centisecond rounding: (ms + 5) / 10
        var totalCentiseconds = (int)Math.Round(time.TotalMilliseconds / 10.0, MidpointRounding.AwayFromZero);
        var cs = totalCentiseconds % 100;
        var totalSec = totalCentiseconds / 100;
        var sec = totalSec % 60;
        var totalMin = totalSec / 60;
        var min = totalMin % 60;
        var hrs = totalMin / 60;

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}.{3:D2}",
            hrs, min, sec, cs);
    }
}
