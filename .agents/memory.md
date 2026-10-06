# SubtitleToolkit Agent Memory & Decision Log

## Architecture Decisions & Constraints (ADR Summary)

### ADR 01: Zero-Dependency Policy
- **Decision**: Package targets `netstandard2.0;net8.0` with 0 runtime NuGet dependencies.
- **Context**: Consumer apps include Unity, serverless Lambdas, Linux microservices, and desktop apps. External dependencies cause version conflicts and break Unity IL2CPP.
- **Consequence**: `Microsoft.Bcl.HashCode`, `System.Memory`, and `System.Collections.Immutable` are barred from `netstandard2.0`. Polyfills (`PolySharp`, analyzers) use `PrivateAssets="all"`.

### ADR 02: Canonical Source of Truth in `SubtitleCue.RawText`
- **Decision**: `RawText` is the sole source of truth for cue text.
- **Context**: Storing `PlainText`, `RawText`, and AST `Parts` concurrently leads to desynchronization bugs.
- **Consequence**: `PlainText` is lazily derived from `RawText`. VTT Voice `<v Name>` stays inside `RawText` and is NOT copied into `VttCueData`.

### ADR 03: Internal Line-Ending Normalization
- **Decision**: Normalize all incoming newlines in `RawText` to `\n` on construction.
- **Context**: Real-world files mix `\r\n` and `\n`. Comparing documents parsed on different OS environments causes false test failures.
- **Consequence**: `RawText` always uses `\n`. Output line ending is controlled via `SubtitleWriteOptions.LineEnding`.

### ADR 04: SSA v4.00 vs. ASS v4.00+ Separation
- **Decision**: Treat SSA and ASS as distinct formats (`SubtitleFormat.Ssa` vs `SubtitleFormat.Ass`).
- **Context**: SSA uses `[V4 Styles]`, `Marked=`, and 1–11 alignment numbering. ASS uses `[V4+ Styles]`, `Layer`, and 1–9 numpad alignment. Conflating them corrupts alignment and event layers.

### ADR 05: Lossless VTT Anchored Non-Cue Blocks
- **Decision**: Anchor `NOTE`, `STYLE`, and `REGION` blocks in `VttDocumentData` using `BeforeCueIndex`.
- **Context**: `NOTE` blocks can appear anywhere in WebVTT files, including between cues.
- **Consequence**: Round-tripping retains exact block placement without moving inter-cue comments to the file header.

### ADR 06: Conversion Safety & Coordinate Mapping
- **Decision**: `Subtitle.Save` forbids cross-format writing. Conversions require `Subtitle.Convert` and return a `ConversionReport`.
- **Context**: ASS -> SRT/VTT is lossy (styles, drawings, positioning). Silently dropping data corrupts presentation.
- **Consequence**: All dropped features are cataloged with machine-readable codes. Default PlayRes when omitted is strictly `384x288` per libass specification.

### Verified Positioning Against Existing Libraries
- **`libse` (Subtitle Edit Core)**: Feature-rich desktop monolith with large binary footprint and historical desktop dependencies. SubtitleToolkit targets lightweight serverless/AOT pipelines.
- **`SubtitlesParser` / `SubtitlesParserV2`**: Basic parsing of SRT and dialogue-only ASS; lacks style tables, script info, and lossless round-trip bags.
- **`SubtitleToolkit`**: Zero-dependency, modern C# 12, immutable, lossless format bags, format conversion audit reports, and cross-platform compatibility.
