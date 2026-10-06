using System;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class SrtTests
{
    [Fact]
    public void Parse_StandardSrt_ParsesCorrectly()
    {
        var srt = @"1
00:00:01,000 --> 00:00:04,000
Hello world

2
00:00:05,500 --> 00:00:08,750
Second line
with multiline text
";

        var result = Subtitle.Parse(srt, SubtitleFormat.SubRip);
        var doc = result.Document;

        Assert.Equal(SubtitleFormat.SubRip, doc.Format);
        Assert.Equal(2, doc.Cues.Count);

        Assert.Equal(TimeSpan.FromSeconds(1), doc.Cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(4), doc.Cues[0].End);
        Assert.Equal("Hello world", doc.Cues[0].RawText);
        Assert.Equal("Hello world", doc.Cues[0].PlainText);

        Assert.Equal(TimeSpan.FromMilliseconds(5500), doc.Cues[1].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(8750), doc.Cues[1].End);
        Assert.Equal("Second line\nwith multiline text", doc.Cues[1].RawText);
    }

    [Fact]
    public void Parse_LenientMissingIndex_ParsesCues()
    {
        var srt = @"00:00:01,000 --> 00:00:04,000
No index cue 1

00:00:05.000 --> 00:00:09.000
No index cue 2 with period separator
";

        var result = Subtitle.Parse(srt, SubtitleFormat.SubRip, new SubtitleReadOptions { Mode = ParseMode.Lenient });
        Assert.Equal(2, result.Document.Cues.Count);
        Assert.Equal("No index cue 1", result.Document.Cues[0].RawText);
        Assert.Equal("No index cue 2 with period separator", result.Document.Cues[1].RawText);
    }

    [Fact]
    public void Parse_StrictMalformedTimestamp_ThrowsSubtitleParseException()
    {
        var srt = @"1
invalid timestamp
Hello
";
        Assert.Throws<SubtitleParseException>(() =>
            Subtitle.Parse(srt, SubtitleFormat.SubRip, new SubtitleReadOptions { Mode = ParseMode.Strict }));
    }

    [Fact]
    public void Write_RenumbersSequentiallyStartingAtOne()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "One", new SrtCueData(99)),
            new SubtitleCue(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "Two", new SrtCueData(100))
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var output = Subtitle.WriteToString(doc, SubtitleFormat.SubRip, new SubtitleWriteOptions { LineEnding = "\n" });

        Assert.Contains("1\n00:00:01,000 --> 00:00:02,000\nOne", output);
        Assert.Contains("2\n00:00:03,000 --> 00:00:04,000\nTwo", output);
    }

    [Fact]
    public void PlainText_StripsHtmlTagsAndDecodesEntities()
    {
        var cue = new SubtitleCue(TimeSpan.Zero, TimeSpan.FromSeconds(1), "<b>Hello</b> &amp; <i>World</i>");
        Assert.Equal("Hello & World", cue.PlainText);
    }
}
