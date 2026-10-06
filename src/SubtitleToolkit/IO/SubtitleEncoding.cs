using System;
using System.IO;
using System.Text;

namespace SubtitleToolkit.IO;

/// <summary>
/// Resilient encoding detection and stream reading pipeline.
/// </summary>
public static class SubtitleEncoding
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    /// <summary>
    /// Reads an entire stream to string using BOM detection, strict UTF-8 trial, and safe fallback.
    /// </summary>
    public static string ReadString(Stream stream, Encoding? fallbackEncoding, out Encoding detectedEncoding)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        // Read all stream bytes
        byte[] bytes;
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
        {
            bytes = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array!, segment.Offset, bytes, 0, segment.Count);
        }
        else
        {
            using var bufferStream = new MemoryStream();
            stream.CopyTo(bufferStream);
            bytes = bufferStream.ToArray();
        }

        return DecodeBytes(bytes, fallbackEncoding, out detectedEncoding);
    }

    /// <summary>
    /// Decodes raw byte content with BOM detection, strict UTF-8 trial, and safe fallback.
    /// </summary>
    public static string DecodeBytes(byte[] bytes, Encoding? fallbackEncoding, out Encoding detectedEncoding)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length == 0)
        {
            detectedEncoding = Encoding.UTF8;
            return string.Empty;
        }

        // 1. Check 4-byte UTF-32 BOMs before 2-byte UTF-16 BOMs
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                detectedEncoding = Encoding.UTF32; // UTF-32 LE
                return detectedEncoding.GetString(bytes, 4, bytes.Length - 4);
            }
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                detectedEncoding = new UTF32Encoding(true, true); // UTF-32 BE
                return detectedEncoding.GetString(bytes, 4, bytes.Length - 4);
            }
        }

        // 2. Check 2-byte UTF-16 BOMs
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                detectedEncoding = Encoding.Unicode; // UTF-16 LE
                return detectedEncoding.GetString(bytes, 2, bytes.Length - 2);
            }
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                detectedEncoding = Encoding.BigEndianUnicode; // UTF-16 BE
                return detectedEncoding.GetString(bytes, 2, bytes.Length - 2);
            }
        }

        // 3. Check 3-byte UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            detectedEncoding = Encoding.UTF8;
            return detectedEncoding.GetString(bytes, 3, bytes.Length - 3);
        }

        // 4. No BOM: Attempt strict UTF-8 trial
        try
        {
            var text = StrictUtf8.GetString(bytes);
            detectedEncoding = Encoding.UTF8;
            return text;
        }
        catch (DecoderFallbackException)
        {
            // Strict UTF-8 failed due to invalid bytes.
            // Fall back to configured fallback or ISO-8859-1 to prevent U+FFFD data loss.
            detectedEncoding = fallbackEncoding ?? Latin1;
            return detectedEncoding.GetString(bytes);
        }
    }
}
