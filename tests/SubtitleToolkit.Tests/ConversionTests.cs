using System;
using System.Linq;
using SubtitleToolkit.Diagnostics;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class ConversionTests
{
    [Fact]
    public void Convert_SameFormat_ReturnsLosslessResult()
    {
        var cues = new[] { new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Hello") };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var result = Subtitle.Convert(doc, SubtitleFormat.SubRip);

        Assert.Equal(SubtitleFormat.SubRip, result.Document.Format);
        Assert.True(result.Report.IsLossless);
        Assert.Empty(result.Report.Notices);
    }

    [Fact]
    public void Convert_SrtToVtt_ConvertsAnTagsToSettingsAndStripsMetadata()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), @"{\an8}Top Title"),
            new SubtitleCue(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6), "Bottom Dialogue")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var result = doc.Convert(SubtitleFormat.WebVtt);

        Assert.Equal(SubtitleFormat.WebVtt, result.Document.Format);
        Assert.Equal(2, result.Document.Cues.Count);

        // Cue 0: {\an8} stripped from text, converted to VTT settings
        var cue0 = result.Document.Cues[0];
        Assert.Equal("Top Title", cue0.RawText);
        var vttData0 = Assert.IsType<VttCueData>(cue0.FormatData);
        Assert.NotNull(vttData0.Settings);
        Assert.Equal(VttAlignment.Center, vttData0.Settings!.Align);
        Assert.Equal(10, vttData0.Settings.Line!.Value);

        // Notice logged
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.PositioningConverted);
    }

    [Fact]
    public void Convert_VttToSrt_ConvertsVoiceToSpeakerAndSettingsToAn()
    {
        var cues = new[]
        {
            new SubtitleCue(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3),
                "<v Alice>Hello Bob!</v>",
                new VttCueData(null, "align:right line:90%"))
        };
        var blocks = new[] { new VttAnchoredBlock(0, VttBlockKind.Note, "Comment") };
        var vttDocData = new VttDocumentData(nonCueBlocks: blocks, timestampMap: "X-TIMESTAMP-MAP=LOCAL:0,MPEGTS:0");
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues, vttDocData);

        var result = Subtitle.Convert(doc, SubtitleFormat.SubRip);

        Assert.Equal(SubtitleFormat.SubRip, result.Document.Format);
        var srtCue = result.Document.Cues[0];

        // Speaker label prepended, {\an3} prepended for right-bottom align
        Assert.StartsWith("{\\an3}", srtCue.RawText);
        Assert.Contains("Alice: Hello Bob!", srtCue.RawText);

        // Reports dropped non-cue blocks
        Assert.True(result.Report.HasWarnings);
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.NonCueBlocksDropped);
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.VoiceTagConverted);
    }

    [Fact]
    public void Convert_AssToSrt_StripsDrawingsAndStyles_LogsLosses()
    {
        var cues = new[]
        {
            new SubtitleCue(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3),
                @"{\b1\pos(100,200)}Bold text{\b0}{\p1}m 0 0 l 10 10{\p0}")
        };
        var styles = new[] { new AssStyle { Name = "Header", FontSize = 30 } };
        var assDocData = new AssDocumentData(styles: styles);
        var doc = new SubtitleDocument(SubtitleFormat.Ass, cues, assDocData);

        var result = Subtitle.Convert(doc, SubtitleFormat.SubRip);

        Assert.Equal(SubtitleFormat.SubRip, result.Document.Format);
        var srtCue = result.Document.Cues[0];

        // HTML tags used for bold, drawings stripped, pos tag stripped
        Assert.Equal("<b>Bold text</b>", srtCue.RawText);
        Assert.DoesNotContain("m 0 0", srtCue.RawText);

        // Audit report
        Assert.True(result.Report.HasWarnings);
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.DrawingDropped);
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.StyleTableDropped);
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.PositioningDropped);
    }

    [Fact]
    public void Convert_SrtToAss_GeneratesScriptInfoAndColors()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "<b>Bold</b> and <font color=\"#0000FF\">Blue</font>")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var result = doc.Convert(SubtitleFormat.Ass, new ConversionOptions
        {
            TargetPlayResX = 384,
            TargetPlayResY = 288
        });

        Assert.Equal(SubtitleFormat.Ass, result.Document.Format);
        var assDocData = Assert.IsType<AssDocumentData>(result.Document.FormatData);

        // Default PlayRes and ScriptInfo
        Assert.Equal("384", assDocData.ScriptInfo["PlayResX"]);
        Assert.Equal("288", assDocData.ScriptInfo["PlayResY"]);

        // Blue (#0000FF) converted to ASS BGR (&HFF0000&)
        var assCue = result.Document.Cues[0];
        Assert.Contains(@"{\b1}Bold{\b0}", assCue.RawText);
        Assert.Contains(@"{\c&HFF0000&}Blue", assCue.RawText);
    }

    [Fact]
    public void Convert_AssToVtt_CoordinatesMappedAccurately()
    {
        // 192/384 = 50%, 144/288 = 50%
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), @"{\an5\pos(192,144)}Centered Title")
        };
        var scriptInfo = new System.Collections.Generic.Dictionary<string, string>
        {
            ["PlayResX"] = "384",
            ["PlayResY"] = "288"
        };
        var doc = new SubtitleDocument(SubtitleFormat.Ass, cues, new AssDocumentData(scriptInfo));

        var result = Subtitle.Convert(doc, SubtitleFormat.WebVtt);

        Assert.Equal(SubtitleFormat.WebVtt, result.Document.Format);
        var vttCue = result.Document.Cues[0];
        Assert.Equal("Centered Title", vttCue.RawText);

        var vttData = Assert.IsType<VttCueData>(vttCue.FormatData);
        Assert.NotNull(vttData.Settings);
        Assert.Equal(50.0, vttData.Settings!.Position!.Percentage);
        Assert.Equal(50.0, vttData.Settings.Line!.Value);
        Assert.Equal(VttLineAlign.Center, vttData.Settings.Line.Alignment);
        Assert.Equal(VttAlignment.Center, vttData.Settings.Align);
    }

    [Fact]
    public void Convert_VttToAss_SnapToLinesAndVoiceMapping()
    {
        var cues = new[]
        {
            new SubtitleCue(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3),
                "<v Narrator>Chapter 1",
                new VttCueData(null, "line:0 align:center"))
        };
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues);

        var result = Subtitle.Convert(doc, SubtitleFormat.Ass);
        var assCue = result.Document.Cues[0];

        // Voice mapped to Actor
        var assData = Assert.IsType<AssCueData>(assCue.FormatData);
        Assert.Equal("Narrator", assData.ActorName);

        // Snap to lines 0 mapped to top position
        Assert.Contains(@"\pos(", assCue.RawText);
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.SnapToLinesConverted);
        Assert.Contains(result.Report.Notices, n => n.Code == ConversionNoticeCode.VoiceTagConverted);
    }

    [Fact]
    public void Convert_AssToSsa_And_SsaToAss_AlignmentsAndMarkedMapped()
    {
        // 1. ASS -> SSA
        var assStyles = new[] { new AssStyle { Name = "TopStyle", Alignment = 7 } }; // ASS 7 = Top-Left
        var assCues = new[]
        {
            new SubtitleCue(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3),
                @"{\an7}Top Left",
                new AssCueData { Layer = 2 })
        };
        var assDoc = new SubtitleDocument(SubtitleFormat.Ass, assCues, new AssDocumentData(styles: assStyles));

        var ssaResult = Subtitle.Convert(assDoc, SubtitleFormat.Ssa);
        Assert.Equal(SubtitleFormat.Ssa, ssaResult.Document.Format);

        var ssaDocData = Assert.IsType<AssDocumentData>(ssaResult.Document.FormatData);
        Assert.Equal(5, ssaDocData.Styles[0].Alignment); // SSA 5 = Top-Left

        var ssaCue = ssaResult.Document.Cues[0];
        Assert.Contains(@"\a5", ssaCue.RawText); // \an7 -> \a5
        var ssaCueData = Assert.IsType<AssCueData>(ssaCue.FormatData);
        Assert.True(ssaCueData.Marked); // Layer > 0 -> Marked = true

        // 2. SSA -> ASS
        var assResult = Subtitle.Convert(ssaResult.Document, SubtitleFormat.Ass);
        Assert.Equal(SubtitleFormat.Ass, assResult.Document.Format);

        var roundtripDocData = Assert.IsType<AssDocumentData>(assResult.Document.FormatData);
        Assert.Equal(7, roundtripDocData.Styles[0].Alignment); // SSA 5 -> ASS 7

        var roundtripCue = assResult.Document.Cues[0];
        Assert.Contains(@"\an7", roundtripCue.RawText);
        var roundtripCueData = Assert.IsType<AssCueData>(roundtripCue.FormatData);
        Assert.Equal(1, roundtripCueData.Layer); // Marked -> Layer 1
    }

    [Fact]
    public void Convert_ThrowOnLoss_ThrowsWhenLossOccurs()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), @"{\p1}m 0 0{\p0}")
        };
        var doc = new SubtitleDocument(SubtitleFormat.Ass, cues);

        var ex = Assert.Throws<SubtitleConversionException>(() =>
        {
            Subtitle.Convert(doc, SubtitleFormat.SubRip, new ConversionOptions { ThrowOnLoss = true });
        });

        Assert.True(ex.Report.HasWarnings);
        Assert.Contains("data loss", ex.Message);
    }
}
