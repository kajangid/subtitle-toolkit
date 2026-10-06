using System;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class AssTests
{
    [Fact]
    public void Parse_FullAssFile_PreservesStylesEventsAndUnknownSections()
    {
        var ass = @"[Script Info]
Title: Sample Fansub
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080

[Aegisub Project Garbage]
Last Style Storage: Default
Audio File: video.mkv

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Open Sans,45,&H00FFFFFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,2,1,2,20,20,20,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Comment: 0,0:00:00.00,0:00:05.00,Default,,0,0,0,,Sign: Opening Title
Dialogue: 1,0:01:23.45,0:01:28.90,Default,Narrator,10,10,20,,{\b1}Hello, world!{\b0} Here is a comma, right here.\NAnd second line.
";

        var result = Subtitle.Parse(ass, SubtitleFormat.Ass);
        var doc = result.Document;

        Assert.Equal(SubtitleFormat.Ass, doc.Format);
        Assert.Equal(2, doc.Cues.Count);

        var assData = Assert.IsType<AssDocumentData>(doc.FormatData);
        Assert.Equal("Sample Fansub", assData.ScriptInfo["Title"]);
        Assert.Equal("1920", assData.ScriptInfo["PlayResX"]);
        Assert.Equal("1080", assData.ScriptInfo["PlayResY"]);

        // Unknown section preserved
        Assert.Single(assData.UnknownSections);
        Assert.Equal("Aegisub Project Garbage", assData.UnknownSections[0].SectionName);
        Assert.Contains("Audio File: video.mkv", assData.UnknownSections[0].Content);

        // Styles preserved
        Assert.Single(assData.Styles);
        Assert.Equal("Default", assData.Styles[0].Name);
        Assert.Equal("Open Sans", assData.Styles[0].FontName);
        Assert.Equal(45, assData.Styles[0].FontSize);
        Assert.True(assData.Styles[0].Bold);

        // Event 1 (Comment)
        var cue1 = doc.Cues[0];
        var cue1Data = Assert.IsType<AssCueData>(cue1.FormatData);
        Assert.Equal(AssEventType.Comment, cue1Data.EventType);
        Assert.Equal(0, cue1Data.Layer);
        Assert.Equal("Sign: Opening Title", cue1.RawText);

        // Event 2 (Dialogue with internal commas)
        var cue2 = doc.Cues[1];
        var cue2Data = Assert.IsType<AssCueData>(cue2.FormatData);
        Assert.Equal(AssEventType.Dialogue, cue2Data.EventType);
        Assert.Equal(1, cue2Data.Layer);
        Assert.Equal("Narrator", cue2Data.ActorName);
        Assert.Equal(TimeSpan.FromMilliseconds(83450), cue2.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(88900), cue2.End);

        // Internal comma in dialogue was NOT split
        Assert.Contains("Here is a comma, right here.", cue2.RawText);

        // PlainText extraction strips override tags and maps \N to newline
        Assert.Equal("Hello, world! Here is a comma, right here.\nAnd second line.", cue2.PlainText);
    }

    [Fact]
    public void Write_FormatsCentisecondsWithStandardRounding()
    {
        var cue = new SubtitleCue(
            TimeSpan.FromMilliseconds(1234), // 1.234s -> 1:00:01.23 (rounds to 123 cs)
            TimeSpan.FromMilliseconds(5678)  // 5.678s -> 1:00:05.68 (rounds to 568 cs)
            , "Test", new AssCueData());

        var doc = new SubtitleDocument(SubtitleFormat.Ass, new[] { cue });
        var output = Subtitle.WriteToString(doc, SubtitleFormat.Ass, new SubtitleWriteOptions { LineEnding = "\n" });

        Assert.Contains("0:00:01.23,0:00:05.68", output);
    }
}
