using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SubtitleToolkit.Common;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;

namespace SubtitleToolkit.Formats;

/// <summary>
/// Parser and serializer for W3C WebVTT (.vtt) subtitle files.
/// </summary>
public sealed class VttHandler : ISubtitleHandler
{
    public SubtitleFormat Format => SubtitleFormat.WebVtt;

    public ParseResult Parse(string content, SubtitleReadOptions options)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        var cues = new List<SubtitleCue>();
        var diagnostics = new List<ParseDiagnostic>();
        var nonCueBlocks = new List<VttAnchoredBlock>();

        // Normalize lines
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var lineIndex = 0;

        // Skip leading blank lines
        while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
            lineIndex++;

        if (lineIndex >= lines.Length)
        {
            var msg = "File is empty or contains only whitespace.";
            if (options.Mode == ParseMode.Strict)
                throw new SubtitleParseException(msg, 1);

            return new ParseResult(new SubtitleDocument(SubtitleFormat.WebVtt, cues), diagnostics);
        }

        // 1. Verify WEBVTT signature header
        var headerLine = lines[lineIndex].Trim();
        string? headerComment = null;

        if (!headerLine.StartsWith("WEBVTT", StringComparison.Ordinal))
        {
            var msg = $"Missing 'WEBVTT' signature header. Found: '{headerLine}'";
            if (options.Mode == ParseMode.Strict)
                throw new SubtitleParseException(msg, lineIndex + 1);

            diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, lineIndex + 1, "VTT_MISSING_HEADER", msg));
        }
        else
        {
            if (headerLine.Length > 6 && (headerLine[6] == ' ' || headerLine[6] == '\t'))
            {
                headerComment = headerLine.Substring(6).Trim();
            }
            lineIndex++;
        }

        string? timestampMap = null;

        // 2. Parse body: header metadata, NOTE/STYLE/REGION blocks, and Cues
        while (lineIndex < lines.Length)
        {
            // Skip empty lines
            while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
                lineIndex++;

            if (lineIndex >= lines.Length)
                break;

            var current = lines[lineIndex].Trim();

            // Check X-TIMESTAMP-MAP
            if (current.StartsWith("X-TIMESTAMP-MAP=", StringComparison.OrdinalIgnoreCase))
            {
                timestampMap = current;
                lineIndex++;
                continue;
            }

            // Check NOTE block
            if (current.StartsWith("NOTE", StringComparison.Ordinal))
            {
                var blockLines = new List<string>();
                var inlineContent = current.Length > 4 ? current.Substring(4).Trim() : string.Empty;
                if (!string.IsNullOrEmpty(inlineContent))
                    blockLines.Add(inlineContent);

                lineIndex++;
                while (lineIndex < lines.Length && !string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    blockLines.Add(lines[lineIndex]);
                    lineIndex++;
                }

                var contentStr = string.Join("\n", blockLines.ToArray());
                nonCueBlocks.Add(new VttAnchoredBlock(cues.Count, VttBlockKind.Note, contentStr));
                continue;
            }

            // Check STYLE block
            if (string.Equals(current, "STYLE", StringComparison.Ordinal) || current.StartsWith("STYLE ", StringComparison.Ordinal))
            {
                var blockLines = new List<string>();
                lineIndex++;
                while (lineIndex < lines.Length && !string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    blockLines.Add(lines[lineIndex]);
                    lineIndex++;
                }

                var contentStr = string.Join("\n", blockLines.ToArray());
                nonCueBlocks.Add(new VttAnchoredBlock(cues.Count, VttBlockKind.Style, contentStr));
                continue;
            }

            // Check REGION block
            if (string.Equals(current, "REGION", StringComparison.Ordinal) || current.StartsWith("REGION ", StringComparison.Ordinal))
            {
                var blockLines = new List<string>();
                lineIndex++;
                while (lineIndex < lines.Length && !string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    blockLines.Add(lines[lineIndex]);
                    lineIndex++;
                }

                var contentStr = string.Join("\n", blockLines.ToArray());
                nonCueBlocks.Add(new VttAnchoredBlock(cues.Count, VttBlockKind.Region, contentStr));
                continue;
            }

            // Otherwise, it must be a cue! Either identifier or timestamp line directly.
            string? cueId = null;
            string timestampLine;
            var cueStartLine = lineIndex + 1;

            if (current.Contains("-->"))
            {
                timestampLine = current;
                lineIndex++;
            }
            else
            {
                cueId = current;
                lineIndex++;
                while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
                    lineIndex++;

                if (lineIndex >= lines.Length)
                    break;

                timestampLine = lines[lineIndex].Trim();
                lineIndex++;
            }

            if (!timestampLine.Contains("-->"))
            {
                var msg = $"Expected WebVTT timestamp with '-->' at line {cueStartLine}. Found: '{timestampLine}'";
                if (options.Mode == ParseMode.Strict)
                    throw new SubtitleParseException(msg, cueStartLine);

                diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, cueStartLine, "VTT_BAD_TIMESTAMP", msg));
                continue;
            }

            var arrowIdx = timestampLine.IndexOf("-->", StringComparison.Ordinal);
            var startStr = timestampLine.Substring(0, arrowIdx).Trim();
            var afterArrow = timestampLine.Substring(arrowIdx + 3).Trim();

            string endStr;
            string? rawSettings = null;

            var firstSpace = afterArrow.IndexOf(' ');
            if (firstSpace > 0)
            {
                endStr = afterArrow.Substring(0, firstSpace).Trim();
                rawSettings = afterArrow.Substring(firstSpace + 1).Trim();
            }
            else
            {
                endStr = afterArrow;
            }

            if (!TimestampHelper.TryParseVttTimestamp(startStr, out var startTime)
                || !TimestampHelper.TryParseVttTimestamp(endStr, out var endTime))
            {
                var msg = $"Invalid WebVTT timestamp in '{timestampLine}' at line {cueStartLine}.";
                if (options.Mode == ParseMode.Strict)
                    throw new SubtitleParseException(msg, cueStartLine);

                diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, cueStartLine, "VTT_TIMESTAMP_FORMAT", msg));
                continue;
            }

            // Read cue text lines
            var textLines = new List<string>();
            while (lineIndex < lines.Length && !string.IsNullOrWhiteSpace(lines[lineIndex]))
            {
                textLines.Add(lines[lineIndex]);
                lineIndex++;
            }

            var rawText = string.Join("\n", textLines.ToArray());
            var vttCueData = new VttCueData(cueId, rawSettings);
            cues.Add(new SubtitleCue(startTime, endTime, rawText, vttCueData));
        }

        var docData = new VttDocumentData(headerComment, timestampMap, nonCueBlocks);
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues, docData);
        return new ParseResult(doc, diagnostics);
    }

    public void Write(SubtitleDocument doc, TextWriter writer, SubtitleWriteOptions options)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        var le = options.LineEnding;
        var vttData = doc.FormatData as VttDocumentData;

        // 1. Signature
        writer.Write("WEBVTT");
        if (!string.IsNullOrEmpty(vttData?.HeaderComment))
        {
            writer.Write(" ");
            writer.Write(vttData!.HeaderComment);
        }
        writer.Write(le);

        // 2. Timestamp map if present
        if (!string.IsNullOrEmpty(vttData?.TimestampMap))
        {
            writer.Write(vttData!.TimestampMap);
            writer.Write(le);
        }

        writer.Write(le);

        // Group anchored blocks by BeforeCueIndex
        var nonCueBlocks = vttData?.NonCueBlocks ?? Array.Empty<VttAnchoredBlock>();
        var blockGroups = nonCueBlocks.GroupBy(b => b.BeforeCueIndex).ToDictionary(g => g.Key, g => g.ToList());

        // Emit blocks before cue 0 (header area)
        EmitAnchoredBlocks(blockGroups, 0, writer, le);
        EmitAnchoredBlocks(blockGroups, -1, writer, le);

        // WebVTT requires non-decreasing start times
        for (var i = 0; i < doc.Cues.Count; i++)
        {
            // Emit any non-cue blocks anchored before this cue
            if (i > 0)
            {
                EmitAnchoredBlocks(blockGroups, i, writer, le);
            }

            var cue = doc.Cues[i];
            var cueData = cue.FormatData as VttCueData;

            // Identifier
            if (!string.IsNullOrEmpty(cueData?.Identifier))
            {
                writer.Write(cueData!.Identifier);
                writer.Write(le);
            }

            // Timestamps + settings
            writer.Write(TimestampHelper.FormatVtt(cue.Start));
            writer.Write(" --> ");
            writer.Write(TimestampHelper.FormatVtt(cue.End));

            var settingsStr = !string.IsNullOrEmpty(cueData?.RawSettings)
                ? cueData!.RawSettings
                : cueData?.Settings?.ToSettingsString();

            if (!string.IsNullOrEmpty(settingsStr))
            {
                writer.Write(" ");
                writer.Write(settingsStr);
            }
            writer.Write(le);

            // RawText (with \n converted to configured line ending)
            if (!string.IsNullOrEmpty(cue.RawText))
            {
                writer.Write(cue.RawText.Replace("\n", le));
                writer.Write(le);
            }

            // Blank line after cue
            writer.Write(le);
        }

        // Emit trailing non-cue blocks
        for (var i = doc.Cues.Count; i < doc.Cues.Count + 10; i++)
        {
            EmitAnchoredBlocks(blockGroups, i, writer, le);
        }
    }

    private static void EmitAnchoredBlocks(Dictionary<int, List<VttAnchoredBlock>> blockGroups, int cueIndex, TextWriter writer, string le)
    {
        if (!blockGroups.TryGetValue(cueIndex, out var list))
            return;

        foreach (var block in list)
        {
            var keyword = block.Kind switch
            {
                VttBlockKind.Note => "NOTE",
                VttBlockKind.Style => "STYLE",
                VttBlockKind.Region => "REGION",
                _ => "NOTE"
            };

            writer.Write(keyword);
            if (!string.IsNullOrEmpty(block.Content))
            {
                writer.Write(le);
                writer.Write(block.Content.Replace("\n", le));
            }
            writer.Write(le);
            writer.Write(le);
        }
    }
}
