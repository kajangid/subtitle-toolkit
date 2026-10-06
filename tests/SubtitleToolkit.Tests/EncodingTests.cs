using System.IO;
using System.Text;
using SubtitleToolkit.IO;
using Xunit;

namespace SubtitleToolkit.Tests;

public class EncodingTests
{
    [Fact]
    public void DecodeBytes_Utf8WithBom_DecodesWithoutBomInString()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var textBytes = Encoding.UTF8.GetBytes("WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nHello");
        var allBytes = new byte[bom.Length + textBytes.Length];
        bom.CopyTo(allBytes, 0);
        textBytes.CopyTo(allBytes, bom.Length);

        var text = SubtitleEncoding.DecodeBytes(allBytes, null, out var detected);

        Assert.Equal(Encoding.UTF8, detected);
        Assert.False(text.StartsWith("\uFEFF", StringComparison.Ordinal));
        Assert.StartsWith("WEBVTT", text);
    }

    [Fact]
    public void DecodeBytes_Utf16Le_DecodesAccurately()
    {
        var rawString = "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nUTF-16 subtitle";
        var bytes = Encoding.Unicode.GetPreamble();
        var content = Encoding.Unicode.GetBytes(rawString);
        var allBytes = new byte[bytes.Length + content.Length];
        bytes.CopyTo(allBytes, 0);
        content.CopyTo(allBytes, bytes.Length);

        var text = SubtitleEncoding.DecodeBytes(allBytes, null, out var detected);

        Assert.Equal(Encoding.Unicode, detected);
        Assert.StartsWith("WEBVTT", text);
        Assert.Contains("UTF-16 subtitle", text);
    }

    [Fact]
    public void DecodeBytes_Utf32Le_DetectedBeforeUtf16Le()
    {
        var rawString = "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nUTF-32 subtitle";
        var bytes = Encoding.UTF32.GetPreamble();
        var content = Encoding.UTF32.GetBytes(rawString);
        var allBytes = new byte[bytes.Length + content.Length];
        bytes.CopyTo(allBytes, 0);
        content.CopyTo(allBytes, bytes.Length);

        var text = SubtitleEncoding.DecodeBytes(allBytes, null, out var detected);

        Assert.Equal(Encoding.UTF32, detected);
        Assert.Contains("UTF-32 subtitle", text);
    }

    [Fact]
    public void DecodeBytes_InvalidUtf8_FallsBackToConfiguredEncodingWithoutDataLoss()
    {
        // 0xE9 is 'é' in Latin-1 / Windows-1252. By itself without UTF-8 continuation, it is invalid UTF-8.
        var latin1Bytes = new byte[] { (byte)'e', 0xE9, (byte)'t', (byte)'e' }; // "eété"
        var text = SubtitleEncoding.DecodeBytes(latin1Bytes, null, out var detected);

        // Fallback default is Latin-1 (ISO-8859-1)
        Assert.Equal("ISO-8859-1", detected.WebName, ignoreCase: true);
        Assert.Equal("eéte", text);
        Assert.DoesNotContain("\uFFFD", text); // Proves no U+FFFD replacement corruption
    }
}
