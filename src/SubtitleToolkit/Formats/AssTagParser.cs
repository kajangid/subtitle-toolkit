using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SubtitleToolkit.Formats.Ass;

/// <summary>
/// Parser converting low-level <see cref="AssTagToken"/> instances into strongly-typed <see cref="AssTag"/> AST records.
/// </summary>
public static class AssTagParser
{
    /// <summary>
    /// Parses a single <see cref="AssTagToken"/> into a strongly-typed <see cref="AssTag"/>.
    /// </summary>
    public static AssTag ParseTag(AssTagToken token)
    {
        var name = token.Name;
        var rawArgs = token.RawArgs;

        if (string.IsNullOrEmpty(name))
        {
            return new UnknownTag(string.Empty, rawArgs);
        }

        switch (name)
        {
            case "b":
                if (string.IsNullOrEmpty(rawArgs)) return new BoldTag();
                if (int.TryParse(rawArgs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bVal))
                {
                    return bVal > 1 ? new BoldTag(Weight: bVal) : new BoldTag(Enabled: bVal != 0);
                }
                return new UnknownTag(name, rawArgs);

            case "i":
                return new ItalicTag(rawArgs != "0");

            case "u":
                return new UnderlineTag(rawArgs != "0");

            case "s":
                return new StrikeoutTag(rawArgs != "0");

            case "c":
            case "1c":
            case "2c":
            case "3c":
            case "4c":
                var target = name == "c" ? 1 : (name[0] - '0');
                var isShorthand = name == "c";
                byte? r = null, g = null, b = null;
                var colorMatch = Regex.Match(rawArgs, @"&H([0-9a-fA-F]{6})&", RegexOptions.IgnoreCase);
                if (colorMatch.Success)
                {
                    var hex = colorMatch.Groups[1].Value;
                    if (byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bByte)
                        && byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gByte)
                        && byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rByte))
                    {
                        b = bByte; g = gByte; r = rByte;
                    }
                }
                return new ColorTag(target, rawArgs, isShorthand, r, g, b);

            case "alpha":
            case "1a":
            case "2a":
            case "3a":
            case "4a":
                var alphaTarget = name == "alpha" ? 0 : (name[0] - '0');
                byte? alphaVal = null;
                var alphaMatch = Regex.Match(rawArgs, @"&H([0-9a-fA-F]{2})&", RegexOptions.IgnoreCase);
                if (alphaMatch.Success && byte.TryParse(alphaMatch.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var aByte))
                {
                    alphaVal = aByte;
                }
                return new AlphaTag(alphaTarget, rawArgs, alphaVal);

            case "pos":
                var trimmedPos = token.TrimmedArgs;
                var posParts = trimmedPos.Split(',');
                if (posParts.Length == 2
                    && double.TryParse(posParts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var posX)
                    && double.TryParse(posParts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var posY))
                {
                    return new PosTag(posX, posY);
                }
                return new UnknownTag(name, rawArgs);

            case "move":
                var trimmedMove = token.TrimmedArgs;
                var moveParts = trimmedMove.Split(',');
                if (moveParts.Length >= 4
                    && double.TryParse(moveParts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var mx1)
                    && double.TryParse(moveParts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var my1)
                    && double.TryParse(moveParts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var mx2)
                    && double.TryParse(moveParts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var my2))
                {
                    int? mt1 = null, mt2 = null;
                    if (moveParts.Length >= 6
                        && int.TryParse(moveParts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t1)
                        && int.TryParse(moveParts[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t2))
                    {
                        mt1 = t1; mt2 = t2;
                    }
                    return new MoveTag(mx1, my1, mx2, my2, mt1, mt2);
                }
                return new UnknownTag(name, rawArgs);

            case "org":
                var trimmedOrg = token.TrimmedArgs;
                var orgParts = trimmedOrg.Split(',');
                if (orgParts.Length == 2
                    && double.TryParse(orgParts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ox)
                    && double.TryParse(orgParts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var oy))
                {
                    return new OrgTag(ox, oy);
                }
                return new UnknownTag(name, rawArgs);

            case "fad":
                var trimmedFad = token.TrimmedArgs;
                var fadParts = trimmedFad.Split(',');
                if (fadParts.Length == 2
                    && int.TryParse(fadParts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fadeIn)
                    && int.TryParse(fadParts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fadeOut))
                {
                    return new FadeTag(true, FadeIn: fadeIn, FadeOut: fadeOut);
                }
                return new FadeTag(true, RawArgs: rawArgs);

            case "fade":
                var trimmedFade = token.TrimmedArgs;
                var fadeParts = trimmedFade.Split(',');
                if (fadeParts.Length == 7
                    && int.TryParse(fadeParts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fa1)
                    && int.TryParse(fadeParts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fa2)
                    && int.TryParse(fadeParts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fa3)
                    && int.TryParse(fadeParts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ft1)
                    && int.TryParse(fadeParts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ft2)
                    && int.TryParse(fadeParts[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ft3)
                    && int.TryParse(fadeParts[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ft4))
                {
                    return new FadeTag(false, A1: fa1, A2: fa2, A3: fa3, T1: ft1, T2: ft2, T3: ft3, T4: ft4);
                }
                return new FadeTag(false, RawArgs: rawArgs);

            case "k":
            case "K":
            case "kf":
            case "ko":
            case "kt":
                if (int.TryParse(rawArgs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kCs))
                {
                    return new KaraokeTag(name, kCs);
                }
                return new UnknownTag(name, rawArgs);

            case "p":
                if (int.TryParse(rawArgs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pScale))
                {
                    return new DrawingTag(pScale);
                }
                return new UnknownTag(name, rawArgs);

            case "pbo":
                if (int.TryParse(rawArgs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pboVal))
                {
                    return new DrawingTag(0, BaselineOffset: pboVal);
                }
                return new UnknownTag(name, rawArgs);

            case "an":
                if (int.TryParse(rawArgs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var anVal))
                {
                    return new AlignmentTag(anVal, IsLegacy: false);
                }
                return new UnknownTag(name, rawArgs);

            case "a":
                if (int.TryParse(rawArgs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aVal))
                {
                    return new AlignmentTag(aVal, IsLegacy: true);
                }
                return new UnknownTag(name, rawArgs);

            case "fs":
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var fsVal))
                {
                    return new FontSizeTag(fsVal, rawArgs);
                }
                return new FontSizeTag(0, rawArgs);

            case "fn":
                return new FontNameTag(rawArgs);

            case "fscx":
            case "fscy":
                var axis = name[3];
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var fscVal))
                {
                    return new FontScaleTag(axis, fscVal);
                }
                return new UnknownTag(name, rawArgs);

            case "fsp":
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var fspVal))
                {
                    return new FontSpacingTag(fspVal);
                }
                return new UnknownTag(name, rawArgs);

            case "fr":
            case "frx":
            case "fry":
            case "frz":
                char? rAxis = name.Length == 3 ? name[2] : null;
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var frVal))
                {
                    return new FontRotationTag(rAxis, frVal);
                }
                return new UnknownTag(name, rawArgs);

            case "blur":
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var blurVal))
                {
                    return new BlurTag(blurVal, Edges: false);
                }
                return new UnknownTag(name, rawArgs);

            case "be":
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var beVal))
                {
                    return new BlurTag(beVal, Edges: true);
                }
                return new UnknownTag(name, rawArgs);

            case "bord":
            case "xbord":
            case "ybord":
                char? bAxis = name.Length == 5 ? name[0] : null;
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var bordVal))
                {
                    return new BorderTag(bordVal, bAxis);
                }
                return new UnknownTag(name, rawArgs);

            case "shad":
            case "xshad":
            case "yshad":
                char? sAxis = name.Length == 5 ? name[0] : null;
                if (double.TryParse(rawArgs, NumberStyles.Float, CultureInfo.InvariantCulture, out var shadVal))
                {
                    return new ShadowTag(shadVal, sAxis);
                }
                return new UnknownTag(name, rawArgs);

            case "clip":
            case "iclip":
                var isInverse = name == "iclip";
                return new ClipTag(isInverse, rawArgs);

            case "t":
                return ParseTransformTag(rawArgs);

            case "r":
                return new ResetTag(string.IsNullOrEmpty(rawArgs) ? null : rawArgs);

            default:
                return new UnknownTag(name, rawArgs);
        }
    }

    /// <summary>
    /// Parses an entire ASS override block ({\...}) into strongly-typed <see cref="AssTag"/> elements.
    /// </summary>
    public static IReadOnlyList<AssTag> ParseBlock(string block)
    {
        var tokens = AssTagTokenizer.TokenizeBlock(block);
        return tokens.Select(ParseTag).ToList();
    }

    /// <summary>
    /// Formats a sequence of <see cref="AssTag"/> records into a complete override block enclosed in '{' and '}'.
    /// </summary>
    public static string FormatBlock(IEnumerable<AssTag> tags)
    {
        if (tags == null) throw new ArgumentNullException(nameof(tags));
        var sb = new StringBuilder("{");
        foreach (var t in tags)
        {
            sb.Append(t.ToString());
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static TransformTag ParseTransformTag(string rawArgs)
    {
        var inner = rawArgs.StartsWith("(") && rawArgs.EndsWith(")") && rawArgs.Length >= 2
            ? rawArgs.Substring(1, rawArgs.Length - 2)
            : rawArgs;

        // Check if there are timing parameters before nested tags
        var firstSlash = inner.IndexOf('\\');
        if (firstSlash < 0)
        {
            return new TransformTag(rawArgs);
        }

        var header = inner.Substring(0, firstSlash).TrimEnd(',', ' ');
        var tagsPart = inner.Substring(firstSlash);
        var nestedTags = ParseBlock("{" + tagsPart + "}");

        int? t1 = null, t2 = null;
        double? accel = null;

        if (!string.IsNullOrEmpty(header))
        {
            var parts = header.Split(',');
            if (parts.Length == 1 && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
            {
                accel = a;
            }
            else if (parts.Length == 2
                && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pt1)
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pt2))
            {
                t1 = pt1; t2 = pt2;
            }
            else if (parts.Length >= 3
                && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pt1x)
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pt2x)
                && double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ax))
            {
                t1 = pt1x; t2 = pt2x; accel = ax;
            }
        }

        return new TransformTag(rawArgs, t1, t2, accel, nestedTags);
    }
}

