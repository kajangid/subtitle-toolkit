using System.Linq;
using SubtitleToolkit.Formats;
using SubtitleToolkit.Formats.Ass;
using Xunit;

namespace SubtitleToolkit.Tests;

public class AssAstTests
{
    [Fact]
    public void ParseTag_BoldTag_ParsesEnabledAndWeight()
    {
        var tag1 = (BoldTag)AssTagParser.ParseTag(new AssTagToken("b", "1"));
        Assert.True(tag1.Enabled);
        Assert.Null(tag1.Weight);
        Assert.Equal(@"\b1", tag1.ToString());

        var tag0 = (BoldTag)AssTagParser.ParseTag(new AssTagToken("b", "0"));
        Assert.False(tag0.Enabled);
        Assert.Equal(@"\b0", tag0.ToString());

        var tagWeight = (BoldTag)AssTagParser.ParseTag(new AssTagToken("b", "700"));
        Assert.Equal(700, tagWeight.Weight);
        Assert.Equal(@"\b700", tagWeight.ToString());
    }

    [Fact]
    public void ParseTag_ItalicUnderlineStrikeout_ParsedAccurately()
    {
        var iTag = (ItalicTag)AssTagParser.ParseTag(new AssTagToken("i", "1"));
        Assert.True(iTag.Enabled);
        Assert.Equal(@"\i1", iTag.ToString());

        var uTag = (UnderlineTag)AssTagParser.ParseTag(new AssTagToken("u", "1"));
        Assert.True(uTag.Enabled);
        Assert.Equal(@"\u1", uTag.ToString());

        var sTag = (StrikeoutTag)AssTagParser.ParseTag(new AssTagToken("s", "0"));
        Assert.False(sTag.Enabled);
        Assert.Equal(@"\s0", sTag.ToString());
    }

    [Fact]
    public void ParseTag_ColorAndAlpha_ParsesHexComponents()
    {
        // ASS color is &HBBGGRR& -> Blue=00, Green=00, Red=FF
        var colorToken = new AssTagToken("1c", "&H0000FF&");
        var colorTag = (ColorTag)AssTagParser.ParseTag(colorToken);

        Assert.Equal(1, colorTag.Target);
        Assert.Equal((byte)255, colorTag.Red);
        Assert.Equal((byte)0, colorTag.Green);
        Assert.Equal((byte)0, colorTag.Blue);
        Assert.Equal(@"\1c&H0000FF&", colorTag.ToString());

        // Alpha tag
        var alphaToken = new AssTagToken("alpha", "&H80&");
        var alphaTag = (AlphaTag)AssTagParser.ParseTag(alphaToken);
        Assert.Equal(0, alphaTag.Target);
        Assert.Equal((byte)128, alphaTag.Alpha);
        Assert.Equal(@"\alpha&H80&", alphaTag.ToString());
    }

    [Fact]
    public void ParseTag_PosAndMoveAndOrg_ParsesCoordinates()
    {
        var posToken = new AssTagToken("pos", "(192.5,200)");
        var posTag = (PosTag)AssTagParser.ParseTag(posToken);
        Assert.Equal(192.5, posTag.X);
        Assert.Equal(200.0, posTag.Y);
        Assert.Equal(@"\pos(192.5,200)", posTag.ToString());

        var moveToken = new AssTagToken("move", "(10,20,100,200,0,500)");
        var moveTag = (MoveTag)AssTagParser.ParseTag(moveToken);
        Assert.Equal(10, moveTag.X1);
        Assert.Equal(20, moveTag.Y1);
        Assert.Equal(100, moveTag.X2);
        Assert.Equal(200, moveTag.Y2);
        Assert.Equal(0, moveTag.T1);
        Assert.Equal(500, moveTag.T2);
        Assert.Equal(@"\move(10,20,100,200,0,500)", moveTag.ToString());

        var orgToken = new AssTagToken("org", "(50,50)");
        var orgTag = (OrgTag)AssTagParser.ParseTag(orgToken);
        Assert.Equal(50, orgTag.X);
        Assert.Equal(50, orgTag.Y);
        Assert.Equal(@"\org(50,50)", orgTag.ToString());
    }

