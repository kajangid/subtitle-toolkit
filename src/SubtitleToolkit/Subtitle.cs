using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.Formats;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;

namespace SubtitleToolkit;

/// <summary>
/// Main entry point for loading, parsing, and saving subtitle documents.
/// </summary>
public static class Subtitle
{
    private static readonly SrtHandler Srt = new();
    private static readonly VttHandler Vtt = new();
    private static readonly AssHandler Ass = new();
    private static readonly SsaHandler Ssa = new();

    /// <summary>
    /// Parses subtitle text from a string.
    /// </summary>
    public static ParseResult Parse(string content, SubtitleFormat? format = null, SubtitleReadOptions? options = null)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        options ??= new SubtitleReadOptions();
        var effectiveFormat = format ?? options.Format ?? FormatDetector.Detect(content);
        var handler = GetHandler(effectiveFormat);

        return handler.Parse(content, options);
    }

    /// <summary>
    /// Loads a subtitle file from the specified path.
    /// </summary>
    public static ParseResult Load(string path, SubtitleReadOptions? options = null)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        using var stream = File.OpenRead(path);
        var format = options?.Format ?? GuessFormatFromExtension(path);
        return Load(stream, format, options);
    }

    /// <summary>
    /// Loads a subtitle document from a stream with automatic BOM and encoding recovery.
    /// </summary>
    public static ParseResult Load(Stream stream, SubtitleFormat? format = null, SubtitleReadOptions? options = null)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        options ??= new SubtitleReadOptions();
        var content = SubtitleEncoding.ReadString(stream, options.FallbackEncoding, out _);
        var effectiveFormat = format ?? options.Format ?? FormatDetector.Detect(content);
        var handler = GetHandler(effectiveFormat);

        return handler.Parse(content, options);
    }

    /// <summary>
    /// Asynchronously loads a subtitle file from the specified path.
    /// </summary>
    public static async Task<ParseResult> LoadAsync(string path, SubtitleReadOptions? options = null, CancellationToken ct = default)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
#if NET8_0_OR_GREATER
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
#else
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
#endif
        var format = options?.Format ?? GuessFormatFromExtension(path);
        return await LoadAsync(stream, format, options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously loads a subtitle document from a stream with encoding recovery.
    /// </summary>
    public static async Task<ParseResult> LoadAsync(Stream stream, SubtitleFormat? format = null, SubtitleReadOptions? options = null, CancellationToken ct = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        options ??= new SubtitleReadOptions();
        using var ms = new MemoryStream();
#if NET8_0_OR_GREATER
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
#else
        await stream.CopyToAsync(ms).ConfigureAwait(false);
#endif
        ms.Position = 0;
        return Load(ms, format, options);
    }

    /// <summary>
    /// Saves a subtitle document to the specified file path.
    /// Throws <see cref="InvalidOperationException"/> if targetFormat does not match document format.
    /// </summary>
    public static void Save(SubtitleDocument doc, string path, SubtitleFormat? targetFormat = null, SubtitleWriteOptions? options = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (path == null) throw new ArgumentNullException(nameof(path));

        var format = targetFormat ?? GuessFormatFromExtension(path) ?? doc.Format;
        ValidateSaveFormat(doc, format);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(path);
        Save(doc, stream, format, options);
    }

    /// <summary>
    /// Saves a subtitle document to the specified stream.
    /// Throws <see cref="InvalidOperationException"/> if targetFormat does not match document format.
    /// </summary>
    public static void Save(SubtitleDocument doc, Stream stream, SubtitleFormat targetFormat, SubtitleWriteOptions? options = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        ValidateSaveFormat(doc, targetFormat);
        options ??= new SubtitleWriteOptions();

        var encoding = ResolveEncoding(targetFormat, options);
        using var writer = new StreamWriter(stream, encoding, 1024, leaveOpen: true);
        var handler = GetHandler(targetFormat);
        handler.Write(doc, writer, options);
        writer.Flush();
    }

    /// <summary>
    /// Asynchronously saves a subtitle document to a file path.
    /// </summary>
    public static async Task SaveAsync(SubtitleDocument doc, string path, SubtitleFormat? targetFormat = null, SubtitleWriteOptions? options = null, CancellationToken ct = default)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (path == null) throw new ArgumentNullException(nameof(path));

        var format = targetFormat ?? GuessFormatFromExtension(path) ?? doc.Format;
        ValidateSaveFormat(doc, format);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

#if NET8_0_OR_GREATER
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
#else
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
#endif
        await SaveAsync(doc, stream, format, options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously saves a subtitle document to a stream.
    /// </summary>
    public static Task SaveAsync(SubtitleDocument doc, Stream stream, SubtitleFormat targetFormat, SubtitleWriteOptions? options = null, CancellationToken ct = default)
    {
        // Serialization is CPU-bound in memory
        Save(doc, stream, targetFormat, options);
#if NET8_0_OR_GREATER
        return Task.CompletedTask;
#else
        return Task.FromResult(0);
#endif
    }

    /// <summary>
    /// Serializes a subtitle document directly to string.
    /// </summary>
    public static string WriteToString(SubtitleDocument doc, SubtitleFormat targetFormat, SubtitleWriteOptions? options = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        ValidateSaveFormat(doc, targetFormat);

        options ??= new SubtitleWriteOptions();
        using var writer = new StringWriter();
        var handler = GetHandler(targetFormat);
        handler.Write(doc, writer, options);
        return writer.ToString();
    }

    /// <summary>
    /// Shifts cues in the subtitle document by the specified offset.
    /// </summary>
    public static SubtitleDocument TimeShift(SubtitleDocument doc, TimeSpan offset, TimeShiftOptions? options = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        return doc.TimeShift(offset, options);
    }

    /// <summary>
    /// Converts a subtitle document to a target format with structured loss reporting.
    /// </summary>
    public static ConversionResult Convert(SubtitleDocument doc, SubtitleFormat targetFormat, ConversionOptions? options = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        return SubtitleToolkit.Common.SubtitleConverter.Convert(doc, targetFormat, options);
    }

    private static void ValidateSaveFormat(SubtitleDocument doc, SubtitleFormat targetFormat)
    {
        if (doc.Format != targetFormat)
        {
            throw new InvalidOperationException(
                $"Cannot directly save a {doc.Format} document as {targetFormat}. Format conversions are potentially lossy; use Subtitle.Convert() for explicit format conversion.");
        }
    }

    private static Encoding ResolveEncoding(SubtitleFormat format, SubtitleWriteOptions options)
    {
        // WebVTT specification strictly mandates UTF-8 without BOM
        if (format == SubtitleFormat.WebVtt)
        {
            return new UTF8Encoding(false);
        }

        if (options.Encoding != null)
        {
            return options.Encoding;
        }

        // EmitBom controls whether UTF-8 BOM is emitted
        return new UTF8Encoding(options.EmitBom);
    }

    private static ISubtitleHandler GetHandler(SubtitleFormat format) => format switch
    {
        SubtitleFormat.SubRip => Srt,
        SubtitleFormat.WebVtt => Vtt,
        SubtitleFormat.Ass => Ass,
        SubtitleFormat.Ssa => Ssa,
        _ => throw new NotSupportedException($"Subtitle format '{format}' is not supported.")
    };

    private static SubtitleFormat? GuessFormatFromExtension(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        return ext switch
        {
            ".srt" => SubtitleFormat.SubRip,
            ".vtt" => SubtitleFormat.WebVtt,
            ".ass" => SubtitleFormat.Ass,
            ".ssa" => SubtitleFormat.Ssa,
            _ => null
        };
    }
}
