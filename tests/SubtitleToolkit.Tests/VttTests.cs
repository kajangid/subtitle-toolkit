using System;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class VttTests
{
    [Fact]
    public void Parse_VttWithSettingsAndComments_ParsesAccurately()
    {
        var vtt = @"WEBVTT - Header commentary
X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000

NOTE Initial setup note

cue-1
00:00:01.000 --> 00:00:03.500 line:90% position:50%,line-left align:center
<v Alice>Hello from Alice</v>

NOTE Inter-cue note

00:00:04.000 --> 00:00:06.000
<v Bob>Hello from Bob</v>
";

        var result = Subtitle.Parse(vtt, SubtitleFormat.WebVtt);
        var doc = result.Document;

        Assert.Equal(SubtitleFormat.WebVtt, doc.Format);
        Assert.Equal(2, doc.Cues.Count);

        var vttDocData = Assert.IsType<VttDocumentData>(doc.FormatData);
        Assert.Equal("- Header commentary", vttDocData.HeaderComment);
        Assert.Equal("X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000", vttDocData.TimestampMap);

        // Verify anchored blocks
        Assert.Equal(2, vttDocData.NonCueBlocks.Count);
        Assert.Equal(VttBlockKind.Note, vttDocData.NonCueBlocks[0].Kind);
        Assert.Equal(0, vttDocData.NonCueBlocks[0].BeforeCueIndex);
        Assert.Equal("Initial setup note", vttDocData.NonCueBlocks[0].Content);

        Assert.Equal(1, vttDocData.NonCueBlocks[1].BeforeCueIndex);
        Assert.Equal("Inter-cue note", vttDocData.NonCueBlocks[1].Content);

        // Verify Cue 1
        var cue1 = doc.Cues[0];
        Assert.Equal(TimeSpan.FromSeconds(1), cue1.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), cue1.End);
        Assert.Equal("<v Alice>Hello from Alice</v>", cue1.RawText);
        Assert.Equal("Hello from Alice", cue1.PlainText); // Voice tag stripped in PlainText

        var cue1Data = Assert.IsType<VttCueData>(cue1.FormatData);
        Assert.Equal("cue-1", cue1Data.Identifier);
        Assert.NotNull(cue1Data.Settings);
        Assert.Equal(90, cue1Data.Settings.Line?.Value);
        Assert.True(cue1Data.Settings.Line?.IsPercentage);
        Assert.Equal(50, cue1Data.Settings.Position?.Percentage);
        Assert.Equal(VttPositionAlign.LineLeft, cue1Data.Settings.Position?.Alignment);
        Assert.Equal(VttAlignment.Center, cue1Data.Settings.Align);
    }

    [Fact]
    public void Write_PreservesAnchoredNonCueBlocksInPosition()
    {
        var cue = new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Hi",
            new VttCueData("id-1", "align:center"));

        var nonCue = new[]
        {
            new VttAnchoredBlock(0, VttBlockKind.Note, "Header Note"),
            new VttAnchoredBlock(1, VttBlockKind.Style, "::cue { color: yellow; }")
        };

        var docData = new VttDocumentData("Test Title", null, nonCue);
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, new[] { cue }, docData);

        var output = Subtitle.WriteToString(doc, SubtitleFormat.WebVtt, new SubtitleWriteOptions { LineEnding = "\n" });

        Assert.StartsWith("WEBVTT Test Title\n\nNOTE\nHeader Note\n\nid-1\n00:00:01.000 --> 00:00:02.000 align:center\nHi\n\nSTYLE\n::cue { color: yellow; }", output);
    }
}
