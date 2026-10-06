using System;
using System.IO;
using SubtitleToolkit.Model;
using Xunit;

namespace SubtitleToolkit.Tests;

public class SaveSafetyTests
{
    [Fact]
    public void Save_FormatMismatch_ThrowsInvalidOperationException()
    {
        var cue = new SubtitleCue(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hello");
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, new[] { cue });

        using var ms = new MemoryStream();

        // Attempting to directly save SRT as WebVTT without conversion must throw
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Subtitle.Save(doc, ms, SubtitleFormat.WebVtt));

        Assert.Contains("Cannot directly save a SubRip document as WebVtt", ex.Message);
        Assert.Contains("Subtitle.Convert()", ex.Message);
    }
}
