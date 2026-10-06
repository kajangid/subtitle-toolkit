using System.Linq;
using SubtitleToolkit.Formats;
using Xunit;

namespace SubtitleToolkit.Tests;

public class AssTagTokenizerTests
{
    [Fact]
    public void TokenizeBlock_BasicTags_SplitsCorrectly()
    {
        var input = @"{\b1\i1\fs24\fnArial}";
        var tokens = AssTagTokenizer.TokenizeBlock(input);

        Assert.Equal(4, tokens.Count);

        Assert.Equal("b", tokens[0].Name);
        Assert.Equal("1", tokens[0].RawArgs);
        Assert.True(tokens[0].IsTag);

        Assert.Equal("i", tokens[1].Name);
        Assert.Equal("1", tokens[1].RawArgs);

        Assert.Equal("fs", tokens[2].Name);
        Assert.Equal("24", tokens[2].RawArgs);

        Assert.Equal("fn", tokens[3].Name);
        Assert.Equal("Arial", tokens[3].RawArgs);
    }

    [Fact]
    public void TokenizeBlock_ColorAndAlphaTags_ParsedAccurately()
    {
        var input = @"{\c&H0000FF&\1c&HFFFFFF&\3c&H000000&\alpha&H80&\1a&HFF&}";
        var tokens = AssTagTokenizer.TokenizeBlock(input);

        Assert.Equal(5, tokens.Count);
        Assert.Equal("c", tokens[0].Name);
        Assert.Equal("&H0000FF&", tokens[0].RawArgs);

        Assert.Equal("1c", tokens[1].Name);
        Assert.Equal("&HFFFFFF&", tokens[1].RawArgs);

        Assert.Equal("3c", tokens[2].Name);
        Assert.Equal("&H000000&", tokens[2].RawArgs);

        Assert.Equal("alpha", tokens[3].Name);
        Assert.Equal("&H80&", tokens[3].RawArgs);

        Assert.Equal("1a", tokens[4].Name);
        Assert.Equal("&HFF&", tokens[4].RawArgs);
    }

    [Fact]
    public void TokenizeBlock_ParenthesizedTags_PreservesFullArgsAndParens()
    {
        var input = @"{\pos(192,200)\org(100,50)\fad(500,500)\clip(10,20,300,400)}";
        var tokens = AssTagTokenizer.TokenizeBlock(input);

        Assert.Equal(4, tokens.Count);

        Assert.Equal("pos", tokens[0].Name);
        Assert.Equal("(192,200)", tokens[0].RawArgs);
        Assert.Equal("192,200", tokens[0].TrimmedArgs);

        Assert.Equal("org", tokens[1].Name);
        Assert.Equal("(100,50)", tokens[1].RawArgs);
        Assert.Equal("100,50", tokens[1].TrimmedArgs);

        Assert.Equal("fad", tokens[2].Name);
        Assert.Equal("(500,500)", tokens[2].RawArgs);

        Assert.Equal("clip", tokens[3].Name);
        Assert.Equal("(10,20,300,400)", tokens[3].RawArgs);
    }

    [Fact]
    public void TokenizeBlock_TransformWithNestedTags_KeepsInnerTagsInsideRawArgs()
    {
        var input = @"{\t(0,500,\fscx120\fscy120)\b0}";
        var tokens = AssTagTokenizer.TokenizeBlock(input);

        Assert.Equal(2, tokens.Count);

        Assert.Equal("t", tokens[0].Name);
        Assert.Equal(@"(0,500,\fscx120\fscy120)", tokens[0].RawArgs);
        Assert.Equal(@"0,500,\fscx120\fscy120", tokens[0].TrimmedArgs);

        Assert.Equal("b", tokens[1].Name);
        Assert.Equal("0", tokens[1].RawArgs);
    }

    [Fact]
    public void TokenizeBlock_ResetTag_WithAndWithoutStyleName()
    {
        var input = @"{\r\b1\rAltStyle}";
        var tokens = AssTagTokenizer.TokenizeBlock(input);

        Assert.Equal(3, tokens.Count);

        Assert.Equal("r", tokens[0].Name);
        Assert.Equal(string.Empty, tokens[0].RawArgs);

        Assert.Equal("b", tokens[1].Name);
        Assert.Equal("1", tokens[1].RawArgs);

        Assert.Equal("r", tokens[2].Name);
        Assert.Equal("AltStyle", tokens[2].RawArgs);
    }

    [Fact]
    public void TokenizeBlock_InlineComments_PreservedAsNonTagTokens()
    {
        var input = @"{Intro note\b1\i1}";
        var tokens = AssTagTokenizer.TokenizeBlock(input);

        Assert.Equal(3, tokens.Count);
        Assert.False(tokens[0].IsTag);
        Assert.Equal(string.Empty, tokens[0].Name);
        Assert.Equal("Intro note", tokens[0].RawArgs);

        Assert.Equal("b", tokens[1].Name);
        Assert.Equal("1", tokens[1].RawArgs);

        Assert.Equal("i", tokens[2].Name);
        Assert.Equal("1", tokens[2].RawArgs);
    }

    [Theory]
    [InlineData(@"{\b1\i1}")]
    [InlineData(@"{\pos(100,200)\fs24\fnArial}")]
    [InlineData(@"{\c&H0000FF&\alpha&H80&\an5}")]
    [InlineData(@"{\clip(m 0 0 l 100 0 100 100 0 100)\p1}")]
    [InlineData(@"{\t(0,500,\fscx120\fscy120)\rCustom}")]
    [InlineData(@"{comment\b1\pos(50,50)}")]
    [InlineData(@"{\xbord2\ybord3\xshad1\yshad2\blur1.5\be1}")]
    public void FormatBlock_RoundTrip_MatchesOriginalVerbatim(string original)
    {
        var tokens = AssTagTokenizer.TokenizeBlock(original);
        var reconstructed = AssTagTokenizer.FormatBlock(tokens);

        Assert.Equal(original, reconstructed);
    }

    [Fact]
    public void ExtractBlocks_FromDialogueText_ExtractsAllBlocks()
    {
        var text = @"{\pos(100,200)\b1}Hello {\i1}world!{\r}";
        var blocks = AssTagTokenizer.ExtractBlocks(text).ToList();

        Assert.Equal(3, blocks.Count);

        // Block 1: \pos and \b1
        Assert.Equal(2, blocks[0].Count);
        Assert.Equal("pos", blocks[0][0].Name);
        Assert.Equal("b", blocks[0][1].Name);

        // Block 2: \i1
        Assert.Single(blocks[1]);
        Assert.Equal("i", blocks[1][0].Name);

        // Block 3: \r
        Assert.Single(blocks[2]);
        Assert.Equal("r", blocks[2][0].Name);
    }
}
