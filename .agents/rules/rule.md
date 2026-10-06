# SubtitleToolkit Coding Rules

## 1. Zero-Dependency Mandate
- `<PackageReference>` in `SubtitleToolkit.csproj` must NEVER contain a runtime dependency.
- Allowed package references are build-time analyzers only:
  - `PolySharp` with `PrivateAssets="all"`
  - `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PrivateAssets="all"`
- `Microsoft.Bcl.HashCode`, `System.Memory`, and `System.Collections.Immutable` are strictly forbidden on `netstandard2.0`.
- Use native BCL features, `ReadOnlyCollection<T>`, and standard arrays.

## 2. Model & Immutability Rules
- **Immutability by Default**: `SubtitleDocument` and `SubtitleCue` are immutable. Operations like `TimeShift(...)` return new instances.
- **Defensive Copying**: Never store raw user-provided `List<T>` or `IEnumerable<T>`. Always copy defensively in constructors into `ReadOnlyCollection<T>`.
- **Single Source of Truth**:
  - `SubtitleCue.RawText` is the canonical text payload.
  - `SubtitleCue.PlainText` is lazily derived from `RawText` based on format. Never store a separate mutable `PlainText` field.
  - VTT Voice tags `<v Name>` stay strictly inside `RawText`. Do NOT add a `Voice` property to `VttCueData`.
  - VTT Cue Settings store both verbatim `string RawSettings` and parsed `VttCueSettings Settings`.
- **Typed Format Bags**:
  - Never use `object? FormatData`. Use `CueFormatData` and `DocumentFormatData` abstract base classes.
  - Derive format-specific subclasses: `AssCueData`, `VttCueData`, `AssDocumentData`, `VttDocumentData`.
  - When converting formats, stale format data is stripped and logged as `ConversionNoticeCode.StaleFormatDataDropped`.

## 3. Text & Line-Ending Normalization
- **Internal Normalization**: All incoming multiline text in `SubtitleCue.RawText` MUST be normalized to `\n` on parse and construction:
  ```csharp
  RawText = rawText.Replace("\r\n", "\n").Replace('\r', '\n');
  ```
- **Serialization**: Line endings are translated to `SubtitleWriteOptions.LineEnding` (default `\r\n`) strictly upon serialization.
- ASS dialogue text uses `\N` for hard linebreaks.

## 4. Encoding & Resilient I/O
- Always check BOM in exact priority order:
  1. UTF-32 LE (`FF FE 00 00`) & UTF-32 BE (`00 00 FE FF`)
  2. UTF-16 LE (`FF FE`) & UTF-16 BE (`FE FF`)
  3. UTF-8 BOM (`EF BB BF`)
- When no BOM is present, attempt strict UTF-8 (`new UTF8Encoding(false, throwOnInvalidBytes: true)`).
- On `DecoderFallbackException`, fall back to configured `SubtitleReadOptions.FallbackEncoding` (defaults to `ISO-8859-1` / Latin-1 so invalid bytes are never corrupted into `U+FFFD`).
- VTT writing is strictly UTF-8 without BOM. ASS and SRT writing support configurable `SubtitleWriteOptions.EmitBom`.

## 5. Conversion & Save Safety
- `Subtitle.Save(...)` must **throw `InvalidOperationException`** if `targetFormat != doc.Format`.
- The only permissible method for changing subtitle formats is `Subtitle.Convert(doc, targetFormat, options)`.
- Conversions must return a `ConversionResult` containing `SubtitleDocument` and a structured `ConversionReport` with `ConversionNotice` items (Severity, Code, CueIndex, FeatureName, Message).

## 6. Format-Specific Invariants
- **SRT**: Re-indexes sequentially starting at 1 on output. Format timestamps as `hh:mm:ss,fff`.
- **WebVTT**: Emits `WEBVTT` signature header. Preserves `X-TIMESTAMP-MAP`. Anchors `NOTE`, `STYLE`, and `REGION` blocks via `BeforeCueIndex` to maintain exact positional ordering. Cues must be ordered chronologically by `Start`.
- **ASS vs SSA**:
  - SSA (v4.00): Uses `[V4 Styles]`, `Marked=` (not Layer), and 1–11 alignments (1–3 bottom, 5–7 top, 9–11 middle).
  - ASS (v4.00+): Uses `[V4+ Styles]`, `Layer`, and 1–9 numpad alignments.
  - Never conflate SSA with ASS.
  - ASS timestamps format as `h:mm:ss.cc` with centisecond rounding `(ms + 5) / 10`.
  - Preserve unknown sections (`[Aegisub Project Garbage]`, `[Fonts]`, etc.) verbatim.
  - Split event lines with a max field count equal to `Format:` column count so commas in `Text` remain un-split.
