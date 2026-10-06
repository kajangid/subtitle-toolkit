# SubtitleToolkit Phased Roadmap & Plan

## Roadmap Overview

| Version | Milestone Scope | Key Deliverables |
| :--- | :--- | :--- |
| **v0.1** | Core Parsers, Encodings & Format Bags | SRT, WebVTT, ASS, SSA parse & write; BOM detection & strict UTF-8 fallback; `ParseDiagnostics` (Lenient/Strict); round-trip contracts; format bags. |
| **v0.2** | Time-Shifting & Tag Tokenizer | Immutable `TimeShift` (negative clamp/drop, re-sorting, `X-TIMESTAMP-MAP`); lossless `AssTagTokenizer` (`{\...}` override split & round-trip). |
| **v0.3** | Lossless-Aware Format Conversion | `Subtitle.Convert` engine; `ConversionReport` with machine-readable codes; coordinate mapping between VTT percentages and ASS PlayRes (libass 384x288 default). |
| **v0.4** | Strongly-Typed ASS AST | Sealed record tag hierarchy (`BoldTag`, `PosTag`, `KaraokeTag`, `DrawingTag`) with `UnknownTag` fallback. |
| **v1.0** | Production Release | Multi-targeting validation (`netstandard2.0;net8.0`), `PublicApiAnalyzers`, SourceLink, FsCheck round-trip property tests, golden file test corpus. |
| **v1.x** | Advanced Operations | Dual-track bilingual stacking with cue splitting; timeline overlay & reconciliation; sequential concatenation; restyler/sanitizer. |

---

## Detailed Milestone Breakdown

### Milestone v0.1: Core Parsers & Resilient Encodings
- **Package Setup**:
  - `src/SubtitleToolkit/SubtitleToolkit.csproj` multi-targeting `netstandard2.0;net8.0`.
  - Zero runtime `<PackageReference>` entries.
  - `PolySharp` and `PublicApiAnalyzers` with `PrivateAssets="all"`.
- **I/O & Encodings**:
  - `SubtitleEncoding`: BOM sniffer (UTF-32, UTF-16, UTF-8), strict UTF-8 trial, fallback to ISO-8859-1.
  - Synchronous and asynchronous streams: `Load`, `LoadAsync`, `Save`, `SaveAsync`.
- **Core Model**:
  - `SubtitleDocument`: format, defensive `ReadOnlyCollection<SubtitleCue>`, `DocumentFormatData`.
  - `SubtitleCue`: `Start`, `End`, `RawText` (normalized to `\n`), lazy `PlainText`, `CueFormatData`.
  - `CueFormatData` & `DocumentFormatData` hierarchies (`AssCueData`, `VttCueData`, `AssDocumentData`, `VttDocumentData`).
- **Format Handlers**:
  - `SrtHandler`: Lenient indices and timestamp parsing (`00:00:00,000` / `.`).
  - `VttHandler`: `WEBVTT` signature, anchored `VttAnchoredBlock` list, cue settings parser + raw string.
  - `AssHandler` & `SsaHandler`: Strict separation of SSA v4 vs ASS v4+, dynamic `Format:` column mapping, centisecond rounding, verbatim unknown section storage.
- **Diagnostics**:
  - `ParseResult` with `IReadOnlyList<ParseDiagnostic>` and implicit conversion to `SubtitleDocument`.
  - Lenient vs Strict modes.

### Milestone v0.2: Time-Shifting & Lossless ASS Tag Tokenizer
- **TimeShifter**:
  - Offset addition/subtraction.
  - Options: `ClampNegativeToZero` (default true), `DropNegativeCues`, selective `Filter`.
  - WebVTT cue re-ordering guarantee.
  - `X-TIMESTAMP-MAP` adjustment in `VttDocumentData`.
- **AssTagTokenizer**:
  - Low-level lossless tokenizer splitting `{\...}` blocks into `(string Name, string RawArgs)` pairs.
  - Verbatim round-trip capability without regex data loss.

### Milestone v0.3: Format Conversion Engine & Coordinate Mapping
- **Conversion Engine**:
  - `Subtitle.Convert(doc, targetFormat, options) -> ConversionResult`.
  - `ConversionReport`: structured list of `ConversionNotice` (Severity, Code, CueIndex, FeatureName, Message).
  - Configurable `ThrowOnLoss`.
- **Coordinate Mapping**:
  - VTT percentages (`line:XX% position:YY%`) <-> ASS coordinates using `PlayResX` / `PlayResY` (defaulting strictly to libass standard 384x288).
  - Snap-to-lines conversion heuristics.
  - ASS `\pos` / `\an` conversion to VTT cue settings.
  - Stale format bag stripping.

### Milestone v0.4: Sealed Record ASS Tag AST
- Typed AST: `AssTag` base record with sealed subtypes (`BoldTag`, `ItalicTag`, `ColorTag`, `PosTag`, `MoveTag`, `FadeTag`, `KaraokeTag`, `DrawingTag`).
- `UnknownTag(string Raw)` fallback ensuring 100% round-trip preservation of custom or Aegisub extensions.

### Milestone v1.0: Stabilization & Release
- API freeze with `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
- Package validation with `EnablePackageValidation`.
- Synthetic and permissively licensed test corpus.
- FsCheck property tests: `Parse(Write(doc)) == doc`.
- Documentation & benchmarks comparing memory and correctness against `libse` and `SubtitlesParser`.