    [Fact]
    public void ParseTag_FadeAndKaraoke_ParsedAccurately()
    {
        var fadToken = new AssTagToken("fad", "(500,500)");
        var fadTag = (FadeTag)AssTagParser.ParseTag(fadToken);
        Assert.True(fadTag.IsSimple);
        Assert.Equal(500, fadTag.FadeIn);
        Assert.Equal(500, fadTag.FadeOut);
        Assert.Equal(@"\fad(500,500)", fadTag.ToString());

        var kToken = new AssTagToken("kf", "120");
        var kTag = (KaraokeTag)AssTagParser.ParseTag(kToken);
        Assert.Equal("kf", kTag.Type);
        Assert.Equal(120, kTag.DurationCentiseconds);
        Assert.Equal(@"\kf120", kTag.ToString());
    }

    [Fact]
    public void ParseTag_DrawingAndAlignment_ParsedAccurately()
    {
        var pToken = new AssTagToken("p", "1");
        var pTag = (DrawingTag)AssTagParser.ParseTag(pToken);
        Assert.Equal(1, pTag.Scale);
        Assert.Equal(@"\p1", pTag.ToString());

        var pboToken = new AssTagToken("pbo", "-5");
        var pboTag = (DrawingTag)AssTagParser.ParseTag(pboToken);
        Assert.Equal(-5, pboTag.BaselineOffset);
        Assert.Equal(@"\pbo-5", pboTag.ToString());

        var anToken = new AssTagToken("an", "5");
        var anTag = (AlignmentTag)AssTagParser.ParseTag(anToken);
        Assert.Equal(5, anTag.Alignment);
        Assert.False(anTag.IsLegacy);
        Assert.Equal(@"\an5", anTag.ToString());
    }

    [Fact]
    public void ParseTag_TransformWithNestedTags_ParsesNestedAst()
    {
        var tToken = new AssTagToken("t", @"(0,500,\fscx120\fscy120)");
        var tTag = (TransformTag)AssTagParser.ParseTag(tToken);

        Assert.Equal(0, tTag.T1);
        Assert.Equal(500, tTag.T2);
        Assert.NotNull(tTag.NestedTags);
        Assert.Equal(2, tTag.NestedTags!.Count);

        var scaleX = Assert.IsType<FontScaleTag>(tTag.NestedTags[0]);
        Assert.Equal('x', scaleX.Axis);
        Assert.Equal(120, scaleX.Scale);

        var scaleY = Assert.IsType<FontScaleTag>(tTag.NestedTags[1]);
        Assert.Equal('y', scaleY.Axis);
        Assert.Equal(120, scaleY.Scale);
    }

    [Fact]
    public void ParseTag_UnknownTagFallback_PreservesRoundTrip()
    {
        var customToken = new AssTagToken("customExt", "(1,2,3)");
        var customTag = (UnknownTag)AssTagParser.ParseTag(customToken);

        Assert.Equal("customExt", customTag.TagName);
        Assert.Equal("(1,2,3)", customTag.RawArgs);
        Assert.Equal(@"\customExt(1,2,3)", customTag.ToString());
    }

    [Fact]
    public void DialogueAst_ParseAndRender_RoundTripsVerbatim()
    {
        var rawText = @"{\pos(192,200)\b1}Hello {\i1}World!{\r}";
        var ast = AssDialogueAst.Parse(rawText);

        Assert.Equal(5, ast.Elements.Count);

        // Block 1
        var b1 = Assert.IsType<AssTagBlockElement>(ast.Elements[0]);
        Assert.Equal(2, b1.Tags.Count);
        Assert.IsType<PosTag>(b1.Tags[0]);
        Assert.IsType<BoldTag>(b1.Tags[1]);

        // Text 1
        var t1 = Assert.IsType<AssTextElement>(ast.Elements[1]);
        Assert.Equal("Hello ", t1.Text);

        // Block 2
        var b2 = Assert.IsType<AssTagBlockElement>(ast.Elements[2]);
        Assert.Single(b2.Tags);
        Assert.IsType<ItalicTag>(b2.Tags[0]);

        // Text 2
        var t2 = Assert.IsType<AssTextElement>(ast.Elements[3]);
        Assert.Equal("World!", t2.Text);

        // Block 3
        var b3 = Assert.IsType<AssTagBlockElement>(ast.Elements[4]);
        Assert.Single(b3.Tags);
        Assert.IsType<ResetTag>(b3.Tags[0]);

        Assert.Equal(rawText, ast.Render());
    }
}
