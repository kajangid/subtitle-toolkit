using System;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class RoundTripTests
{
    [Fact]
    public void RoundTrip_Srt_MatchesDocument()
    {
        var originalSrt = @"1
00:00:01,000 --> 00:00:04,500
First subtitle line.

2
00:00:05,000 --> 00:00:09,000
Second subtitle line
with multiline text.
";

        var doc1 = Subtitle.Parse(originalSrt, SubtitleFormat.SubRip).Document;
        var written = Subtitle.WriteToString(doc1, SubtitleFormat.SubRip, new SubtitleWriteOptions { LineEnding = "\n" });
        var doc2 = Subtitle.Parse(written, SubtitleFormat.SubRip).Document;

        Assert.Equal(doc1.Cues.Count, doc2.Cues.Count);
        for (var i = 0; i < doc1.Cues.Count; i++)
        {
            Assert.Equal(doc1.Cues[i].Start, doc2.Cues[i].Start);
            Assert.Equal(doc1.Cues[i].End, doc2.Cues[i].End);
            Assert.Equal(doc1.Cues[i].RawText, doc2.Cues[i].RawText);
        }
    }

    [Fact]
    public void RoundTrip_Vtt_MatchesDocumentAndSettings()
    {
        var originalVtt = @"WEBVTT - Header Info

cue-1
00:00:01.000 --> 00:00:03.000 line:90% align:center
First WebVTT cue.

cue-2
00:00:04.500 --> 00:00:07.000
Second WebVTT cue.
";

        var doc1 = Subtitle.Parse(originalVtt, SubtitleFormat.WebVtt).Document;
        var written = Subtitle.WriteToString(doc1, SubtitleFormat.WebVtt, new SubtitleWriteOptions { LineEnding = "\n" });
        var doc2 = Subtitle.Parse(written, SubtitleFormat.WebVtt).Document;

        Assert.Equal(doc1.Cues.Count, doc2.Cues.Count);
        for (var i = 0; i < doc1.Cues.Count; i++)
        {
            Assert.Equal(doc1.Cues[i].Start, doc2.Cues[i].Start);
            Assert.Equal(doc1.Cues[i].End, doc2.Cues[i].End);
            Assert.Equal(doc1.Cues[i].RawText, doc2.Cues[i].RawText);

            var cueData1 = doc1.Cues[i].FormatData as VttCueData;
            var cueData2 = doc2.Cues[i].FormatData as VttCueData;
            Assert.Equal(cueData1?.Identifier, cueData2?.Identifier);
            Assert.Equal(cueData1?.Settings?.Line?.Value, cueData2?.Settings?.Line?.Value);
            Assert.Equal(cueData1?.Settings?.Align, cueData2?.Settings?.Align);
        }
    }

    [Fact]
    public void RoundTrip_Ass_MatchesStylesEventsAndUnknownSections()
    {
        var originalAss = @"[Script Info]
Title: RoundTrip Test
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080

[Aegisub Extradata]
Item 1: Extradata payload

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Main,Arial,36,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:01.00,0:00:04.00,Main,,0,0,0,,{\b1}Bold line{\b0}
Dialogue: 1,0:00:05.50,0:00:08.75,Main,Actor,10,10,10,,Line with a comma, right here.
";

        var doc1 = Subtitle.Parse(originalAss, SubtitleFormat.Ass).Document;
        var written = Subtitle.WriteToString(doc1, SubtitleFormat.Ass, new SubtitleWriteOptions { LineEnding = "\n" });
        var doc2 = Subtitle.Parse(written, SubtitleFormat.Ass).Document;

        var data1 = Assert.IsType<AssDocumentData>(doc1.FormatData);
        var data2 = Assert.IsType<AssDocumentData>(doc2.FormatData);

        Assert.Equal(data1.ScriptInfo["Title"], data2.ScriptInfo["Title"]);
        Assert.Single(data2.UnknownSections);
        Assert.Equal("Aegisub Extradata", data2.UnknownSections[0].SectionName);

        Assert.Equal(doc1.Cues.Count, doc2.Cues.Count);
        for (var i = 0; i < doc1.Cues.Count; i++)
        {
            Assert.Equal(doc1.Cues[i].Start, doc2.Cues[i].Start);
            Assert.Equal(doc1.Cues[i].End, doc2.Cues[i].End);
            Assert.Equal(doc1.Cues[i].RawText, doc2.Cues[i].RawText);
        }
    }
}
