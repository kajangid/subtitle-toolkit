using System;

namespace SubtitleToolkit.Model.FormatData;

/// <summary>
/// Kind of non-cue block in a WebVTT file.
/// </summary>
public enum VttBlockKind
{
    Note,
    Style,
    Region
}

/// <summary>
/// Represents a non-cue WebVTT block (NOTE, STYLE, REGION) anchored to a cue index.
/// </summary>
public sealed class VttAnchoredBlock : IEquatable<VttAnchoredBlock>
{
    /// <summary>
    /// Index of cue before which this block appears.
    /// 0 indicates before the first cue; -1 indicates top of file after WEBVTT; Cues.Count indicates after all cues.
    /// </summary>
    public int BeforeCueIndex { get; }

    /// <summary>The block kind.</summary>
    public VttBlockKind Kind { get; }

    /// <summary>Raw text body of the block (excluding block keyword header).</summary>
    public string Content { get; }

    public VttAnchoredBlock(int beforeCueIndex, VttBlockKind kind, string content)
    {
        BeforeCueIndex = beforeCueIndex;
        Kind = kind;
        Content = content ?? string.Empty;
    }

    public bool Equals(VttAnchoredBlock? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return BeforeCueIndex == other.BeforeCueIndex
            && Kind == other.Kind
            && string.Equals(Content, other.Content, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as VttAnchoredBlock);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + BeforeCueIndex.GetHashCode();
            hash = (hash * 31) + Kind.GetHashCode();
            hash = (hash * 31) + (Content?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
