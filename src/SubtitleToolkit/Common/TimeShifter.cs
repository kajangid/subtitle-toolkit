using System;
using System.Collections.Generic;
using System.Linq;
using SubtitleToolkit.Common;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;

namespace SubtitleToolkit;

/// <summary>
/// Configuration options for time-shifting operations.
/// </summary>
public sealed class TimeShiftOptions
{
    /// <summary>
    /// If true, clamps negative timestamps (Start or End &lt; 0) to <see cref="TimeSpan.Zero"/>.
    /// Default is true.
    /// </summary>
    public bool ClampNegativeToZero { get; set; } = true;

    /// <summary>
    /// If true, cues whose shifted end timestamp is &lt;= <see cref="TimeSpan.Zero"/> are dropped.
    /// Default is false.
    /// </summary>
    public bool DropNegativeCues { get; set; } = false;

    /// <summary>
    /// Optional filter predicate. If specified, only cues matching this predicate are shifted.
    /// Non-matching cues remain at their original timestamps.
    /// </summary>
    public Func<SubtitleCue, bool>? Filter { get; set; }
}

/// <summary>
/// Performs immutable time-shifting on subtitle documents and cues.
/// </summary>
public static class TimeShifter
{
    /// <summary>
    /// Shifts all (or filtered) cues in a document by the specified offset.
    /// Returns a new immutable <see cref="SubtitleDocument"/>.
    /// </summary>
    public static SubtitleDocument Shift(SubtitleDocument doc, TimeSpan offset, TimeShiftOptions? options = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        options ??= new TimeShiftOptions();

        var trackedCues = new List<(SubtitleCue Cue, int OriginalIndex)>(doc.Cues.Count);

        for (var i = 0; i < doc.Cues.Count; i++)
        {
            var cue = doc.Cues[i];

            // If a filter is specified and cue does not match, keep as-is
            if (options.Filter != null && !options.Filter(cue))
            {
                trackedCues.Add((cue, i));
                continue;
            }

            var newStart = cue.Start + offset;
            var newEnd = cue.End + offset;

            // Drop negative cues if requested
            if (options.DropNegativeCues && newEnd <= TimeSpan.Zero)
            {
                continue;
            }

            // Clamp negative timestamps to zero if requested
            if (options.ClampNegativeToZero)
            {
                if (newStart < TimeSpan.Zero) newStart = TimeSpan.Zero;
                if (newEnd < TimeSpan.Zero) newEnd = TimeSpan.Zero;
            }

            var shiftedCue = cue.WithTimes(newStart, newEnd);
            trackedCues.Add((shiftedCue, i));
        }

        // WebVTT requires non-decreasing start times: enforce chronological ordering
        if (doc.Format == SubtitleFormat.WebVtt)
        {
            trackedCues = trackedCues.OrderBy(tc => tc.Cue.Start).ToList();
        }

        var newCues = trackedCues.Select(tc => tc.Cue).ToList();
        var newFormatData = ShiftFormatData(doc, offset, options, trackedCues);

        return new SubtitleDocument(doc.Format, newCues, newFormatData);
    }

    private static DocumentFormatData? ShiftFormatData(
        SubtitleDocument doc,
        TimeSpan offset,
        TimeShiftOptions options,
        List<(SubtitleCue Cue, int OriginalIndex)> trackedCues)
    {
        if (doc.FormatData is VttDocumentData vttData)
        {
            return ShiftVttData(vttData, doc.Cues.Count, offset, options, trackedCues);
        }

        return doc.FormatData;
    }

    private static VttDocumentData ShiftVttData(
        VttDocumentData vttData,
        int originalCueCount,
        TimeSpan offset,
        TimeShiftOptions options,
        List<(SubtitleCue Cue, int OriginalIndex)> trackedCues)
    {
        // 1. Adjust X-TIMESTAMP-MAP LOCAL timestamp if shifting all cues
        var timestampMap = vttData.TimestampMap;
        if (options.Filter == null && !string.IsNullOrEmpty(timestampMap))
        {
            timestampMap = AdjustVttTimestampMap(timestampMap!, offset, options.ClampNegativeToZero);
        }

        // 2. Adjust anchored non-cue blocks
        var newBlocks = new List<VttAnchoredBlock>(vttData.NonCueBlocks.Count);
        foreach (var block in vttData.NonCueBlocks)
        {
            var oldIdx = block.BeforeCueIndex;
            int newIdx;

            if (oldIdx <= 0)
            {
                newIdx = oldIdx; // Top of file / before first cue
            }
            else if (oldIdx >= originalCueCount)
            {
                newIdx = trackedCues.Count; // Trailing after all cues
            }
            else
            {
                // Find first surviving cue with OriginalIndex >= oldIdx
                var foundIdx = trackedCues.FindIndex(tc => tc.OriginalIndex >= oldIdx);
                newIdx = foundIdx >= 0 ? foundIdx : trackedCues.Count;
            }

            newBlocks.Add(new VttAnchoredBlock(newIdx, block.Kind, block.Content));
        }

        return new VttDocumentData(vttData.HeaderComment, timestampMap, newBlocks);
    }

    private static string AdjustVttTimestampMap(string timestampMap, TimeSpan offset, bool clampNegativeToZero)
    {
        var localIdx = timestampMap.IndexOf("LOCAL:", StringComparison.OrdinalIgnoreCase);
        if (localIdx < 0) return timestampMap;

        var start = localIdx + 6;
        var end = timestampMap.IndexOfAny(new[] { ',', ' ', '\t' }, start);
        if (end < 0) end = timestampMap.Length;

        var localStr = timestampMap.Substring(start, end - start);
        if (!TimestampHelper.TryParseVttTimestamp(localStr, out var localTime))
            return timestampMap;

        var shifted = localTime + offset;
        if (clampNegativeToZero && shifted < TimeSpan.Zero)
            shifted = TimeSpan.Zero;

        var newLocalStr = TimestampHelper.FormatVtt(shifted);
        return timestampMap.Substring(0, start) + newLocalStr + timestampMap.Substring(end);
    }
}
