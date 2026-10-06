using System;
using System.Text;
using SubtitleToolkit.Model;

namespace SubtitleToolkit.Common;

/// <summary>
/// Helper utilities for extracting plain text and sanitizing subtitle content.
/// </summary>
public static class SubtitleTextHelper
{
    /// <summary>
    /// Extracts plain text from raw subtitle text based on format rules.
    /// </summary>
    public static string ExtractPlainText(string rawText, SubtitleFormat? format)
    {
        if (string.IsNullOrEmpty(rawText))
            return string.Empty;

        return format switch
        {
            SubtitleFormat.Ass or SubtitleFormat.Ssa => ExtractAssPlainText(rawText),
            SubtitleFormat.WebVtt => ExtractVttPlainText(rawText),
            SubtitleFormat.SubRip => ExtractSrtPlainText(rawText),
            _ => ExtractGenericPlainText(rawText)
        };
    }

    private static string ExtractAssPlainText(string text)
    {
        var sb = new StringBuilder(text.Length);
        var inOverride = false;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];
            if (c == '{')
            {
                inOverride = true;
                i++;
                continue;
            }
            if (c == '}')
            {
                inOverride = false;
                i++;
                continue;
            }

            if (inOverride)
            {
                i++;
                continue;
            }

            if (c == '\\' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (next is 'N' or 'n')
                {
                    sb.Append('\n');
                    i += 2;
                    continue;
                }
                if (next is 'h')
                {
                    sb.Append(' ');
                    i += 2;
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static string ExtractVttPlainText(string text)
    {
        var stripped = StripXmlTags(text);
        return DecodeHtmlEntities(stripped);
    }

    private static string ExtractSrtPlainText(string text)
    {
        // Strip <b>, <i>, <u>, <font ...> and {\an8}
        var noAssTags = StripAssOverrideBlocks(text);
        var stripped = StripXmlTags(noAssTags);
        return DecodeHtmlEntities(stripped);
    }

    private static string ExtractGenericPlainText(string text)
    {
        var noAss = StripAssOverrideBlocks(text);
        var noXml = StripXmlTags(noAss);
        return DecodeHtmlEntities(noXml);
    }

    private static string StripAssOverrideBlocks(string text)
    {
        if (text.IndexOf('{') < 0) return text;
        var sb = new StringBuilder(text.Length);
        var inBlock = false;
        foreach (var c in text)
        {
            if (c == '{') { inBlock = true; continue; }
            if (c == '}') { inBlock = false; continue; }
            if (!inBlock) sb.Append(c);
        }
        return sb.ToString();
    }

    private static string StripXmlTags(string text)
    {
        if (text.IndexOf('<') < 0) return text;
        var sb = new StringBuilder(text.Length);
        var inTag = false;
        foreach (var c in text)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }

    private static string DecodeHtmlEntities(string text)
    {
        if (text.IndexOf('&') < 0) return text;
        return text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&apos;", "'")
            .Replace("&nbsp;", " ");
    }
}
