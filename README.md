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

## Documentation

Comprehensive production documentation is available in the [`docs/`](docs/) directory:
- [**Features & Developer Guide**](docs/FEATURES.md): Detailed API walkthrough with runnable code examples covering loading, parsing, encoding detection, format bags, time-shifting, coordinate conversions, and ASS AST manipulation.
- [**Architecture, Design Decisions & Technical Limitations**](docs/ARCHITECTURE.md): System architecture diagrams, Architecture Decision Records (ADRs), performance characteristics, comparative benchmarks, and non-goals.
- [**Releasing & CI/CD Guide**](docs/RELEASING.md): Version bumping (`./bump.sh`), dual-tagging conventions, and GitHub Actions release pipelines.

---

## Roadmap & Status Checklist

### Completed Capabilities (v1.0.x)

- [x] **Core Format Parsers & Serializers**
  - [x] SubRip (`.srt`) with 1-based sequential re-indexing and flexible timestamp parsing.
  - [x] WebVTT (`.vtt`) with `WEBVTT` headers, cue settings parser, and non-decreasing timestamp ordering.
  - [x] Advanced SubStation Alpha (`.ass`) with `[Script Info]`, `[V4+ Styles]`, and unknown section retention.
  - [x] SubStation Alpha v4.00 (`.ssa`) with `[V4 Styles]`, `Marked=` fields, and 1–11 alignments.
- [x] **Resilient I/O & Encodings**
  - [x] Multi-tier BOM sniffer (UTF-32 LE/BE, UTF-16 LE/BE, UTF-8).
  - [x] Strict UTF-8 verification with non-destructive fallback to Latin-1/ISO-8859-1 (no corrupted bytes).
  - [x] Synchronous and asynchronous stream processing (`Load`, `LoadAsync`, `Save`, `SaveAsync`).
- [x] **Immutable Core Architecture**
  - [x] `SubtitleDocument` and `SubtitleCue` with defensive copying on construction.
  - [x] Single source of truth in `SubtitleCue.RawText` with internal line ending normalization to `\n`.
  - [x] Lazy, format-aware `PlainText` derivation.
  - [x] Strongly-typed extension bags (`AssCueData`, `VttCueData`, `AssDocumentData`, `VttDocumentData`).
- [x] **Time-Shifting Engine**
  - [x] Positive and negative offset shifting with negative clamping to zero.
  - [x] Automatic negative cue dropping (`DropNegativeCues`).
  - [x] Selective predicate filtering (`Filter`).
  - [x] Automatic WebVTT cue re-ordering and `X-TIMESTAMP-MAP` MPEG-TS timing recalculation.
- [x] **Format Conversion & Coordinate Space Mapping**
  - [x] Complete conversion matrix across SRT, WebVTT, ASS, and SSA.
  - [x] Bidirectional coordinate mapping between WebVTT percentages and ASS PlayRes (libass `384x288` default).
  - [x] WebVTT snap-to-lines heuristics and `<v Speaker>` <-> Actor mapping.
  - [x] `ConversionReport` with machine-readable notice codes and strict `ThrowOnLoss` mode.
- [x] **Strongly-Typed ASS AST & Tokenizer**
  - [x] Zero-regex, lossless override block tokenizer.
  - [x] 20+ sealed tag records (`PosTag`, `MoveTag`, `ColorTag`, `BoldTag`, `DrawingTag`, etc.).
  - [x] `UnknownTag` fallback guaranteeing 100% round-trip preservation of custom tags.
- [x] **Production Readiness & CI/CD**
  - [x] Zero runtime dependencies across `netstandard2.0` and `net8.0`.
  - [x] Public API freeze enforced via Roslyn `PublicApiAnalyzers`.
  - [x] Property-based round-trip testing and golden file corpus suite (69 tests).
  - [x] Automated CI, NuGet.org, and GitHub Packages release workflows.

### Planned Features & Future Improvements (`v1.x`)

- [ ] **Dual-Track Bilingual Stacking (`Subtitle.Stack`)**
  - Merge secondary language tracks into a primary subtitle file with automatic top/bottom screen alignment.
  - Split and synchronize overlapping cue timestamps between differing translation paces.
- [ ] **Sequential Timeline Concatenation (`Subtitle.Concat`)**
  - Stitch multiple part files (e.g. CD1 / CD2) sequentially with automatic cumulative offset shifting.
- [ ] **Multi-Track Collision Reconciliation (`Subtitle.Reconcile`)**
  - Detect and resolve visual overlaps and collisions between concurrent subtitle tracks.
- [ ] **Sanitizer & Restyler Engine (`Subtitle.Sanitize` / `Subtitle.Restyle`)**
  - Strip unneeded formatting, normalize font families, adjust minimum/maximum cue display durations.
  - Profanity filtering and text cleanup routines.

### Known Issues & Edge Cases

1. **Unclosed ASS Override Blocks**: In lenient parsing mode, unclosed `{` without a closing `}` is treated as plain text rather than throwing an exception.
2. **Dense Vector Drawings**: Converting ASS files with extensive vector art (`\p1` drawings) into WebVTT produces `DrawingDropped` warnings and strips vector commands.
3. **Unity IL2CPP Code Stripping**: When using Unity with Aggressive Code Stripping, ensure generic dictionary constructors used in format bags are preserved via `link.xml`.

---

## License

Licensed under the [MIT License](LICENSE).
