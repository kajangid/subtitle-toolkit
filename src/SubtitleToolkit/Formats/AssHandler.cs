using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SubtitleToolkit.Common;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;

namespace SubtitleToolkit.Formats;

/// <summary>
/// Parser and serializer for Advanced SubStation Alpha v4.00+ (.ass) subtitle files.
/// </summary>
public class AssHandler : ISubtitleHandler
{
    public virtual SubtitleFormat Format => SubtitleFormat.Ass;

    protected virtual bool IsSsa => false;

    public ParseResult Parse(string content, SubtitleReadOptions options)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        var cues = new List<SubtitleCue>();
        var diagnostics = new List<ParseDiagnostic>();
        var scriptInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var styles = new List<AssStyle>();
        var unknownSections = new List<RawAssSection>();

        string? stylesFormatOrder = null;
        string? eventsFormatOrder = null;

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string? currentSection = null;
        var currentSectionLines = new List<string>();

        string[]? styleColumns = null;
        string[]? eventColumns = null;

        void FlushUnknownSection()
        {
            if (currentSection != null && currentSectionLines.Count > 0)
            {
                var body = string.Join("\n", currentSectionLines.ToArray());
                unknownSections.Add(new RawAssSection(currentSection, body));
                currentSectionLines.Clear();
            }
        }

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx].Trim();
            var lineNum = lineIdx + 1;

            if (line.Length == 0)
            {
                if (currentSection != null && !IsStandardSection(currentSection))
                    currentSectionLines.Add(string.Empty);
                continue;
            }

            // Section header e.g. [Script Info], [V4+ Styles], [Events]
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                FlushUnknownSection();
                currentSection = line.Substring(1, line.Length - 2).Trim();
                continue;
            }

            // Ignore comments in header/styles (e.g. ; This is a comment)
            if (line.StartsWith(";") && currentSection != "Events")
            {
                continue;
            }

            switch (currentSection?.ToLowerInvariant())
            {
                case "script info":
                    var colonIdx = line.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var key = line.Substring(0, colonIdx).Trim();
                        var val = line.Substring(colonIdx + 1).Trim();
                        scriptInfo[key] = val;
                    }
                    break;

                case "v4+ styles":
                case "v4 styles":
                    if (line.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                    {
                        stylesFormatOrder = line.Substring(7).Trim();
                        styleColumns = stylesFormatOrder.Split(new[] { ',' }, StringSplitOptions.None);
                        for (var c = 0; c < styleColumns.Length; c++) styleColumns[c] = styleColumns[c].Trim().ToLowerInvariant();
                    }
                    else if (line.StartsWith("Style:", StringComparison.OrdinalIgnoreCase))
                    {
                        var styleData = line.Substring(6).Trim();
                        var style = ParseStyle(styleData, styleColumns, line, IsSsa);
                        styles.Add(style);
                    }
                    break;

                case "events":
                    if (line.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                    {
                        eventsFormatOrder = line.Substring(7).Trim();
                        eventColumns = eventsFormatOrder.Split(new[] { ',' }, StringSplitOptions.None);
                        for (var c = 0; c < eventColumns.Length; c++) eventColumns[c] = eventColumns[c].Trim().ToLowerInvariant();
                    }
                    else if (line.IndexOf(':') > 0)
                    {
                        var colonPos = line.IndexOf(':');
                        var eventTypeStr = line.Substring(0, colonPos).Trim();
                        var eventBody = line.Substring(colonPos + 1).Trim();

                        var parsedCue = ParseEventLine(eventTypeStr, eventBody, eventColumns, lineNum, IsSsa, options, diagnostics);
                        if (parsedCue != null)
                        {
                            cues.Add(parsedCue);
                        }
                    }
                    break;

                default:
                    if (currentSection != null)
                    {
                        currentSectionLines.Add(line);
                    }
                    break;
            }
        }

        FlushUnknownSection();

        var docData = new AssDocumentData(scriptInfo, styles, unknownSections, IsSsa)
        {
            StylesFormatOrder = stylesFormatOrder,
            EventsFormatOrder = eventsFormatOrder
        };

        var doc = new SubtitleDocument(IsSsa ? SubtitleFormat.Ssa : SubtitleFormat.Ass, cues, docData);
        return new ParseResult(doc, diagnostics);
    }

    private static bool IsStandardSection(string section)
    {
        var s = section.ToLowerInvariant();
        return s is "script info" or "v4+ styles" or "v4 styles" or "events";
    }

    private static AssStyle ParseStyle(string data, string[]? columns, string rawLine, bool isSsa)
    {
        var style = new AssStyle { RawLine = rawLine };
        if (columns == null || columns.Length == 0)
        {
            // Default ASS column order
            columns = new[] { "name", "fontname", "fontsize", "primarycolour", "secondarycolour", "outlinecolour", "backcolour",
                              "bold", "italic", "underline", "strikeout", "scalex", "scaley", "spacing", "angle", "borderstyle",
                              "outline", "shadow", "alignment", "marginl", "marginr", "marginv", "encoding" };
        }

        var values = data.Split(new[] { ',' }, columns.Length);
        for (var i = 0; i < Math.Min(columns.Length, values.Length); i++)
        {
            var col = columns[i];
            var val = values[i].Trim();

            switch (col)
            {
                case "name": style.Name = val; break;
                case "fontname": style.FontName = val; break;
                case "fontsize": if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs)) style.FontSize = fs; break;
                case "primarycolour": style.PrimaryColour = val; break;
                case "secondarycolour": style.SecondaryColour = val; break;
                case "outlinecolour":
                case "tertiarycolour": style.OutlineColour = val; break;
                case "backcolour": style.BackColour = val; break;
                case "bold": style.Bold = val == "-1" || val == "1"; break;
                case "italic": style.Italic = val == "-1" || val == "1"; break;
                case "underline": style.Underline = val == "-1" || val == "1"; break;
                case "strikeout": style.StrikeOut = val == "-1" || val == "1"; break;
                case "scalex": if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var sx)) style.ScaleX = sx; break;
                case "scaley": if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var sy)) style.ScaleY = sy; break;
                case "spacing": if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var sp)) style.Spacing = sp; break;
                case "angle": if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var ang)) style.Angle = ang; break;
                case "borderstyle": if (int.TryParse(val, out var bs)) style.BorderStyle = bs; break;
                case "outline": if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var outl)) style.Outline = outl; break;
                case "shadow": if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var sh)) style.Shadow = sh; break;
                case "alignment":
                    if (int.TryParse(val, out var align)) style.Alignment = align;
                    break;
                case "marginl": if (int.TryParse(val, out var ml)) style.MarginL = ml; break;
                case "marginr": if (int.TryParse(val, out var mr)) style.MarginR = mr; break;
                case "marginv": if (int.TryParse(val, out var mv)) style.MarginV = mv; break;
                case "encoding": if (int.TryParse(val, out var enc)) style.Encoding = enc; break;
            }
        }

        return style;
    }

    private static SubtitleCue? ParseEventLine(
        string eventTypeStr,
        string body,
        string[]? columns,
        int lineNum,
        bool isSsa,
        SubtitleReadOptions options,
        List<ParseDiagnostic> diagnostics)
    {
        var eventType = eventTypeStr.ToLowerInvariant() switch
        {
            "comment" => AssEventType.Comment,
            "picture" => AssEventType.Picture,
            "sound" => AssEventType.Sound,
            "movie" => AssEventType.Movie,
            "command" => AssEventType.Command,
            _ => AssEventType.Dialogue
        };

        if (columns == null || columns.Length == 0)
        {
            columns = isSsa
                ? new[] { "marked", "start", "end", "style", "name", "marginl", "marginr", "marginv", "effect", "text" }
                : new[] { "layer", "start", "end", "style", "name", "marginl", "marginr", "marginv", "effect", "text" };
        }

        // Split with maxCount = columns.Length to prevent splitting the final Text field on internal commas
        var values = body.Split(new[] { ',' }, columns.Length);
        if (values.Length < columns.Length)
        {
            var msg = $"Event line {lineNum} has fewer fields ({values.Length}) than expected ({columns.Length}).";
            if (options.Mode == ParseMode.Strict)
                throw new SubtitleParseException(msg, lineNum);

            diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, lineNum, "ASS_FIELD_COUNT", msg));
        }

        int layer = 0;
        bool marked = false;
        TimeSpan start = TimeSpan.Zero;
        TimeSpan end = TimeSpan.Zero;
        string styleName = "Default";
        string actorName = string.Empty;
        int marginL = 0, marginR = 0, marginV = 0;
        string effect = string.Empty;
        string text = string.Empty;

        for (var i = 0; i < Math.Min(columns.Length, values.Length); i++)
        {
            var col = columns[i];
            var val = values[i];

            switch (col)
            {
                case "layer":
                    int.TryParse(val.Trim(), out layer);
                    break;
                case "marked":
                    marked = val.Trim() != "0";
                    break;
                case "start":
                    if (!TimestampHelper.TryParseAssTimestamp(val.Trim(), out start))
                    {
                        var msg = $"Invalid start timestamp '{val}' at line {lineNum}.";
                        if (options.Mode == ParseMode.Strict) throw new SubtitleParseException(msg, lineNum);
                        diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, lineNum, "ASS_START_TIME", msg));
                    }
                    break;
                case "end":
                    if (!TimestampHelper.TryParseAssTimestamp(val.Trim(), out end))
                    {
                        var msg = $"Invalid end timestamp '{val}' at line {lineNum}.";
                        if (options.Mode == ParseMode.Strict) throw new SubtitleParseException(msg, lineNum);
                        diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, lineNum, "ASS_END_TIME", msg));
                    }
                    break;
                case "style":
                    styleName = val.Trim();
                    break;
                case "name":
                    actorName = val.Trim();
                    break;
                case "marginl":
                    int.TryParse(val.Trim(), out marginL);
                    break;
                case "marginr":
                    int.TryParse(val.Trim(), out marginR);
                    break;
                case "marginv":
                    int.TryParse(val.Trim(), out marginV);
                    break;
                case "effect":
                    effect = val.Trim();
                    break;
                case "text":
                    text = val; // Note: do not trim text, whitespace in dialogue might be deliberate
                    break;
            }
        }

        var cueData = new AssCueData
        {
            IsSsa = isSsa,
            EventType = eventType,
            Layer = layer,
            Marked = marked,
            StyleName = styleName,
            ActorName = actorName,
            MarginL = marginL,
            MarginR = marginR,
            MarginV = marginV,
            Effect = effect
        };

        return new SubtitleCue(start, end, text, cueData);
    }

    public void Write(SubtitleDocument doc, TextWriter writer, SubtitleWriteOptions options)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        var le = options.LineEnding;
        var assData = doc.FormatData as AssDocumentData;

        // 1. [Script Info]
        writer.Write("[Script Info]");
        writer.Write(le);
        if (assData != null && assData.ScriptInfo.Count > 0)
        {
            foreach (var kvp in assData.ScriptInfo)
            {
                writer.Write($"{kvp.Key}: {kvp.Value}");
                writer.Write(le);
            }
        }
        else
        {
            writer.Write($"ScriptType: {(IsSsa ? "v4.00" : "v4.00+")}");
            writer.Write(le);
            writer.Write("PlayResX: 384");
            writer.Write(le);
            writer.Write("PlayResY: 288");
            writer.Write(le);
        }
        writer.Write(le);

        // 2. [V4+ Styles] / [V4 Styles]
        var styleSectionHeader = IsSsa ? "[V4 Styles]" : "[V4+ Styles]";
        writer.Write(styleSectionHeader);
        writer.Write(le);

        var styleFormatOrder = assData?.StylesFormatOrder
            ?? (IsSsa
                ? "Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding"
                : "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");

        if (!styleFormatOrder.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
            styleFormatOrder = "Format: " + styleFormatOrder;

        writer.Write(styleFormatOrder);
        writer.Write(le);

        var styles = assData?.Styles ?? new[] { new AssStyle() };
        foreach (var style in styles)
        {
            if (!string.IsNullOrEmpty(style.RawLine))
            {
                writer.Write(style.RawLine);
            }
            else
            {
                writer.Write(FormatStyleLine(style, IsSsa));
            }
            writer.Write(le);
        }
        writer.Write(le);

        // 3. Unknown / Custom Sections (e.g. [Aegisub Project Garbage], [Fonts])
        if (assData?.UnknownSections != null)
        {
            foreach (var section in assData.UnknownSections)
            {
                writer.Write($"[{section.SectionName}]");
                writer.Write(le);
                if (!string.IsNullOrEmpty(section.Content))
                {
                    writer.Write(section.Content.Replace("\n", le));
                    writer.Write(le);
                }
                writer.Write(le);
            }
        }

        // 4. [Events]
        writer.Write("[Events]");
        writer.Write(le);

        var eventsFormatOrder = assData?.EventsFormatOrder
            ?? (IsSsa
                ? "Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text"
                : "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        if (!eventsFormatOrder.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
            eventsFormatOrder = "Format: " + eventsFormatOrder;

        writer.Write(eventsFormatOrder);
        writer.Write(le);

        foreach (var cue in doc.Cues)
        {
            var cueData = cue.FormatData as AssCueData;
            var eventType = cueData?.EventType.ToString() ?? "Dialogue";

            var layerOrMarked = IsSsa
                ? (cueData?.Marked == true ? "Marked=1" : "Marked=0")
                : (cueData?.Layer.ToString(CultureInfo.InvariantCulture) ?? "0");

            var startStr = TimestampHelper.FormatAss(cue.Start);
            var endStr = TimestampHelper.FormatAss(cue.End);
            var styleName = !string.IsNullOrEmpty(cueData?.StyleName) ? cueData!.StyleName : "Default";
            var actor = cueData?.ActorName ?? string.Empty;
            var ml = cueData?.MarginL.ToString(CultureInfo.InvariantCulture) ?? "0";
            var mr = cueData?.MarginR.ToString(CultureInfo.InvariantCulture) ?? "0";
            var mv = cueData?.MarginV.ToString(CultureInfo.InvariantCulture) ?? "0";
            var effect = cueData?.Effect ?? string.Empty;

            // In ASS text, hard newlines must be emitted as \N
            var text = cue.RawText.Replace("\r\n", "\\N").Replace("\n", "\\N");

            writer.Write($"{eventType}: {layerOrMarked},{startStr},{endStr},{styleName},{actor},{ml},{mr},{mv},{effect},{text}");
            writer.Write(le);
        }
    }

    private static string FormatStyleLine(AssStyle style, bool isSsa)
    {
        var b = style.Bold ? "-1" : "0";
        var it = style.Italic ? "-1" : "0";
        var u = style.Underline ? "-1" : "0";
        var so = style.StrikeOut ? "-1" : "0";

        if (isSsa)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Style: {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},0,{16}",
                style.Name, style.FontName, style.FontSize, style.PrimaryColour, style.SecondaryColour,
                style.OutlineColour, style.BackColour, b, it, style.BorderStyle, style.Outline,
                style.Shadow, style.Alignment, style.MarginL, style.MarginR, style.MarginV, style.Encoding);
        }

        return string.Format(CultureInfo.InvariantCulture,
            "Style: {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22}",
            style.Name, style.FontName, style.FontSize, style.PrimaryColour, style.SecondaryColour,
            style.OutlineColour, style.BackColour, b, it, u, so, style.ScaleX, style.ScaleY,
            style.Spacing, style.Angle, style.BorderStyle, style.Outline, style.Shadow,
            style.Alignment, style.MarginL, style.MarginR, style.MarginV, style.Encoding);
    }
}
