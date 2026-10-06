using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;

namespace SubtitleToolkit.Common;

/// <summary>
/// High-fidelity conversion engine between subtitle formats with structured loss reporting.
/// </summary>
public static class SubtitleConverter
{
    public static ConversionResult Convert(
        SubtitleDocument doc,
        SubtitleFormat targetFormat,
        ConversionOptions? options = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        options ??= new ConversionOptions();

        if (doc.Format == targetFormat)
        {
            return new ConversionResult(doc, new ConversionReport());
        }

        var notices = new List<ConversionNotice>();
        var convertedDoc = (doc.Format, targetFormat) switch
        {
            (SubtitleFormat.SubRip, SubtitleFormat.WebVtt) => ConvertSrtToVtt(doc, notices),
            (SubtitleFormat.WebVtt, SubtitleFormat.SubRip) => ConvertVttToSrt(doc, notices),
            (SubtitleFormat.Ass or SubtitleFormat.Ssa, SubtitleFormat.SubRip) => ConvertAssToSrt(doc, notices),
            (SubtitleFormat.SubRip, SubtitleFormat.Ass) => ConvertSrtToAss(doc, options, notices, isSsa: false),
            (SubtitleFormat.SubRip, SubtitleFormat.Ssa) => ConvertSrtToAss(doc, options, notices, isSsa: true),
            (SubtitleFormat.Ass or SubtitleFormat.Ssa, SubtitleFormat.WebVtt) => ConvertAssToVtt(doc, options, notices),
            (SubtitleFormat.WebVtt, SubtitleFormat.Ass) => ConvertVttToAss(doc, options, notices, isSsa: false),
            (SubtitleFormat.WebVtt, SubtitleFormat.Ssa) => ConvertVttToAss(doc, options, notices, isSsa: true),
            (SubtitleFormat.Ass, SubtitleFormat.Ssa) => ConvertAssToSsa(doc, notices),
            (SubtitleFormat.Ssa, SubtitleFormat.Ass) => ConvertSsaToAss(doc, notices),
            _ => throw new NotSupportedException($"Conversion from {doc.Format} to {targetFormat} is not supported.")
        };

        var report = new ConversionReport(notices);
        if (options.ThrowOnLoss && (report.HasWarnings || report.HasErrors))
        {
            var warningMessages = string.Join("; ", notices.Where(n => n.Severity >= ConversionSeverity.Warning).Select(n => n.Message));
            throw new SubtitleConversionException(report, $"Conversion from {doc.Format} to {targetFormat} resulted in data loss: {warningMessages}");
        }

        return new ConversionResult(convertedDoc, report);
    }

    #region SRT <-> VTT

    private static SubtitleDocument ConvertSrtToVtt(SubtitleDocument doc, List<ConversionNotice> notices)
    {
        if (doc.FormatData != null)
        {
            notices.Add(new ConversionNotice(
                ConversionSeverity.Info,
                ConversionNoticeCode.StaleFormatDataDropped,
                "Stripped source document format metadata during conversion to WebVTT."));
        }

        var newCues = new List<SubtitleCue>(doc.Cues.Count);
        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var rawText = cue.RawText;
            VttCueSettings? settings = null;

            // Check for leading {\anX} in SRT
            var anMatch = Regex.Match(rawText, @"^\{\\an([1-9])\}");
            if (anMatch.Success)
            {
                var an = int.Parse(anMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                settings = MapAnToVttSettings(an);
                rawText = rawText.Substring(anMatch.Length).TrimStart();
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Info,
                    ConversionNoticeCode.PositioningConverted,
                    $"Converted SRT {{\\an{an}}} to WebVTT cue settings.",
                    i,
                    @"{\anX}"));
            }

