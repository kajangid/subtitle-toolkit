using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Format-specific document metadata and non-cue blocks for WebVTT files.
/// </summary>
public sealed class VttDocumentData : DocumentFormatData, IEquatable<VttDocumentData>
{
    public override SubtitleFormat Format => SubtitleFormat.WebVtt;

    /// <summary>Optional comment following the 'WEBVTT' signature on the first line.</summary>
    public string? HeaderComment { get; }

    /// <summary>Optional HLS MPEG-TS timestamp synchronization map header.</summary>
    public string? TimestampMap { get; }

    /// <summary>
    /// Positionally anchored non-cue blocks (NOTE, STYLE, REGION) preserved across the document.
    /// </summary>
    public IReadOnlyList<VttAnchoredBlock> NonCueBlocks { get; }

    public VttDocumentData(
        string? headerComment = null,
        string? timestampMap = null,
        IEnumerable<VttAnchoredBlock>? nonCueBlocks = null)
    {
        HeaderComment = headerComment;
        TimestampMap = timestampMap;
        NonCueBlocks = new ReadOnlyCollection<VttAnchoredBlock>(
            nonCueBlocks != null ? nonCueBlocks.ToList() : new List<VttAnchoredBlock>());
    }

    public bool Equals(VttDocumentData? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (!string.Equals(HeaderComment, other.HeaderComment, StringComparison.Ordinal)
            || !string.Equals(TimestampMap, other.TimestampMap, StringComparison.Ordinal))
            return false;

        if (NonCueBlocks.Count != other.NonCueBlocks.Count)
            return false;

        for (var i = 0; i < NonCueBlocks.Count; i++)
        {
            if (!NonCueBlocks[i].Equals(other.NonCueBlocks[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as VttDocumentData);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (HeaderComment?.GetHashCode() ?? 0);
            hash = (hash * 31) + (TimestampMap?.GetHashCode() ?? 0);
            hash = (hash * 31) + NonCueBlocks.Count.GetHashCode();
            return hash;
        }
    }
}
