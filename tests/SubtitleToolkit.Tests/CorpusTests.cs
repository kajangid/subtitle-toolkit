using System;
using System.IO;
using SubtitleToolkit.Common;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class CorpusTests
{
    private static string GetCorpusPath(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var direct = Path.Combine(baseDir, "Corpus", fileName);
        if (File.Exists(direct)) return direct;

        var sourcePath = Path.Combine(baseDir, "../../../Corpus", fileName);
        if (File.Exists(sourcePath)) return Path.GetFullPath(sourcePath);

        throw new FileNotFoundException($"Corpus file '{fileName}' not found at {direct} or {sourcePath}");
    }

    [Fact]
    public void Corpus_AegisubAss_ParsesAndRoundTrips()
    {
        var path = GetCorpusPath("corpus_aegisub.ass");
        var result = Subtitle.Load(path);
        Assert.False(result.HasErrors);

        var doc = result.Document;
        Assert.Equal(SubtitleFormat.Ass, doc.Format);
        Assert.Equal(4, doc.Cues.Count);

        var assData = Assert.IsType<AssDocumentData>(doc.FormatData);
        Assert.Equal("1920", assData.ScriptInfo["PlayResX"]);
        Assert.Equal("1080", assData.ScriptInfo["PlayResY"]);
        Assert.Equal(2, assData.Styles.Count);
        Assert.Contains(assData.UnknownSections, s => s.SectionName == "Aegisub Project Garbage");

        // Round trip
        var written = Subtitle.WriteToString(doc, SubtitleFormat.Ass);
        var roundTrip = Subtitle.Parse(written, SubtitleFormat.Ass);
        Assert.Equal(doc.Cues.Count, roundTrip.Document.Cues.Count);
        Assert.Equal(doc.Cues[0].RawText, roundTrip.Document.Cues[0].RawText);
    }

    [Fact]
    public void Corpus_StreamingVtt_ParsesAndRoundTrips()
    {
        var path = GetCorpusPath("corpus_streaming.vtt");
        var result = Subtitle.Load(path);
        Assert.False(result.HasErrors);

        var doc = result.Document;
        Assert.Equal(SubtitleFormat.WebVtt, doc.Format);
        Assert.Equal(3, doc.Cues.Count);

        var vttData = Assert.IsType<VttDocumentData>(doc.FormatData);
        Assert.Contains("MPEGTS:900000", vttData.TimestampMap);
        Assert.Equal(2, vttData.NonCueBlocks.Count);

        var firstCue = doc.Cues[0];
        var cueData = Assert.IsType<VttCueData>(firstCue.FormatData);
        Assert.NotNull(cueData.Settings);
        Assert.Equal(50.0, cueData.Settings.Position?.Percentage);
        Assert.Equal(90.0, cueData.Settings.Line?.Value);

        // Round trip
        var written = Subtitle.WriteToString(doc, SubtitleFormat.WebVtt);
        var roundTrip = Subtitle.Parse(written, SubtitleFormat.WebVtt);
        Assert.Equal(doc.Cues.Count, roundTrip.Document.Cues.Count);
        var roundVttData = Assert.IsType<VttDocumentData>(roundTrip.Document.FormatData);
        Assert.Equal(2, roundVttData.NonCueBlocks.Count);
    }

    [Fact]
    public void Corpus_StandardSrt_ParsesAndRoundTrips()
    {
        var path = GetCorpusPath("corpus_standard.srt");
        var result = Subtitle.Load(path);
        Assert.False(result.HasErrors);

        var doc = result.Document;
        Assert.Equal(SubtitleFormat.SubRip, doc.Format);
        Assert.Equal(3, doc.Cues.Count);
        Assert.Contains("urgent emphasis", doc.Cues[1].PlainText);
        Assert.DoesNotContain("<b>", doc.Cues[1].PlainText);

        // Round trip
        var written = Subtitle.WriteToString(doc, SubtitleFormat.SubRip);
        var roundTrip = Subtitle.Parse(written, SubtitleFormat.SubRip);
        Assert.Equal(doc.Cues.Count, roundTrip.Document.Cues.Count);
        Assert.Equal(doc.Cues[1].RawText, roundTrip.Document.Cues[1].RawText);
    }

    [Fact]
    public void Corpus_LegacySsa_ParsesAndRoundTrips()
    {
        var path = GetCorpusPath("corpus_legacy.ssa");
        var result = Subtitle.Load(path);
        Assert.False(result.HasErrors);

        var doc = result.Document;
        Assert.Equal(SubtitleFormat.Ssa, doc.Format);
        Assert.Equal(2, doc.Cues.Count);

        var ssaData = Assert.IsType<AssDocumentData>(doc.FormatData);
        Assert.True(ssaData.IsSsa);
        Assert.Equal(2, ssaData.Styles.Count);

        var secondCueData = Assert.IsType<AssCueData>(doc.Cues[1].FormatData);
        Assert.True(secondCueData.Marked);

        // Round trip
        var written = Subtitle.WriteToString(doc, SubtitleFormat.Ssa);
        var roundTrip = Subtitle.Parse(written, SubtitleFormat.Ssa);
        Assert.Equal(doc.Cues.Count, roundTrip.Document.Cues.Count);
    }

    [Fact]
    public void Corpus_CrossFormatConversions_ExecuteSuccessfully()
    {
        var vttPath = GetCorpusPath("corpus_streaming.vtt");
        var vttDoc = Subtitle.Load(vttPath).Document;

        // VTT -> ASS
        var vttToAss = Subtitle.Convert(vttDoc, SubtitleFormat.Ass);
        Assert.True(vttToAss.Report.IsLossless || !vttToAss.Report.HasErrors);
        Assert.Equal(SubtitleFormat.Ass, vttToAss.Document.Format);
        Assert.Equal(3, vttToAss.Document.Cues.Count);

        // VTT -> SRT
        var vttToSrt = Subtitle.Convert(vttDoc, SubtitleFormat.SubRip);
        Assert.Equal(SubtitleFormat.SubRip, vttToSrt.Document.Format);
        Assert.Contains("Host: Welcome to the global technology summit.", vttToSrt.Document.Cues[0].RawText);

        // ASS -> VTT
        var assPath = GetCorpusPath("corpus_aegisub.ass");
        var assDoc = Subtitle.Load(assPath).Document;
        var assToVtt = Subtitle.Convert(assDoc, SubtitleFormat.WebVtt);
        Assert.Equal(SubtitleFormat.WebVtt, assToVtt.Document.Format);
        Assert.Equal(4, assToVtt.Document.Cues.Count);
    }
}