            var cueData = settings != null ? new VttCueData(null, settings.ToSettingsString()) : null;
            newCues.Add(new SubtitleCue(cue.Start, cue.End, rawText, cueData));
        }

        // WebVTT requires non-decreasing start times
        newCues = newCues.OrderBy(c => c.Start).ToList();
        return new SubtitleDocument(SubtitleFormat.WebVtt, newCues, new VttDocumentData());
    }

    private static SubtitleDocument ConvertVttToSrt(SubtitleDocument doc, List<ConversionNotice> notices)
    {
        if (doc.FormatData is VttDocumentData vttData)
        {
            if (vttData.NonCueBlocks.Count > 0)
            {
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Warning,
                    ConversionNoticeCode.NonCueBlocksDropped,
                    $"Dropped {vttData.NonCueBlocks.Count} WebVTT non-cue blocks (NOTE/STYLE/REGION)."));
            }

            if (!string.IsNullOrEmpty(vttData.TimestampMap))
            {
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Info,
                    ConversionNoticeCode.StaleFormatDataDropped,
                    "Dropped WebVTT X-TIMESTAMP-MAP header."));
            }
        }

        var newCues = new List<SubtitleCue>(doc.Cues.Count);
        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var text = cue.RawText;

            // Convert <v Speaker>Text</v> or <v Speaker>Text
            var voiceMatch = Regex.Match(text, @"<v\s+([^>]+)>(.*?)(</v>)?", RegexOptions.Singleline);
            if (voiceMatch.Success)
            {
                var speaker = voiceMatch.Groups[1].Value.Trim();
                var dialogue = voiceMatch.Groups[2].Value.Trim();
                text = Regex.Replace(text, @"<v\s+[^>]+>(.*?)(</v>)?", $"{speaker}: $1", RegexOptions.Singleline);
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Info,
                    ConversionNoticeCode.VoiceTagConverted,
                    $"Converted WebVTT <v {speaker}> voice tag to speaker label prefix.",
                    i,
                    "<v>"));
            }

            // Convert cue settings to {\anX} if present
            if (cue.FormatData is VttCueData vttCue && vttCue.Settings != null)
            {
                var an = MapVttSettingsToAn(vttCue.Settings);
                if (an.HasValue)
                {
                    text = $"{{\\an{an.Value}}}" + text;
                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Info,
                        ConversionNoticeCode.PositioningConverted,
                        $"Converted WebVTT cue settings to SRT {{\\an{an.Value}}}.",
                        i,
                        "settings"));
                }
            }

            newCues.Add(new SubtitleCue(cue.Start, cue.End, text, null));
        }

        return new SubtitleDocument(SubtitleFormat.SubRip, newCues, null);
    }

    #endregion

    #region ASS/SSA <-> SRT

    private static SubtitleDocument ConvertAssToSrt(SubtitleDocument doc, List<ConversionNotice> notices)
    {
        if (doc.FormatData is AssDocumentData assDoc)
        {
            if (assDoc.Styles.Count > 0)
            {
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Warning,
                    ConversionNoticeCode.StyleTableDropped,
                    $"Dropped {assDoc.Styles.Count} ASS/SSA style definitions."));
            }

            if (assDoc.ScriptInfo.Count > 0)
            {
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Info,
                    ConversionNoticeCode.ScriptInfoDropped,
                    "Dropped ASS/SSA [Script Info] header metadata."));
            }

            if (assDoc.UnknownSections.Count > 0)
            {
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Info,
                    ConversionNoticeCode.StaleFormatDataDropped,
                    $"Dropped {assDoc.UnknownSections.Count} unparsed ASS sections."));
            }
        }

        var newCues = new List<SubtitleCue>(doc.Cues.Count);
        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var (convertedText, droppedFeatures) = ConvertAssTextToHtml(cue.RawText);

            foreach (var feat in droppedFeatures)
            {
                if (feat == "drawing")
                {
                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Warning,
                        ConversionNoticeCode.DrawingDropped,
                        "Dropped ASS vector drawing commands (\\p1...\\p0).",
                        i,
                        "\\p1"));
                }
                else if (feat == "pos" || feat == "move")
                {
                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Warning,
                        ConversionNoticeCode.PositioningDropped,
                        $"Dropped ASS {feat} positioning tag.",
                        i,
                        $"\\{feat}"));
                }
                else
                {
                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Info,
                        ConversionNoticeCode.UnsupportedTagStripped,
                        $"Stripped unsupported ASS tag '{feat}'.",
                        i,
                        feat));
                }
            }

            newCues.Add(new SubtitleCue(cue.Start, cue.End, convertedText, null));
        }

        return new SubtitleDocument(SubtitleFormat.SubRip, newCues, null);
    }

    private static SubtitleDocument ConvertSrtToAss(
        SubtitleDocument doc,
        ConversionOptions options,
        List<ConversionNotice> notices,
        bool isSsa)
    {
        var scriptInfo = new Dictionary<string, string>
        {
            ["Title"] = "Converted by SubtitleToolkit",
            ["ScriptType"] = isSsa ? "v4.00" : "v4.00+",
            ["WrapStyle"] = "0",
            ["PlayResX"] = options.TargetPlayResX.ToString(CultureInfo.InvariantCulture),
            ["PlayResY"] = options.TargetPlayResY.ToString(CultureInfo.InvariantCulture)
        };

        var defaultStyle = new AssStyle
        {
            Name = options.DefaultStyleName,
            Alignment = 2,
            FontSize = 20.0
        };

        var docData = new AssDocumentData(scriptInfo, new[] { defaultStyle }, null, isSsa);
        var newCues = new List<SubtitleCue>(doc.Cues.Count);

        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var assText = ConvertHtmlTextToAss(cue.RawText, isSsa);
            var cueData = new AssCueData
            {
                IsSsa = isSsa,
                StyleName = options.DefaultStyleName,
                EventType = AssEventType.Dialogue
            };

            newCues.Add(new SubtitleCue(cue.Start, cue.End, assText, cueData));
        }

        return new SubtitleDocument(isSsa ? SubtitleFormat.Ssa : SubtitleFormat.Ass, newCues, docData);
    }

    #endregion

    #region ASS/SSA <-> VTT

    private static SubtitleDocument ConvertAssToVtt(
        SubtitleDocument doc,
        ConversionOptions options,
        List<ConversionNotice> notices)
    {
        var assDoc = doc.FormatData as AssDocumentData;
        if (assDoc?.Styles.Count > 0)
        {
            notices.Add(new ConversionNotice(
                ConversionSeverity.Warning,
                ConversionNoticeCode.StyleTableDropped,
                $"Dropped {assDoc.Styles.Count} ASS/SSA style definitions."));
        }

        var playResX = GetPlayResX(assDoc, options.TargetPlayResX);
        var playResY = GetPlayResY(assDoc, options.TargetPlayResY);

        var newCues = new List<SubtitleCue>(doc.Cues.Count);
        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var rawText = cue.RawText;

            // Extract \pos(x, y) if present
            VttCueSettings? settings = null;
            var posMatch = Regex.Match(rawText, @"\\pos\(\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\)");
            if (posMatch.Success)
            {
                if (double.TryParse(posMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    && double.TryParse(posMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    var pctX = Math.Round(Math.Max(0.0, Math.Min(100.0, (x / playResX) * 100.0)), 2);
                    var pctY = Math.Round(Math.Max(0.0, Math.Min(100.0, (y / playResY) * 100.0)), 2);

                    // Check \anX or default to 2
                    var an = 2;
                    var anMatch = Regex.Match(rawText, @"\\an([1-9])");
                    if (anMatch.Success) an = int.Parse(anMatch.Groups[1].Value, CultureInfo.InvariantCulture);

                    var align = an switch
                    {
                        1 or 4 or 7 => VttAlignment.Left,
                        3 or 6 or 9 => VttAlignment.Right,
                        _ => VttAlignment.Center
                    };

                    var lineAlign = an switch
                    {
                        7 or 8 or 9 => VttLineAlign.Start,
                        4 or 5 or 6 => VttLineAlign.Center,
                        _ => VttLineAlign.End
                    };

                    settings = new VttCueSettings(
                        position: new VttPositionSetting { Percentage = pctX },
                        line: new VttLineSetting { Value = pctY, IsPercentage = true, Alignment = lineAlign },
                        align: align);

                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Info,
                        ConversionNoticeCode.PositioningConverted,
                        $"Mapped ASS \\pos({x},{y}) to VTT position:{pctX}% line:{pctY}%.",
                        i,
                        "\\pos"));
                }
            }

            var (convertedText, droppedFeatures) = ConvertAssTextToHtml(rawText);
            foreach (var feat in droppedFeatures)
            {
                if (feat == "drawing")
                {
                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Warning,
                        ConversionNoticeCode.DrawingDropped,
                        "Dropped ASS vector drawing commands.",
                        i,
                        "\\p1"));
                }
                else if (feat != "pos" && feat != "an")
                {
                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Info,
                        ConversionNoticeCode.UnsupportedTagStripped,
                        $"Stripped unsupported ASS tag '{feat}'.",
                        i,
                        feat));
                }
            }

            var cueData = settings != null ? new VttCueData(null, settings.ToSettingsString()) : null;
            newCues.Add(new SubtitleCue(cue.Start, cue.End, convertedText, cueData));
        }

        newCues = newCues.OrderBy(c => c.Start).ToList();
        return new SubtitleDocument(SubtitleFormat.WebVtt, newCues, new VttDocumentData());
    }

    private static SubtitleDocument ConvertVttToAss(
        SubtitleDocument doc,
        ConversionOptions options,
        List<ConversionNotice> notices,
        bool isSsa)
    {
        var playResX = options.TargetPlayResX;
        var playResY = options.TargetPlayResY;

        var scriptInfo = new Dictionary<string, string>
        {
            ["Title"] = "Converted from WebVTT",
            ["ScriptType"] = isSsa ? "v4.00" : "v4.00+",
            ["WrapStyle"] = "0",
            ["PlayResX"] = playResX.ToString(CultureInfo.InvariantCulture),
            ["PlayResY"] = playResY.ToString(CultureInfo.InvariantCulture)
        };

        var defaultStyle = new AssStyle { Name = options.DefaultStyleName, Alignment = 2 };
        var docData = new AssDocumentData(scriptInfo, new[] { defaultStyle }, null, isSsa);

        var newCues = new List<SubtitleCue>(doc.Cues.Count);
        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var text = cue.RawText;
            string? actor = null;

            // Extract voice tag <v Speaker>
            var vMatch = Regex.Match(text, @"<v\s+([^>]+)>(.*?)(</v>)?", RegexOptions.Singleline);
            if (vMatch.Success)
            {
                actor = vMatch.Groups[1].Value.Trim();
                text = Regex.Replace(text, @"<v\s+[^>]+>(.*?)(</v>)?", "$1", RegexOptions.Singleline);
                notices.Add(new ConversionNotice(
                    ConversionSeverity.Info,
                    ConversionNoticeCode.VoiceTagConverted,
                    $"Mapped WebVTT voice '{actor}' to ASS Actor field.",
                    i,
                    "<v>"));
            }

            // Map VTT positioning to \pos and \an
            var posTag = string.Empty;
            if (cue.FormatData is VttCueData vttCue && vttCue.Settings != null)
            {
                var settings = vttCue.Settings;
                double? posX = null;
                double? posY = null;

                if (settings.Position != null)
                {
                    posX = (settings.Position.Percentage / 100.0) * playResX;
                }

                if (settings.Line != null)
                {
                    if (settings.Line.IsPercentage)
                    {
                        posY = (settings.Line.Value / 100.0) * playResY;
                    }
                    else
                    {
                        // Snap-to-lines heuristic
                        var lineH = playResY / 15.0;
                        posY = settings.Line.Value >= 0
                            ? (settings.Line.Value + 1) * lineH
                            : playResY + (settings.Line.Value * lineH);

                        notices.Add(new ConversionNotice(
                            ConversionSeverity.Info,
                            ConversionNoticeCode.SnapToLinesConverted,
                            $"Mapped WebVTT snap-to-lines line:{settings.Line.Value} to Y coordinate {posY:0.#}.",
                            i,
                            "line"));
                    }
                }

                var an = MapVttSettingsToAn(settings) ?? 2;
                if (posX.HasValue || posY.HasValue)
                {
                    var finalX = (int)Math.Round(posX ?? (playResX / 2.0));
                    var finalY = (int)Math.Round(posY ?? (playResY * 0.9));
                    var anPrefix = isSsa ? $"\\a{MapAssAnToSsa(an)}" : $"\\an{an}";
                    posTag = $"{{\\{anPrefix}\\pos({finalX},{finalY})}}";

                    notices.Add(new ConversionNotice(
                        ConversionSeverity.Info,
                        ConversionNoticeCode.PositioningConverted,
                        $"Mapped WebVTT cue settings to ASS \\pos({finalX},{finalY}).",
                        i,
                        "settings"));
                }
            }

            var assContent = ConvertHtmlTextToAss(text, isSsa);
            var fullAssText = posTag + assContent;

            var cueData = new AssCueData
            {
                IsSsa = isSsa,
                StyleName = options.DefaultStyleName,
                ActorName = actor ?? string.Empty
            };

            newCues.Add(new SubtitleCue(cue.Start, cue.End, fullAssText, cueData));
        }

        return new SubtitleDocument(isSsa ? SubtitleFormat.Ssa : SubtitleFormat.Ass, newCues, docData);
    }

    #endregion

    #region ASS <-> SSA

    private static SubtitleDocument ConvertAssToSsa(SubtitleDocument doc, List<ConversionNotice> notices)
    {
        var assDoc = doc.FormatData as AssDocumentData;
        var ssaStyles = new List<AssStyle>();

        if (assDoc?.Styles != null)
        {
            foreach (var st in assDoc.Styles)
            {
                var ssaAlignment = MapAssAnToSsa(st.Alignment);
                ssaStyles.Add(new AssStyle
                {
                    Name = st.Name,
                    FontName = st.FontName,
                    FontSize = st.FontSize,
                    PrimaryColour = st.PrimaryColour,
                    SecondaryColour = st.SecondaryColour,
                    OutlineColour = st.OutlineColour,
                    BackColour = st.BackColour,
                    Bold = st.Bold,
                    Italic = st.Italic,
                    Underline = st.Underline,
                    StrikeOut = st.StrikeOut,
                    ScaleX = st.ScaleX,
                    ScaleY = st.ScaleY,
                    Spacing = st.Spacing,
                    Angle = st.Angle,
                    BorderStyle = st.BorderStyle,
                    Outline = st.Outline,
                    Shadow = st.Shadow,
                    Alignment = ssaAlignment,
                    MarginL = st.MarginL,
                    MarginR = st.MarginR,
                    MarginV = st.MarginV,
                    Encoding = st.Encoding
                });
            }
        }

        var scriptInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (assDoc?.ScriptInfo != null)
        {
            foreach (var kvp in assDoc.ScriptInfo) scriptInfo[kvp.Key] = kvp.Value;
        }
        scriptInfo["ScriptType"] = "v4.00";

        var ssaDocData = new AssDocumentData(scriptInfo, ssaStyles, assDoc?.UnknownSections, isSsa: true);
        var newCues = new List<SubtitleCue>(doc.Cues.Count);

        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var cueData = cue.FormatData as AssCueData;

            // Replace \anX with \aY
            var convertedText = Regex.Replace(cue.RawText, @"\\an([1-9])", m =>
            {
                var an = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                return $"\\a{MapAssAnToSsa(an)}";
            });

            var ssaCueData = new AssCueData
            {
                IsSsa = true,
                Marked = (cueData?.Layer ?? 0) > 0,
                StyleName = cueData?.StyleName ?? "Default",
                ActorName = cueData?.ActorName ?? string.Empty,
                MarginL = cueData?.MarginL ?? 0,
                MarginR = cueData?.MarginR ?? 0,
                MarginV = cueData?.MarginV ?? 0,
                Effect = cueData?.Effect ?? string.Empty,
                EventType = cueData?.EventType ?? AssEventType.Dialogue
            };

            newCues.Add(new SubtitleCue(cue.Start, cue.End, convertedText, ssaCueData));
        }

        notices.Add(new ConversionNotice(
            ConversionSeverity.Info,
            ConversionNoticeCode.AlignmentConverted,
            "Mapped ASS numpad alignments (1-9) to SSA alignments (1-11)."));

        return new SubtitleDocument(SubtitleFormat.Ssa, newCues, ssaDocData);
    }

    private static SubtitleDocument ConvertSsaToAss(SubtitleDocument doc, List<ConversionNotice> notices)
    {
        var ssaDoc = doc.FormatData as AssDocumentData;
        var assStyles = new List<AssStyle>();

        if (ssaDoc?.Styles != null)
        {
            foreach (var st in ssaDoc.Styles)
            {
                var assAlignment = MapSsaToAssAn(st.Alignment);
                assStyles.Add(new AssStyle
                {
                    Name = st.Name,
                    FontName = st.FontName,
                    FontSize = st.FontSize,
                    PrimaryColour = st.PrimaryColour,
                    SecondaryColour = st.SecondaryColour,
                    OutlineColour = st.OutlineColour,
                    BackColour = st.BackColour,
                    Bold = st.Bold,
                    Italic = st.Italic,
                    Underline = st.Underline,
                    StrikeOut = st.StrikeOut,
                    ScaleX = st.ScaleX,
                    ScaleY = st.ScaleY,
                    Spacing = st.Spacing,
                    Angle = st.Angle,
                    BorderStyle = st.BorderStyle,
                    Outline = st.Outline,
                    Shadow = st.Shadow,
                    Alignment = assAlignment,
                    MarginL = st.MarginL,
                    MarginR = st.MarginR,
                    MarginV = st.MarginV,
                    Encoding = st.Encoding
                });
            }
        }

        var scriptInfo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ssaDoc?.ScriptInfo != null)
        {
            foreach (var kvp in ssaDoc.ScriptInfo) scriptInfo[kvp.Key] = kvp.Value;
        }
        scriptInfo["ScriptType"] = "v4.00+";

        var assDocData = new AssDocumentData(scriptInfo, assStyles, ssaDoc?.UnknownSections, isSsa: false);
        var newCues = new List<SubtitleCue>(doc.Cues.Count);

        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var cueData = cue.FormatData as AssCueData;

            // Replace \aX with \anY
            var convertedText = Regex.Replace(cue.RawText, @"\\a([1-9]|10|11)", m =>
            {
                var ssaAlign = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                return $"\\an{MapSsaToAssAn(ssaAlign)}";
            });

            var assCueData = new AssCueData
            {
                IsSsa = false,
                Layer = (cueData?.Marked ?? false) ? 1 : 0,
                StyleName = cueData?.StyleName ?? "Default",
                ActorName = cueData?.ActorName ?? string.Empty,
                MarginL = cueData?.MarginL ?? 0,
                MarginR = cueData?.MarginR ?? 0,
                MarginV = cueData?.MarginV ?? 0,
                Effect = cueData?.Effect ?? string.Empty,
                EventType = cueData?.EventType ?? AssEventType.Dialogue
            };

            newCues.Add(new SubtitleCue(cue.Start, cue.End, convertedText, assCueData));
        }

        notices.Add(new ConversionNotice(
            ConversionSeverity.Info,
            ConversionNoticeCode.AlignmentConverted,
            "Mapped SSA alignments (1-11) to ASS numpad alignments (1-9)."));

        return new SubtitleDocument(SubtitleFormat.Ass, newCues, assDocData);
    }

    #endregion

    #region Helper Methods

    private static (string ConvertedText, List<string> DroppedFeatures) ConvertAssTextToHtml(string text)
    {
        var dropped = new List<string>();

        // Strip drawings between \p1..\p9 and \p0
        if (Regex.IsMatch(text, @"\\p[1-9]"))
        {
            dropped.Add("drawing");
            text = Regex.Replace(text, @"\{\\p[1-9][^}]*\}.*?(\{\\p0[^}]*\}|$)", string.Empty);
        }

        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] == '{')
            {
                var end = text.IndexOf('}', i);
                if (end < 0) { sb.Append(text.Substring(i)); break; }

                var block = text.Substring(i + 1, end - i - 1);
                i = end + 1;

                // Parse known tags in block
                if (block.Contains(@"\b1")) sb.Append("<b>");
                if (block.Contains(@"\b0")) sb.Append("</b>");
                if (block.Contains(@"\i1")) sb.Append("<i>");
                if (block.Contains(@"\i0")) sb.Append("</i>");
                if (block.Contains(@"\u1")) sb.Append("<u>");
                if (block.Contains(@"\u0")) sb.Append("</u>");
                if (block.Contains(@"\s1")) sb.Append("<s>");
                if (block.Contains(@"\s0")) sb.Append("</s>");

                var colorMatch = Regex.Match(block, @"\\(?:1c|c)&H([0-9a-fA-F]{6})&");
                if (colorMatch.Success)
                {
                    var hex = colorMatch.Groups[1].Value;
                    // ASS is BGR, convert to RGB
                    var b = hex.Substring(0, 2);
                    var g = hex.Substring(2, 2);
                    var r = hex.Substring(4, 2);
                    sb.Append($"<font color=\"#{r}{g}{b}\">");
                }

                if (block.Contains(@"\pos(")) dropped.Add("pos");
                if (block.Contains(@"\move(")) dropped.Add("move");
                if (block.Contains(@"\clip(") || block.Contains(@"\iclip(")) dropped.Add("clip");
                if (block.Contains(@"\t(")) dropped.Add("t");
                if (block.Contains(@"\fade(") || block.Contains(@"\fad(")) dropped.Add("fade");
                if (block.Contains(@"\blur") || block.Contains(@"\be")) dropped.Add("blur");

                continue;
            }

            if (text[i] == '\\' && i + 1 < text.Length)
            {
                if (text[i + 1] is 'N' or 'n')
                {
                    sb.Append('\n');
                    i += 2;
                    continue;
                }
                if (text[i + 1] is 'h')
                {
                    sb.Append(' ');
                    i += 2;
                    continue;
                }
            }

            sb.Append(text[i]);
            i++;
        }

        return (sb.ToString(), dropped.Distinct().ToList());
    }

    private static string ConvertHtmlTextToAss(string text, bool isSsa)
    {
        var result = text
            .Replace("<b>", "{\\b1}")
            .Replace("</b>", "{\\b0}")
            .Replace("<i>", "{\\i1}")
            .Replace("</i>", "{\\i0}")
            .Replace("<u>", "{\\u1}")
            .Replace("</u>", "{\\u0}")
            .Replace("<s>", "{\\s1}")
            .Replace("</s>", "{\\s0}");

        // Convert <font color="#RRGGBB"> to {\c&HBBGGRR&}
        result = Regex.Replace(result, @"<font\s+color=""#([0-9a-fA-F]{2})([0-9a-fA-F]{2})([0-9a-fA-F]{2})""\s*>", m =>
        {
            var r = m.Groups[1].Value;
            var g = m.Groups[2].Value;
            var b = m.Groups[3].Value;
            return $"{{\\c&H{b}{g}{r}&}}";
        });
        result = Regex.Replace(result, @"</font>", string.Empty);

        // Convert newlines to \N for ASS dialogue
        result = result.Replace("\r\n", "\\N").Replace("\n", "\\N");
        return result;
    }

    private static VttCueSettings MapAnToVttSettings(int an)
    {
        var align = an switch
        {
            1 or 4 or 7 => VttAlignment.Left,
            3 or 6 or 9 => VttAlignment.Right,
            _ => VttAlignment.Center
        };

        var line = an switch
        {
            7 or 8 or 9 => new VttLineSetting { Value = 10, IsPercentage = true, Alignment = VttLineAlign.Start },
            4 or 5 or 6 => new VttLineSetting { Value = 50, IsPercentage = true, Alignment = VttLineAlign.Center },
            _ => new VttLineSetting { Value = 90, IsPercentage = true, Alignment = VttLineAlign.End }
        };

        return new VttCueSettings(align: align, line: line);
    }

    private static int? MapVttSettingsToAn(VttCueSettings settings)
    {
        int horiz = 2; // Center
        if (settings.Align.HasValue)
        {
            horiz = settings.Align.Value switch
            {
                VttAlignment.Left or VttAlignment.Start => 1,
                VttAlignment.Right or VttAlignment.End => 3,
                _ => 2
            };
        }

        int vert = 0; // Bottom
        if (settings.Line != null)
        {
            var val = settings.Line.Value;
            if (settings.Line.IsPercentage)
            {
                if (val <= 33) vert = 2;      // Top
                else if (val <= 66) vert = 1; // Middle
                else vert = 0;               // Bottom
            }
            else
            {
                vert = val >= 0 && val <= 3 ? 2 : 0;
            }
        }

        // Return numpad 1-9
        return (vert * 3) + horiz;
    }

    private static int MapAssAnToSsa(int an) => an switch
    {
        1 => 1, 2 => 2, 3 => 3,
        4 => 9, 5 => 10, 6 => 11,
        7 => 5, 8 => 6, 9 => 7,
        _ => 2
    };

    private static int MapSsaToAssAn(int ssa) => ssa switch
    {
        1 => 1, 2 => 2, 3 => 3,
        5 => 7, 6 => 8, 7 => 9,
        9 => 4, 10 => 5, 11 => 6,
        _ => 2
    };

    private static int GetPlayResX(AssDocumentData? data, int defaultVal)
    {
        if (data?.ScriptInfo != null
            && data.ScriptInfo.TryGetValue("PlayResX", out var val)
            && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }
        return defaultVal;
    }

    private static int GetPlayResY(AssDocumentData? data, int defaultVal)
    {
        if (data?.ScriptInfo != null
            && data.ScriptInfo.TryGetValue("PlayResY", out var val)
            && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }
        return defaultVal;
    }

    #endregion
}
