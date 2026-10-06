using System;
using System.Collections.Generic;
using System.IO;
using SubtitleToolkit.Common;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;

namespace SubtitleToolkit.Formats;

/// <summary>
/// Parser and serializer for SubRip (.srt) subtitle files.
/// </summary>
public sealed class SrtHandler : ISubtitleHandler
{
    public SubtitleFormat Format => SubtitleFormat.SubRip;

    public ParseResult Parse(string content, SubtitleReadOptions options)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        var cues = new List<SubtitleCue>();
        var diagnostics = new List<ParseDiagnostic>();

        // Normalize all line endings to \n for line processing
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var lineIndex = 0;

        while (lineIndex < lines.Length)
        {
            // Skip leading whitespace / blank lines
            while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
                lineIndex++;

            if (lineIndex >= lines.Length)
                break;

            var cueStartLine = lineIndex + 1;
            int? parsedIndex = null;
            string? timestampLine = null;

            // Check if current line is an integer index
            if (int.TryParse(lines[lineIndex].Trim(), out var idx))
            {
                parsedIndex = idx;
                lineIndex++;
                // Skip any empty lines between index and timestamp
                while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex]))
                    lineIndex++;
            }

            if (lineIndex < lines.Length)
            {
                timestampLine = lines[lineIndex].Trim();
            }

            if (string.IsNullOrEmpty(timestampLine) || timestampLine == null || !timestampLine.Contains("-->"))
            {
                var msg = $"Expected timestamp line with '-->' at line {cueStartLine}. Found: '{timestampLine}'";
                if (options.Mode == ParseMode.Strict)
                    throw new SubtitleParseException(msg, cueStartLine);

                diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, cueStartLine, "SRT_BAD_TIMESTAMP", msg));
                lineIndex++;
                continue;
            }

            // Parse timestamp line: "00:00:01,000 --> 00:00:04,000 [coordinates]"
            var arrowIdx = timestampLine.IndexOf("-->", StringComparison.Ordinal);
            var startPart = timestampLine.Substring(0, arrowIdx).Trim();
            var remainder = timestampLine.Substring(arrowIdx + 3).Trim();

            string endPart;
            string? coordinates = null;

            var spaceAfterEnd = remainder.IndexOf(' ');
            if (spaceAfterEnd > 0)
            {
                endPart = remainder.Substring(0, spaceAfterEnd).Trim();
                coordinates = remainder.Substring(spaceAfterEnd + 1).Trim();
                if (string.IsNullOrEmpty(coordinates)) coordinates = null;
            }
            else
            {
                endPart = remainder;
            }

            if (!TimestampHelper.TryParseSrtTimestamp(startPart, out var startTime)
                || !TimestampHelper.TryParseSrtTimestamp(endPart, out var endTime))
            {
                var msg = $"Unable to parse SRT timestamps in line '{timestampLine}'.";
                if (options.Mode == ParseMode.Strict)
                    throw new SubtitleParseException(msg, cueStartLine);

                diagnostics.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, cueStartLine, "SRT_TIMESTAMP_FORMAT", msg));
                lineIndex++;
                continue;
            }

            lineIndex++; // Move past timestamp line

            // Read text lines until blank line or next cue index + timestamp
            var textLines = new List<string>();
            while (lineIndex < lines.Length)
            {
                var current = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(current))
                    break;

                // Lookahead: if this line looks like an index number and next line has "-->", stop
                if (int.TryParse(current.Trim(), out _) && lineIndex + 1 < lines.Length && lines[lineIndex + 1].Contains("-->"))
                    break;

                textLines.Add(current);
                lineIndex++;
            }

            var rawText = string.Join("\n", textLines.ToArray());
            var srtData = new SrtCueData(parsedIndex, coordinates);
            cues.Add(new SubtitleCue(startTime, endTime, rawText, srtData));
        }

        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);
        return new ParseResult(doc, diagnostics);
    }

    public void Write(SubtitleDocument doc, TextWriter writer, SubtitleWriteOptions options)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        var le = options.LineEnding;
        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];
            var srtData = cue.FormatData as SrtCueData;

            // Sequential 1-based index per SRT contract
            writer.Write((i + 1).ToString());
            writer.Write(le);

            // Timestamps
            writer.Write(TimestampHelper.FormatSrt(cue.Start));
            writer.Write(" --> ");
            writer.Write(TimestampHelper.FormatSrt(cue.End));

            if (!string.IsNullOrEmpty(srtData?.Coordinates))
            {
                writer.Write(" ");
                writer.Write(srtData!.Coordinates);
            }
            writer.Write(le);

            // RawText: convert internal \n to configured line ending
            if (!string.IsNullOrEmpty(cue.RawText))
            {
                var formattedText = cue.RawText.Replace("\n", le);
                writer.Write(formattedText);
                writer.Write(le);
            }

            // Empty line between cues
            writer.Write(le);
        }
    }
}
