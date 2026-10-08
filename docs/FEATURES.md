# SubtitleToolkit Features & Developer Guide

`SubtitleToolkit` is a lightweight, zero-dependency .NET library targeting `netstandard2.0;net8.0` for parsing, generating, converting, time-shifting, and AST-manipulating subtitle files across **SubRip (.srt)**, **WebVTT (.vtt)**, **Advanced SubStation Alpha (.ass)**, and **SubStation Alpha v4.00 (.ssa)**.

---

## Table of Contents

1. [Installation & Target Frameworks](#1-installation--target-frameworks)
2. [Parsing & Loading Subtitles](#2-parsing--loading-subtitles)
3. [Resilient I/O & Encoding Detection](#3-resilient-io--encoding-detection)
4. [Immutable Document & Cue Model](#4-immutable-document--cue-model)
5. [Format-Specific Extension Bags](#5-format-specific-extension-bags)
6. [Time-Shifting Engine](#6-time-shifting-engine)
7. [Format Conversion & Coordinate Mapping](#7-format-conversion--coordinate-mapping)
8. [Strongly-Typed ASS Override AST](#8-strongly-typed-ass-override-ast)
9. [Serialization & Saving](#9-serialization--saving)
10. [Error Handling & Diagnostics](#10-error-handling--diagnostics)

---

## 1. Installation & Target Frameworks

Install via .NET CLI:

```bash
dotnet add package SubtitleToolkit
```

Or via Package Manager Console:

```powershell
Install-Package SubtitleToolkit
```

### Supported Runtimes
- **`net8.0` / `net9.0`**: Modern high-performance .NET LTS.
- **`netstandard2.0`**: Full compatibility with Unity (Mono and IL2CPP), .NET Framework 4.6.2+, and legacy Linux/Windows microservices.
- **Zero Runtime Dependencies**: The package contains strictly zero runtime NuGet dependencies on both target frameworks.

---

## 2. Parsing & Loading Subtitles

The [`Subtitle`](../src/SubtitleToolkit/Subtitle.cs) static class provides unified entry points for parsing and loading subtitle documents.

### Parsing from Strings

```csharp
using SubtitleToolkit;
using SubtitleToolkit.Model;

string srtText = @"1
00:00:01,000 --> 00:00:04,000
Welcome to the presentation!
";

// Format can be inferred automatically or specified explicitly
ParseResult result = Subtitle.Parse(srtText, SubtitleFormat.SubRip);
SubtitleDocument doc = result.Document;

Console.WriteLine($"Parsed {doc.Cues.Count} cues.");
```

### Loading from Files

```csharp
// Format is inferred from the file extension (.srt, .vtt, .ass, .ssa)
ParseResult result = Subtitle.Load("subtitles.vtt");
SubtitleDocument doc = result.Document;
```

### Loading from Streams (Synchronous & Asynchronous)

```csharp
using var stream = File.OpenRead("subtitles.ass");

// Synchronous
ParseResult syncResult = Subtitle.Load(stream, SubtitleFormat.Ass);

// Asynchronous
ParseResult asyncResult = await Subtitle.LoadAsync(stream, SubtitleFormat.Ass, cancellationToken: ct);
```

---

## 3. Resilient I/O & Encoding Detection

Real-world subtitle files frequently come in unknown or corrupted encodings. `SubtitleToolkit` provides an automatic three-stage decoding pipeline in [`SubtitleEncoding`](../src/SubtitleToolkit/IO/SubtitleEncoding.cs):

1. **BOM Sniffing**: Inspects the stream preamble for byte order marks in order of specificity:
   - `UTF-32 LE` (`FF FE 00 00`) & `UTF-32 BE` (`00 00 FE FF`)
   - `UTF-16 LE` (`FF FE`) & `UTF-16 BE` (`FE FF`)
   - `UTF-8 BOM` (`EF BB BF`)
2. **Strict UTF-8 Trial**: If no BOM is present, attempts strict UTF-8 decoding (`throwOnInvalidBytes: true`).
3. **Non-Destructive Fallback**: If invalid bytes occur, rewinds the stream and decodes using the configured fallback encoding (default: `ISO-8859-1` / Latin-1). Invalid bytes are preserved rather than replaced with `U+FFFD` (``).

```csharp
var options = new SubtitleReadOptions
{
    // Configure custom fallback encoding for regional files
    FallbackEncoding = System.Text.Encoding.GetEncoding("windows-1252"),
    Mode = ParseMode.Lenient
};

ParseResult result = Subtitle.Load("legacy_french.srt", options);
```

---

## 4. Immutable Document & Cue Model

Every model instance in `SubtitleToolkit` is strictly immutable. Modifying cues or documents creates new instances, preventing multi-threaded data races.

### `SubtitleDocument`
- `Format`: `SubtitleFormat` enum (`SubRip`, `WebVtt`, `Ass`, `Ssa`).
- `Cues`: Defensive `IReadOnlyList<SubtitleCue>`.
- `FormatData`: Optional strongly-typed format metadata bag.

### `SubtitleCue`
- `Start`: `TimeSpan` timestamp.
- `End`: `TimeSpan` timestamp.
- `Duration`: Computed property (`End - Start`, clamped to `Zero` if inverted).
- `RawText`: Canonical multiline string payload with all newlines internally normalized to `\n`.
- `PlainText`: Lazily computed on first access, stripping format-specific tags according to format rules.
- `FormatData`: Format-specific cue metadata.

```csharp
var cue = new SubtitleCue(
    start: TimeSpan.FromSeconds(1),
    end: TimeSpan.FromSeconds(3),
    rawText: "<i>Doctor</i>, we have an <b>emergency</b>!"
);

// RawText retains formatting tags
Console.WriteLine(cue.RawText); // "<i>Doctor</i>, we have an <b>emergency</b>!"

// PlainText is clean plain English
Console.WriteLine(cue.PlainText); // "Doctor, we have an emergency!"

// Immutable modification helpers
SubtitleCue adjustedCue = cue.WithTimes(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));
SubtitleCue editedCue = cue.WithText("Updated text");
```

---

## 5. Format-Specific Extension Bags

To achieve 100% round-trip fidelity, `SubtitleToolkit` stores format-specific data in strongly-typed bags without polluting the shared `SubtitleCue` abstraction.

### WebVTT Extension Bags
- **`VttCueData`**:
  - `Identifier`: Optional cue identifier.
  - `RawSettings`: Verbatim settings string (e.g. `line:90% position:50% align:center`).
  - `Settings`: Structured [`VttCueSettings`](../src/SubtitleToolkit/Model/FormatData/VttCueSettings.cs) exposing `Line`, `Position`, `Size`, `Align`, and `Vertical`.
- **`VttDocumentData`**:
  - `TimestampMap`: Preserves `X-TIMESTAMP-MAP=MPEGTS:...,LOCAL:...` headers for streaming HLS media.
  - `NonCueBlocks`: Preserves `NOTE`, `STYLE`, and `REGION` blocks anchored in-place relative to cues via `BeforeCueIndex`.

```csharp
if (cue.FormatData is VttCueData vttData && vttData.Settings != null)
{
    Console.WriteLine($"Alignment: {vttData.Settings.Align}");
    Console.WriteLine($"Position: {vttData.Settings.Position?.Percentage}%");
}
```

### ASS / SSA Extension Bags
- **`AssCueData`**:
  - `Layer`: Event z-index layer.
  - `StyleName`: Style reference (e.g. `Default`, `TopSign`).
  - `ActorName`: Speaker or character name.
  - `MarginL`, `MarginR`, `MarginV`: Cue override margins.
  - `Effect`: Transition effect string (e.g. `Banner;5;0`).
  - `Marked`: Boolean marked flag for legacy SSA scripts.
- **`AssDocumentData`**:
  - `ScriptInfo`: Key-value dictionary of `[Script Info]` headers (e.g. `PlayResX`, `PlayResY`, `WrapStyle`).
  - `Styles`: Collection of [`AssStyle`](../src/SubtitleToolkit/Model/FormatData/AssDocumentData.cs#L85) records maintaining verbatim field order.
  - `UnknownSections`: Verbatim collection of custom sections (e.g. `[Aegisub Project Garbage]`, `[Fonts]`).

---

## 6. Time-Shifting Engine

The [`TimeShifter`](../src/SubtitleToolkit/Common/TimeShifter.cs) provides immutable offset operations on documents and individual cues.

### Features
- Positive (forward) and negative (backward) time-shifting.
- `ClampNegativeToZero`: Automatically clamps cues that shift before `00:00:00.000` to zero (default: `true`).
- `DropNegativeCues`: Drops cues that conclude at or before zero.
- `Filter`: Selective predicate filtering (e.g. shift only cues belonging to a specific actor or style).
- Automatic chronological re-sorting for WebVTT cues.
- Automatic recalculation of `X-TIMESTAMP-MAP` MPEG-TS offsets.

```csharp
// 1. Shift all cues forward by 2.5 seconds
SubtitleDocument shiftedForward = doc.TimeShift(TimeSpan.FromSeconds(2.5));

// 2. Shift backward by 5 seconds with automatic cue dropping
var options = new TimeShiftOptions
{
    ClampNegativeToZero = true,
    DropNegativeCues = true
};
SubtitleDocument shiftedBackward = doc.TimeShift(TimeSpan.FromSeconds(-5.0), options);

// 3. Selective shifting: shift only Narrator cues
var selectiveOptions = new TimeShiftOptions
{
    Filter = cue => cue.FormatData is AssCueData ass && ass.ActorName == "Narrator"
};
SubtitleDocument narratorShifted = doc.TimeShift(TimeSpan.FromSeconds(1.0), selectiveOptions);
```

---

## 7. Format Conversion & Coordinate Mapping

The [`SubtitleConverter`](../src/SubtitleToolkit/Common/SubtitleConverter.cs) engine supports high-fidelity cross-conversion between any combination of supported formats:

| From | To | Supported Features & Conversions |
| :--- | :--- | :--- |
| **SRT** | **WebVTT** | Converts legacy `{\anX}` tags to WebVTT cue settings (`line`, `align`). Sorts cues chronologically. |
| **WebVTT** | **SRT** | Converts `<v Speaker>` tags to `Speaker:` text prefixes. Converts cue settings to `{\anX}` tags. |
| **ASS/SSA** | **SRT** | Translates `\b1`, `\i1`, `\u1`, `\s1`, and `\c&H...&` tags into HTML `<b>`, `<i>`, `<u>`, `<s>`, `<font color>`. Drops vector drawings and complex positioning with audit logging. |
| **SRT** | **ASS/SSA** | Synthesizes `[Script Info]` and default `[V4+ Styles]`. Converts HTML formatting tags to ASS override tags. Sets PlayRes. |
| **ASS/SSA** | **WebVTT** | Maps `\pos(x,y)` to WebVTT percentages (`position:X% line:Y%`) based on `PlayResX`/`PlayResY`. Strips vector drawings with audit notices. |
| **WebVTT** | **ASS/SSA** | Maps `<v Speaker>` voice tags to the ASS `Actor` field. Maps percentage positioning to `\pos(x,y)` coordinates. Uses snap-to-lines heuristics. |
| **ASS** | **SSA** | Translates 1–9 numpad alignments to SSA 1–11 alignments (`\anX` -> `\aY`). Converts `Layer` to `Marked`. |
| **SSA** | **ASS** | Translates SSA 1–11 alignments to ASS 1–9 numpad alignments (`\aX` -> `\anY`). Converts `Marked` to `Layer`. |

### Conversion Example & Audit Report

```csharp
using SubtitleToolkit.Diagnostics;

var options = new ConversionOptions
{
    TargetPlayResX = 1920,
    TargetPlayResY = 1080,
    DefaultStyleName = "Default",
    ThrowOnLoss = false // Set to true to throw SubtitleConversionException on warnings
};

ConversionResult result = Subtitle.Convert(assDoc, SubtitleFormat.WebVtt, options);
SubtitleDocument vttDoc = result.Document;
ConversionReport report = result.Report;

if (report.HasWarnings)
{
    foreach (var notice in report.Notices)
    {
        Console.WriteLine($"[{notice.Severity}] Code: {notice.Code} (Cue #{notice.CueIndex}): {notice.Message}");
    }
}
```

---

## 8. Strongly-Typed ASS Override AST

`SubtitleToolkit` features a strongly-typed Abstract Syntax Tree (AST) for ASS dialogue strings in [`AssTags.cs`](../src/SubtitleToolkit/Formats/AssTags.cs) and [`AssTagParser.cs`](../src/SubtitleToolkit/Formats/AssTagParser.cs).

### Tag Record Hierarchy
All tags derive from the abstract `AssTag` base record:
- **Geometry & Position**: `PosTag(X, Y)`, `MoveTag(X1, Y1, X2, Y2, T1, T2)`, `OrgTag(X, Y)`
- **Color & Alpha**: `ColorTag(Target, RawColor, UseShorthand, Red, Green, Blue)`, `AlphaTag(Target, RawAlpha, Alpha)`
- **Style Overrides**: `BoldTag(Enabled, Weight)`, `ItalicTag(Enabled)`, `UnderlineTag(Enabled)`, `StrikeoutTag(Enabled)`
- **Typography**: `FontSizeTag(Size)`, `FontNameTag(FontName)`, `FontScaleTag(Axis, Scale)`, `FontSpacingTag(Spacing)`, `FontRotationTag(Axis, Angle)`, `FontEncodingTag(Encoding)`
- **Effects & Animation**: `FadeTag(IsSimple, FadeIn, FadeOut, ...)`, `KaraokeTag(Type, DurationCentiseconds)`, `TransformTag(RawArgs, T1, T2, Accel, NestedTags)`, `ClipTag(...)`, `DrawingTag(Scale, BaselineOffset)`
- **Styling & Alignment**: `AlignmentTag(Alignment, IsLegacy)`, `BlurTag(Strength, Edges)`, `BorderTag(Width, Axis)`, `ShadowTag(Depth, Axis)`, `ResetTag(StyleName)`
- **Extensibility Fallback**: `UnknownTag(TagName, RawArgs)` guarantees 100% round-trip preservation of proprietary or Aegisub extensions.

### AST Usage Example

```csharp
using SubtitleToolkit.Formats.Ass;

string dialogue = @"{\pos(960,540)\b1\c&H00FFFF&}Important Announcement{\r}";

// Parse dialogue into AST
AssDialogueAst ast = AssDialogueAst.Parse(dialogue);

foreach (AssDialogueElement element in ast.Elements)
{
    if (element is AssTagBlockElement block)
    {
        foreach (AssTag tag in block.Tags)
        {
            switch (tag)
            {
                case PosTag pos:
                    Console.WriteLine($"Position: X={pos.X}, Y={pos.Y}");
                    break;
                case BoldTag bold:
                    Console.WriteLine($"Bold enabled: {bold.Enabled}");
                    break;
                case ColorTag color:
                    Console.WriteLine($"Target: {color.Target}, RGB: ({color.Red}, {color.Green}, {color.Blue})");
                    break;
                case UnknownTag unknown:
                    Console.WriteLine($"Custom tag: \\{unknown.TagName}{unknown.RawArgs}");
                    break;
            }
        }
    }
    else if (element is AssTextElement text)
    {
        Console.WriteLine($"Text content: {text.Text}");
    }
}

// Render AST back to verbatim ASS dialogue string
string output = ast.Render();
```

---

## 9. Serialization & Saving

The library enforces a strict **Save Safety Boundary**: calling `Save` with a format different from the document's native format throws an `InvalidOperationException`. Conversions must route explicitly through `Subtitle.Convert`.

### Saving to Files

```csharp
var writeOptions = new SubtitleWriteOptions
{
    LineEnding = "\r\n", // Default is CRLF; can be set to "\n" for Unix pipelines
    EmitBom = false      // Controls UTF-8 BOM emission (WebVTT strictly forbids BOM per spec)
};

// Direct document save
doc.Save("output.srt", writeOptions);

// Or via static entry point
Subtitle.Save(doc, "output.srt", SubtitleFormat.SubRip, writeOptions);
```

### Serializing to In-Memory String

```csharp
string serializedText = Subtitle.WriteToString(doc, SubtitleFormat.WebVtt);
```

---

## 10. Error Handling & Diagnostics

### Parsing Modes
- **`ParseMode.Lenient` (Default)**: Recovers gracefully from malformed timestamps, missing indices, or unescaped characters, logging warnings to `result.Diagnostics`.
- **`ParseMode.Strict`**: Throws [`SubtitleParseException`](../src/SubtitleToolkit/Diagnostics/ParseDiagnostics.cs#L96) immediately upon encountering any formatting violation.

```csharp
try
{
    var options = new SubtitleReadOptions { Mode = ParseMode.Strict };
    var result = Subtitle.Parse(malformedContent, SubtitleFormat.SubRip, options);
}
catch (SubtitleParseException ex)
{
    Console.WriteLine($"Parse failed at line {ex.LineNumber}: {ex.Message}");
}
```
