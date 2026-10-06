using System;
using System.Collections.Generic;
using SubtitleToolkit.Common;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class TimeShiftTests
{
    [Fact]
    public void Shift_PositiveOffset_ShiftsAllCuesForward()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Cue 1"),
            new SubtitleCue(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), "Cue 2")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var shifted = doc.TimeShift(TimeSpan.FromSeconds(2));

        Assert.Equal(2, shifted.Cues.Count);
        Assert.Equal(TimeSpan.FromSeconds(3), shifted.Cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(5), shifted.Cues[0].End);
        Assert.Equal(TimeSpan.FromSeconds(7), shifted.Cues[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(9), shifted.Cues[1].End);
        // Original document unchanged (immutability)
        Assert.Equal(TimeSpan.FromSeconds(1), doc.Cues[0].Start);
    }

    [Fact]
    public void Shift_NegativeOffset_DefaultClampsToZero()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), "Cue 1"),
            new SubtitleCue(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1.5), "Cue 2")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var shifted = doc.TimeShift(TimeSpan.FromSeconds(-2));

        // Cue 1: 1s - 2s = -1s -> clamped to 0s; 4s - 2s = 2s
        Assert.Equal(TimeSpan.Zero, shifted.Cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2), shifted.Cues[0].End);

        // Cue 2: 0.5s - 2s = -1.5s -> clamped to 0s; 1.5s - 2s = -0.5s -> clamped to 0s
        Assert.Equal(TimeSpan.Zero, shifted.Cues[1].Start);
        Assert.Equal(TimeSpan.Zero, shifted.Cues[1].End);
    }

    [Fact]
    public void Shift_NegativeOffset_ClampFalse_AllowsNegativeTimes()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Cue 1")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var shifted = doc.TimeShift(TimeSpan.FromSeconds(-3), new TimeShiftOptions
        {
            ClampNegativeToZero = false
        });

        Assert.Equal(TimeSpan.FromSeconds(-2), shifted.Cues[0].Start);
        Assert.Equal(TimeSpan.Zero, shifted.Cues[0].End);
    }

    [Fact]
    public void Shift_DropNegativeCues_DropsCuesEndingAtOrBeforeZero()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Drop me"),
            new SubtitleCue(TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(4), "Keep me"),
            new SubtitleCue(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8), "Keep me too")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        var shifted = doc.TimeShift(TimeSpan.FromSeconds(-2.5), new TimeShiftOptions
        {
            DropNegativeCues = true,
            ClampNegativeToZero = true
        });

        // Cue 1 was 1s -> 2s; shifted by -2.5s -> end is -0.5s <= 0 => dropped
        // Cue 2 was 1.5s -> 4s; shifted by -2.5s -> start -1s (clamped to 0), end 1.5s => kept
        // Cue 3 was 5s -> 8s; shifted by -2.5s -> start 2.5s, end 5.5s => kept
        Assert.Equal(2, shifted.Cues.Count);
        Assert.Equal("Keep me", shifted.Cues[0].RawText);
        Assert.Equal(TimeSpan.Zero, shifted.Cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(1.5), shifted.Cues[0].End);
        Assert.Equal("Keep me too", shifted.Cues[1].RawText);
        Assert.Equal(TimeSpan.FromSeconds(2.5), shifted.Cues[1].Start);
    }

    [Fact]
    public void Shift_SelectiveFilter_ShiftsOnlyMatchingCues()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Intro"),
            new SubtitleCue(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12), "Body 1"),
            new SubtitleCue(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(17), "Body 2")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        // Only shift cues after 5s
        var shifted = doc.TimeShift(TimeSpan.FromSeconds(5), new TimeShiftOptions
        {
            Filter = cue => cue.Start >= TimeSpan.FromSeconds(5)
        });

        // "Intro" remains unshifted
        Assert.Equal(TimeSpan.FromSeconds(1), shifted.Cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(3), shifted.Cues[0].End);

        // "Body 1" and "Body 2" shifted by +5s
        Assert.Equal(TimeSpan.FromSeconds(15), shifted.Cues[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(17), shifted.Cues[1].End);
        Assert.Equal(TimeSpan.FromSeconds(20), shifted.Cues[2].Start);
        Assert.Equal(TimeSpan.FromSeconds(22), shifted.Cues[2].End);
    }

    [Fact]
    public void Shift_WebVtt_ReordersCuesChronologically()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), "Late"),
            new SubtitleCue(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10), "Early shifted")
        };
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues);

        // Shift only second cue by -6s so its start becomes 2s (before the 5s cue)
        var shifted = doc.TimeShift(TimeSpan.FromSeconds(-6), new TimeShiftOptions
        {
            Filter = cue => cue.RawText == "Early shifted"
        });

        // WebVTT guarantee: sorted by Start time
        Assert.Equal(2, shifted.Cues.Count);
        Assert.Equal("Early shifted", shifted.Cues[0].RawText);
        Assert.Equal(TimeSpan.FromSeconds(2), shifted.Cues[0].Start);
        Assert.Equal("Late", shifted.Cues[1].RawText);
        Assert.Equal(TimeSpan.FromSeconds(5), shifted.Cues[1].Start);
    }

    [Fact]
    public void Shift_WebVtt_AdjustsXTimestampMap()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Hello")
        };
        var vttData = new VttDocumentData(
            timestampMap: "X-TIMESTAMP-MAP=LOCAL:00:00:00.000,MPEGTS:900000"
        );
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues, vttData);

        var shifted = doc.TimeShift(TimeSpan.FromSeconds(3.5));
        var shiftedVttData = Assert.IsType<VttDocumentData>(shifted.FormatData);

        Assert.Equal("X-TIMESTAMP-MAP=LOCAL:00:00:03.500,MPEGTS:900000", shiftedVttData.TimestampMap);
    }

    [Fact]
    public void Shift_WebVtt_AdjustsXTimestampMap_MpegTsFirst()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), "Hello")
        };
        var vttData = new VttDocumentData(
            timestampMap: "X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:01:00.000"
        );
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues, vttData);

        var shifted = doc.TimeShift(TimeSpan.FromSeconds(-10));
        var shiftedVttData = Assert.IsType<VttDocumentData>(shifted.FormatData);

        Assert.Equal("X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:50.000", shiftedVttData.TimestampMap);
    }

    [Fact]
    public void Shift_WebVtt_RemapsAnchoredBlocks_WhenCuesDropped()
    {
        var cues = new[]
        {
            new SubtitleCue(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Cue 0 - will drop"),
            new SubtitleCue(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), "Cue 1 - will keep")
        };
        var blocks = new[]
        {
            new VttAnchoredBlock(0, VttBlockKind.Note, "Header Note"),
            new VttAnchoredBlock(1, VttBlockKind.Style, "Style before Cue 1"),
            new VttAnchoredBlock(2, VttBlockKind.Note, "Trailing Note")
        };
        var vttData = new VttDocumentData(nonCueBlocks: blocks);
        var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues, vttData);

        // Shift by -3s with DropNegativeCues = true (Cue 0 dropped, Cue 1 survives at index 0)
        var shifted = doc.TimeShift(TimeSpan.FromSeconds(-3), new TimeShiftOptions
        {
            DropNegativeCues = true
        });

        Assert.Single(shifted.Cues);
        var shiftedVttData = Assert.IsType<VttDocumentData>(shifted.FormatData);
        Assert.Equal(3, shiftedVttData.NonCueBlocks.Count);

        // Block 0: index 0 (header) -> remains 0
        Assert.Equal(0, shiftedVttData.NonCueBlocks[0].BeforeCueIndex);
        // Block 1: was before Cue 1 -> Cue 1 is now at index 0 -> becomes 0
        Assert.Equal(0, shiftedVttData.NonCueBlocks[1].BeforeCueIndex);
        // Block 2: trailing after original cues (was 2) -> now trailing after 1 cue -> becomes 1
        Assert.Equal(1, shiftedVttData.NonCueBlocks[2].BeforeCueIndex);
    }

    [Fact]
    public void Cue_TimeShift_CreatesShiftedCopy()
    {
        var cue = new SubtitleCue(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), "Test");
        var shifted = cue.TimeShift(TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(5), shifted.Start);
        Assert.Equal(TimeSpan.FromSeconds(8), shifted.End);
        Assert.Equal("Test", shifted.RawText);
    }
}
