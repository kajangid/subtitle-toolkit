using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model.FormatData;

namespace SubtitleToolkit.Model;

/// <summary>
/// Represents an immutable parsed subtitle document containing cues, format type, and metadata bags.
/// </summary>
public sealed class SubtitleDocument : IEquatable<SubtitleDocument>
{
    /// <summary>The format of this subtitle document.</summary>
    public SubtitleFormat Format { get; }

    /// <summary>The collection of subtitle cues, defensively copied and immutable.</summary>
    public IReadOnlyList<SubtitleCue> Cues { get; }

    /// <summary>Format-specific document data and sections, or null if none.</summary>
    public DocumentFormatData? FormatData { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleDocument"/> class.
    /// </summary>
    public SubtitleDocument(
        SubtitleFormat format,
        IEnumerable<SubtitleCue> cues,
        DocumentFormatData? formatData = null)
    {
        if (cues == null) throw new ArgumentNullException(nameof(cues));

        Format = format;
        var cueList = new List<SubtitleCue>(cues);
        Cues = new ReadOnlyCollection<SubtitleCue>(cueList);
        FormatData = formatData;
    }

    /// <summary>
    /// Saves the subtitle document to a file path.
    /// </summary>
    public void Save(string path, SubtitleWriteOptions? options = null)
        => Subtitle.Save(this, path, Format, options);

    /// <summary>
    /// Saves the subtitle document to a stream.
    /// </summary>
    public void Save(Stream stream, SubtitleWriteOptions? options = null)
        => Subtitle.Save(this, stream, Format, options);

    /// <summary>
    /// Asynchronously saves the subtitle document to a file path.
    /// </summary>
    public Task SaveAsync(string path, SubtitleWriteOptions? options = null, CancellationToken ct = default)
        => Subtitle.SaveAsync(this, path, Format, options, ct);

    /// <summary>
    /// Asynchronously saves the subtitle document to a stream.
    /// </summary>
    public Task SaveAsync(Stream stream, SubtitleWriteOptions? options = null, CancellationToken ct = default)
        => Subtitle.SaveAsync(this, stream, Format, options, ct);

    /// <summary>
    /// Shifts cues in this document by the specified offset.
    /// </summary>
    public SubtitleDocument TimeShift(TimeSpan offset, TimeShiftOptions? options = null)
        => TimeShifter.Shift(this, offset, options);

    /// <inheritdoc/>
    public bool Equals(SubtitleDocument? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Format != other.Format || Cues.Count != other.Cues.Count)
            return false;

        for (var i = 0; i < Cues.Count; i++)
        {
            if (!Cues[i].Equals(other.Cues[i]))
                return false;
        }

        return Equals(FormatData, other.FormatData);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SubtitleDocument);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Format.GetHashCode();
            hash = (hash * 31) + Cues.Count.GetHashCode();
            hash = (hash * 31) + (FormatData?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Format} Document ({Cues.Count} cues)";
}