/// <summary>Abstract base element for parsed ASS dialogue lines.</summary>
public abstract record AssDialogueElement;

/// <summary>Represents an ASS override block ({\...}) containing strongly-typed tags.</summary>
public sealed record AssTagBlockElement(IReadOnlyList<AssTag> Tags) : AssDialogueElement
{
    public override string ToString() => AssTagParser.FormatBlock(Tags);
}

/// <summary>Represents a plain-text payload element in an ASS dialogue line.</summary>
public sealed record AssTextElement(string Text) : AssDialogueElement
{
    public override string ToString() => Text;
}

/// <summary>
/// Parsed Abstract Syntax Tree (AST) for an entire ASS dialogue line,
/// alternating between override blocks and plain-text segments.
/// </summary>
public sealed class AssDialogueAst
{
    public IReadOnlyList<AssDialogueElement> Elements { get; }

    public AssDialogueAst(IEnumerable<AssDialogueElement> elements)
    {
        if (elements == null) throw new ArgumentNullException(nameof(elements));
        Elements = new ReadOnlyCollection<AssDialogueElement>(elements.ToList());
    }

    /// <summary>
    /// Parses an entire ASS dialogue text into an AST of strongly-typed tag blocks and text segments.
    /// </summary>
    public static AssDialogueAst Parse(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return new AssDialogueAst(Array.Empty<AssDialogueElement>());
        }

        var elements = new List<AssDialogueElement>();
        var i = 0;

        while (i < rawText.Length)
        {
            var openBrace = rawText.IndexOf('{', i);
            if (openBrace < 0)
            {
                elements.Add(new AssTextElement(rawText.Substring(i)));
                break;
            }

            if (openBrace > i)
            {
                elements.Add(new AssTextElement(rawText.Substring(i, openBrace - i)));
            }

            var closeBrace = rawText.IndexOf('}', openBrace);
            if (closeBrace < 0)
            {
                // Unclosed brace
                elements.Add(new AssTextElement(rawText.Substring(openBrace)));
                break;
            }

            var blockContent = rawText.Substring(openBrace, closeBrace - openBrace + 1);
            var tags = AssTagParser.ParseBlock(blockContent);
            elements.Add(new AssTagBlockElement(tags));

            i = closeBrace + 1;
        }

        return new AssDialogueAst(elements);
    }

    /// <summary>Renders the AST back to verbatim ASS dialogue text.</summary>
    public string Render() => string.Concat(Elements.Select(e => e.ToString()));

    public override string ToString() => Render();
}
