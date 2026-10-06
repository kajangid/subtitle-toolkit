using System;
using System.Collections.Generic;
using System.Text;
using SubtitleToolkit.Common;
using SubtitleToolkit.Formats.Ass;
using SubtitleToolkit.Model;
using SubtitleToolkit.Model.FormatData;
using Xunit;

namespace SubtitleToolkit.Tests;

public class PropertyRoundTripTests
{
    private static readonly string[] SampleWords = new[]
    {
        "alpha", "beta", "gamma", "delta", "hello", "world", "technology", "quantum",
        "tokyo", "shinji", "subtitle", "streaming", "video", "pipeline", "performance",
        "café", "naïve", "résumé", "日本語", "한국어", "العربية", "🚀", "✨"
    };

    [Fact]
    public void Property_SrtRoundTrip_PreservesAllCuesAndTimings()
    {
        var rng = new Random(1337);
        for (var iter = 0; iter < 25; iter++)
        {
            var cueCount = rng.Next(1, 15);
            var cues = new List<SubtitleCue>();
            var currentMs = rng.Next(0, 5000);

            for (var i = 0; i < cueCount; i++)
            {
                var start = TimeSpan.FromMilliseconds(currentMs);
                var durationMs = rng.Next(500, 3000);
                var end = start + TimeSpan.FromMilliseconds(durationMs);
                currentMs += durationMs + rng.Next(100, 1000);

                var lineCount = rng.Next(1, 4);
                var sb = new StringBuilder();
                for (var l = 0; l < lineCount; l++)
                {
                    if (l > 0) sb.Append('\n');
                    var wordCount = rng.Next(2, 6);
                    for (var w = 0; w < wordCount; w++)
                    {
                        if (w > 0) sb.Append(' ');
                        sb.Append(SampleWords[rng.Next(SampleWords.Length)]);
                    }
                }

                cues.Add(new SubtitleCue(start, end, sb.ToString()));
            }

            var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);
            var serialized = Subtitle.WriteToString(doc, SubtitleFormat.SubRip);
            var parsedResult = Subtitle.Parse(serialized, SubtitleFormat.SubRip);

            Assert.False(parsedResult.HasErrors);
            var roundTripDoc = parsedResult.Document;
            Assert.Equal(doc.Cues.Count, roundTripDoc.Cues.Count);

            for (var i = 0; i < doc.Cues.Count; i++)
            {
                Assert.Equal(doc.Cues[i].Start, roundTripDoc.Cues[i].Start);
                Assert.Equal(doc.Cues[i].End, roundTripDoc.Cues[i].End);
                Assert.Equal(doc.Cues[i].RawText, roundTripDoc.Cues[i].RawText);
            }
        }
    }

    [Fact]
    public void Property_WebVttRoundTrip_PreservesCuesAndSettings()
    {
        var rng = new Random(4242);
        for (var iter = 0; iter < 25; iter++)
        {
            var cueCount = rng.Next(1, 12);
            var cues = new List<SubtitleCue>();
            var currentMs = rng.Next(0, 3000);

            for (var i = 0; i < cueCount; i++)
            {
                var start = TimeSpan.FromMilliseconds(currentMs);
                var durationMs = rng.Next(500, 2500);
                var end = start + TimeSpan.FromMilliseconds(durationMs);
                currentMs += durationMs + rng.Next(200, 500);

                var text = SampleWords[rng.Next(SampleWords.Length)] + " " + SampleWords[rng.Next(SampleWords.Length)];

                VttCueSettings? settings = null;
                if (rng.Next(2) == 0)
                {
                    settings = new VttCueSettings(
                        position: new VttPositionSetting { Percentage = rng.Next(10, 90) },
                        line: new VttLineSetting { Value = rng.Next(10, 90), IsPercentage = true },
                        align: (VttAlignment)rng.Next(0, 5));
                }

                var cueData = settings != null ? new VttCueData(null, settings.ToSettingsString()) : null;
                cues.Add(new SubtitleCue(start, end, text, cueData));
            }

            var doc = new SubtitleDocument(SubtitleFormat.WebVtt, cues, new VttDocumentData());
            var serialized = Subtitle.WriteToString(doc, SubtitleFormat.WebVtt);
            var parsed = Subtitle.Parse(serialized, SubtitleFormat.WebVtt).Document;

            Assert.Equal(doc.Cues.Count, parsed.Cues.Count);
            for (var i = 0; i < doc.Cues.Count; i++)
            {
                Assert.Equal(doc.Cues[i].Start, parsed.Cues[i].Start);
                Assert.Equal(doc.Cues[i].End, parsed.Cues[i].End);
                Assert.Equal(doc.Cues[i].RawText, parsed.Cues[i].RawText);
            }
        }
    }

    [Fact]
    public void Property_TimeShift_OffsetAdditionAndSubtraction_IsSymmetric()
    {
        var rng = new Random(8888);
        var cues = new List<SubtitleCue>
        {
            new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), "Cue 1"),
            new(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(25), "Cue 2"),
            new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(35), "Cue 3")
        };
        var doc = new SubtitleDocument(SubtitleFormat.SubRip, cues);

        for (var i = 0; i < 20; i++)
        {
            var offsetMs = rng.Next(100, 5000);
            var offset = TimeSpan.FromMilliseconds(offsetMs);

            // Shifting forward then shifting backward by the same offset yields identical document
            var forward = doc.TimeShift(offset);
            var restored = forward.TimeShift(-offset);

            Assert.Equal(doc.Cues.Count, restored.Cues.Count);
            for (var c = 0; c < doc.Cues.Count; c++)
            {
                Assert.Equal(doc.Cues[c].Start, restored.Cues[c].Start);
                Assert.Equal(doc.Cues[c].End, restored.Cues[c].End);
                Assert.Equal(doc.Cues[c].RawText, restored.Cues[c].RawText);
            }
        }
    }

    [Fact]
    public void Property_AssAst_RandomDialogueSequences_RoundTripVerbatim()
    {
        var rng = new Random(9999);
        for (var iter = 0; iter < 25; iter++)
        {
            var elements = new List<AssDialogueElement>();
            var elementCount = rng.Next(2, 6);

            for (var e = 0; e < elementCount; e++)
            {
                if (e % 2 == 0)
                {
                    // Tag block
                    var tags = new List<AssTag>();
                    var tagCount = rng.Next(1, 3);
                    for (var t = 0; t < tagCount; t++)
                    {
                        var tagChoice = rng.Next(5);
                        tags.Add(tagChoice switch
                        {
                            0 => new BoldTag(Enabled: rng.Next(2) == 1),
                            1 => new ItalicTag(Enabled: rng.Next(2) == 1),
                            2 => new PosTag(rng.Next(10, 1920), rng.Next(10, 1080)),
                            3 => new ColorTag(1, "&H00FFFF&", UseShorthand: true),
                            _ => new AlignmentTag(rng.Next(1, 10))
                        });
                    }
                    elements.Add(new AssTagBlockElement(tags));
                }
                else
                {
                    // Text element
                    var text = SampleWords[rng.Next(SampleWords.Length)] + " " + SampleWords[rng.Next(SampleWords.Length)];
                    elements.Add(new AssTextElement(text));
                }
            }

            var ast = new AssDialogueAst(elements);
            var rendered = ast.Render();
            var parsedAst = AssDialogueAst.Parse(rendered);

            Assert.Equal(rendered, parsedAst.Render());
        }
    }
}
