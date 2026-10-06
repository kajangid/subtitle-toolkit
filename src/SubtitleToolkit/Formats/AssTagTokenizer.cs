using System;
using System.Collections.Generic;
using System.Text;

namespace SubtitleToolkit.Formats;

/// <summary>
/// Represents a single token within an ASS override block ({\...}),
/// containing the tag name and raw verbatim argument payload.
/// </summary>
public readonly struct AssTagToken : IEquatable<AssTagToken>
{
    /// <summary>
    /// The tag name (e.g. "b", "i", "fs", "pos", "1c", "t"),
    /// or empty string for comments / plain text within the block.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The verbatim raw argument string (e.g. "1", "(100,200)", "&amp;H0000FF&amp;").
    /// </summary>
    public string RawArgs { get; }

    /// <summary>
    /// Returns true if this token is a tag (non-empty <see cref="Name"/>), false if comment/plain text.
    /// </summary>
    public bool IsTag => !string.IsNullOrEmpty(Name);

    public AssTagToken(string name, string rawArgs)
    {
        Name = name ?? string.Empty;
        RawArgs = rawArgs ?? string.Empty;
    }

    /// <summary>
    /// Gets the argument stripped of surrounding parentheses if present, or <see cref="RawArgs"/> as-is.
    /// </summary>
    public string TrimmedArgs =>
        RawArgs.Length >= 2 && RawArgs.StartsWith("(") && RawArgs.EndsWith(")")
            ? RawArgs.Substring(1, RawArgs.Length - 2)
            : RawArgs;

    /// <summary>
    /// Reconstructs the verbatim tag representation (e.g. "\pos(100,200)" or "comment").
    /// </summary>
    public override string ToString() => IsTag ? "\\" + Name + RawArgs : RawArgs;

    public bool Equals(AssTagToken other) =>
        string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(RawArgs, other.RawArgs, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AssTagToken other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Name?.GetHashCode() ?? 0) * 397) ^ (RawArgs?.GetHashCode() ?? 0);
        }
    }

    public static bool operator ==(AssTagToken left, AssTagToken right) => left.Equals(right);
    public static bool operator !=(AssTagToken left, AssTagToken right) => !left.Equals(right);
}

/// <summary>
/// High-performance lossless tokenizer and serializer for ASS override blocks ({\...}).
/// </summary>
public static class AssTagTokenizer
{
    private static readonly string[] KnownTags = new[]
    {
        // 5 chars
        "xbord", "ybord", "xshad", "yshad", "alpha",
        // 4 chars
        "fscx", "fscy", "bord", "shad", "blur", "fade", "clip", "move", "iclip",
        // 3 chars
        "fsp", "frx", "fry", "frz", "fad", "pos", "org", "pbo",
        // 2 chars
        "fn", "fs", "fr", "fe",
        "1c", "2c", "3c", "4c",
        "1a", "2a", "3a", "4a",
        "an", "be", "kf", "ko", "kt",
        // 1 char
        "b", "i", "u", "s", "c", "a", "k", "K", "q", "r", "p", "t", "h"
    };

    /// <summary>
    /// Splits an ASS override block ({\...} or \...) into (Name, RawArgs) tokens with 100% round-trip fidelity.
    /// </summary>
    public static IReadOnlyList<AssTagToken> TokenizeBlock(string block)
    {
        if (string.IsNullOrEmpty(block))
            return Array.Empty<AssTagToken>();

        var text = block;
        if (text.StartsWith("{") && text.EndsWith("}") && text.Length >= 2)
        {
            text = text.Substring(1, text.Length - 2);
        }

        var tokens = new List<AssTagToken>();
        var i = 0;

        while (i < text.Length)
        {
            if (text[i] != '\\')
            {
                var nextSlash = text.IndexOf('\\', i);
                if (nextSlash < 0)
                {
                    tokens.Add(new AssTagToken(string.Empty, text.Substring(i)));
                    break;
                }
                tokens.Add(new AssTagToken(string.Empty, text.Substring(i, nextSlash - i)));
                i = nextSlash;
                continue;
            }

            // Skip '\'
            i++;
            if (i >= text.Length)
            {
                tokens.Add(new AssTagToken(string.Empty, "\\"));
                break;
            }

            // Check if next char is also '\' (empty tag / escaped slash)
            if (text[i] == '\\')
            {
                tokens.Add(new AssTagToken(string.Empty, "\\"));
                continue;
            }

            // Match known tag (longest prefix first)
            string? matchedName = null;
            for (var k = 0; k < KnownTags.Length; k++)
            {
                var tag = KnownTags[k];
                if (text.Length - i >= tag.Length && string.CompareOrdinal(text, i, tag, 0, tag.Length) == 0)
                {
                    matchedName = tag;
                    break;
                }
            }

            string tagName;
            if (matchedName != null)
            {
                tagName = matchedName;
                i += tagName.Length;
            }
            else
            {
                // Unknown tag: read identifier until '(', '\', or non-letter/digit
                var startName = i;
                while (i < text.Length && text[i] != '\\' && text[i] != '(' && (char.IsLetterOrDigit(text[i]) || i == startName))
                {
                    i++;
                }
                tagName = text.Substring(startName, i - startName);
            }

            // Determine RawArgs
            string rawArgs;
            if (i < text.Length && text[i] == '(')
            {
                // Parenthesized argument: scan with balanced parens
                var startArg = i;
                var depth = 0;
                while (i < text.Length)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            i++;
                            break;
                        }
                    }
                    i++;
                }
                rawArgs = text.Substring(startArg, i - startArg);
            }
            else
            {
                // Argument up to next '\'
                var nextSlash = text.IndexOf('\\', i);
                if (nextSlash >= 0)
                {
                    rawArgs = text.Substring(i, nextSlash - i);
                    i = nextSlash;
                }
                else
                {
                    rawArgs = text.Substring(i);
                    i = text.Length;
                }
            }

            tokens.Add(new AssTagToken(tagName, rawArgs));
        }

        return tokens;
    }

    /// <summary>
    /// Convenience alias for <see cref="TokenizeBlock"/>.
    /// </summary>
    public static IReadOnlyList<AssTagToken> Tokenize(string block) => TokenizeBlock(block);

    /// <summary>
    /// Formats tokens back into a complete ASS override block enclosed in '{' and '}'.
    /// </summary>
    public static string FormatBlock(IEnumerable<AssTagToken> tokens)
    {
        if (tokens == null) throw new ArgumentNullException(nameof(tokens));
        var sb = new StringBuilder("{");
        foreach (var t in tokens)
        {
            sb.Append(t.ToString());
        }
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// Formats tokens back into tag sequence without enclosing '{' and '}'.
    /// </summary>
    public static string FormatTags(IEnumerable<AssTagToken> tokens)
    {
        if (tokens == null) throw new ArgumentNullException(nameof(tokens));
        var sb = new StringBuilder();
        foreach (var t in tokens)
        {
            sb.Append(t.ToString());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts and tokenizes all override blocks ({\...}) from an ASS dialogue line.
    /// </summary>
    public static IEnumerable<IReadOnlyList<AssTagToken>> ExtractBlocks(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf('{', i);
            if (start < 0) break;
            var end = text.IndexOf('}', start);
            if (end < 0) break;
            yield return TokenizeBlock(text.Substring(start, end - start + 1));
            i = end + 1;
        }
    }
}
