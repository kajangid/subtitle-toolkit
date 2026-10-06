using System;
using SubtitleToolkit.IO;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class SsaTests
{
    [Fact]
    public void Parse_SsaV4Script_MaintainsMarkedAndSsaAlignments()
    {
        var ssa = @"[Script Info]
Title: Legacy SSA
ScriptType: v4.00

[V4 Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding
Style: TopTitle,Arial,28,&H00FFFFFF,&H0000FFFF,&H00000000,&H00000000,-1,0,1,2,2,6,30,30,30,0,0

[Events]
Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: Marked=1,0:00:01.00,0:00:04.00,TopTitle,Speaker,0,0,0,,Legacy SSA line
";

        var result = Subtitle.Parse(ssa);
        var doc = result.Document;

        Assert.Equal(SubtitleFormat.Ssa, doc.Format);
        var ssaDocData = Assert.IsType<AssDocumentData>(doc.FormatData);
        Assert.True(ssaDocData.IsSsa);

        // Style alignment 6 in SSA v4 is Top-Center (in ASS numpad it would be Mid-Right)
        Assert.Equal(6, ssaDocData.Styles[0].Alignment);

        // Event uses Marked=1
        var cue = doc.Cues[0];
        var cueData = Assert.IsType<AssCueData>(cue.FormatData);
        Assert.True(cueData.IsSsa);
        Assert.True(cueData.Marked);
        Assert.Equal("Legacy SSA line", cue.RawText);

        // Verify write maintains Marked=1 and [V4 Styles]
        var output = Subtitle.WriteToString(doc, SubtitleFormat.Ssa, new SubtitleWriteOptions { LineEnding = "\n" });
        Assert.Contains("[V4 Styles]", output);
        Assert.Contains("Marked=1", output);
    }
}
