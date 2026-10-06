# SubtitleToolkit

[![NuGet](https://img.shields.io/nuget/v/SubtitleToolkit.svg)](https://www.nuget.org/packages/SubtitleToolkit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

A modern, high-performance, **zero-dependency** .NET library for parsing, writing, converting, and time-shifting **SRT**, **WebVTT**, **ASS**, and **SSA** subtitle files with 100% round-trip fidelity, strongly-typed AST, and machine-readable conversion diagnostics.

Targeting `netstandard2.0` and `net8.0` with **0 runtime NuGet package dependencies**.

---

## Key Features

- **Multi-Format Support**:
  - **SubRip (.srt)**: Resilient timestamp parsing (`hh:mm:ss,fff` or `.`), sequential 1-based renumbering, legacy coordinate retention.
  - **WebVTT (.vtt)**: `WEBVTT` signature headers, MPEG-TS `X-TIMESTAMP-MAP` adjustment, anchored non-cue blocks (`NOTE`, `STYLE`, `REGION`), and structured cue settings (`line`, `position`, `size`, `align`, `vertical`).
  - **Advanced SubStation Alpha (.ass)**: Full `[Script Info]`, `[V4+ Styles]`, `[Events]`, dynamic column ordering, centisecond rounding (`h:mm:ss.cc`), and verbatim unknown section retention (`[Aegisub Project Garbage]`, `[Fonts]`, etc.).
  - **SubStation Alpha v4.00 (.ssa)**: Strict separation from ASS (uses `[V4 Styles]`, `Marked=` instead of `Layer`, and SSA 1–11 alignments).
- **Strongly-Typed ASS AST & Tokenizer**:
  - Zero-regex, lossless tokenization of `{\...}` override blocks.
  - Sealed record AST hierarchy: `BoldTag`, `ItalicTag`, `ColorTag`, `PosTag`, `MoveTag`, `OrgTag`, `FadeTag`, `KaraokeTag`, `DrawingTag`, `AlignmentTag`, `FontSizeTag`, `TransformTag`, `ClipTag`, and `UnknownTag` fallback.
- **Conversion Engine with Coordinate Mapping**:
  - Full cross-format conversion matrix across SRT, WebVTT, ASS, and SSA.
  - Bidirectional coordinate mapping between WebVTT percentages (`line:XX% position:YY%`) and ASS `PlayRes` (libass `384x288` default).
  - WebVTT snap-to-lines heuristics and `<v Speaker>` <-> Actor mapping.
  - `ConversionReport` with structured audit notices and optional `ThrowOnLoss`.
- **Immutable Time-Shifter**:
  - Shift entire documents or individual cues forward or backward.
  - Negative timestamp clamping or automatic cue dropping.
  - Selective predicate filtering and automatic MPEG-TS `X-TIMESTAMP-MAP` timestamp recalculation.
- **Resilient I/O & Encodings**:
  - Priority BOM sniffer (UTF-32 LE/BE, UTF-16 LE/BE, UTF-8).
  - Strict UTF-8 decoding trial with automatic non-destructive fallback to Latin-1/ISO-8859-1.
- **Zero State Drift**:
  - Canonical `RawText` with internal linebreaks strictly normalized to `\n`.
  - Lazy `PlainText` derivation according to format rules.

---

## Comparison with Alternatives

| Feature / Metric | **SubtitleToolkit** | **libse (Subtitle Edit Core)** | **SubtitlesParser** |
| :--- | :--- | :--- | :--- |
| **Runtime Dependencies** | **0** (pure BCL) | Many / Desktop monolith | 0 |
| **Target Frameworks** | `netstandard2.0`, `net8.0` | `.NET Framework` / Desktop | `netstandard2.0` |
| **AOT / Serverless Ready** | **Yes** (trim-friendly) | No (large binary size) | Yes |
| **Data Model** | **Immutable** | Mutable state | Mutable POCO |
| **Round-Trip Preservation** | **100% Lossless** | Lossy on custom blocks | Drops all styles/headers |
| **ASS Style Tables & Headers** | **Yes** (Preserved) | Yes | **No** (dialogue only) |
| **ASS Unknown Sections** | **Yes** (`[Aegisub Garbage]`, etc.) | Partial | **No** (dropped) |
| **Loss Audit Diagnostics** | **Yes** (`ConversionReport`) | No | No |
| **ASS Override Tag AST** | **Yes** (20+ typed records) | Regex-based | No |
| **WebVTT NOTE/STYLE Anchoring**| **Yes** (in-place retention) | Flattened to header | Dropped |

---

## Quick Start

### 1. Parsing & Loading

```csharp
using SubtitleToolkit;
using SubtitleToolkit.Model;

// Load from file (with automatic BOM detection & strict UTF-8 fallback)
var result = Subtitle.Load("subtitles.vtt");
SubtitleDocument doc = result.Document;

// Access cues
foreach (var cue in doc.Cues)
{
    Console.WriteLine($"[{cue.Start} -> {cue.End}] {cue.PlainText}");
}

// Check diagnostics (if in Lenient mode)
if (result.HasWarnings)
{
    foreach (var diag in result.Diagnostics)
    {
        Console.WriteLine($"Line {diag.LineNumber}: {diag.Message}");
    }
}
```

### 2. Time-Shifting

```csharp
using SubtitleToolkit.Common;

// Immutable shift: all cues shifted forward by 1.5 seconds
var shifted = doc.TimeShift(TimeSpan.FromSeconds(1.5));

// Negative shift with negative cue dropping
var backShifted = doc.TimeShift(
    TimeSpan.FromSeconds(-5.0),
    new TimeShiftOptions
    {
        ClampNegativeToZero = true,
        DropNegativeCues = true
    });
```

### 3. Format Conversion & Audit Reporting

```csharp
using SubtitleToolkit.Diagnostics;

// Convert ASS to WebVTT with coordinate mapping
var conversion = Subtitle.Convert(assDoc, SubtitleFormat.WebVtt, new ConversionOptions
{
    TargetPlayResX = 1920,
    TargetPlayResY = 1080
});

SubtitleDocument vttDoc = conversion.Document;

// Inspect conversion report for dropped or adapted features
foreach (var notice in conversion.Report.Notices)
{
    Console.WriteLine($"[{notice.Severity}] {notice.Code} (Cue #{notice.CueIndex}): {notice.Message}");
}
```

### 4. Strongly-Typed ASS AST

```csharp
using SubtitleToolkit.Formats.Ass;

var rawDialogue = @"{\pos(960,540)\b1\c&H00FFFF&}Important Announcement";

// Parse into structured AST
var ast = AssDialogueAst.Parse(rawDialogue);

foreach (var element in ast.Elements)
{
    if (element is AssTagBlockElement block)
    {
        foreach (var tag in block.Tags)
        {
            if (tag is PosTag pos) Console.WriteLine($"Position: ({pos.X}, {pos.Y})");
            if (tag is BoldTag bold) Console.WriteLine($"Bold: {bold.Enabled}");
            if (tag is ColorTag color) Console.WriteLine($"Color target {color.Target}: {color.RawColor}");
        }
    }
    else if (element is AssTextElement text)
    {
        Console.WriteLine($"Text: {text.Text}");
    }
}

// Render back to ASS dialogue string
string output = ast.Render();
```

### 5. Saving Documents

```csharp
// Save directly to file path or Stream
doc.Save("output.srt");

// Subtitle.Save prevents accidental cross-format saving;
// targetFormat must match doc.Format, or use Subtitle.Convert() first!
```

---

## License

Licensed under the [MIT License](LICENSE).
