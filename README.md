# SubtitleToolkit

A modern, lightweight, zero-dependency .NET library for parsing, converting, and time-shifting **SRT**, **WebVTT**, **ASS**, and **SSA** subtitle files with 100% round-trip fidelity and machine-readable conversion diagnostics.

Targeting `netstandard2.0` and `net8.0` with **zero runtime package dependencies**.

---

## Features

- **Multi-Format Support**:
  - **SubRip (.srt)**: Resilient timestamp parsing (`hh:mm:ss,fff` or `.`), sequential 1-based renumbering, legacy coordinate retention.
  - **WebVTT (.vtt)**: `WEBVTT` signature headers, `X-TIMESTAMP-MAP` retention, anchored non-cue blocks (`NOTE`, `STYLE`, `REGION`), and structured cue settings (`line`, `position`, `size`, `align`, `vertical`).
  - **Advanced SubStation Alpha (.ass)**: Full `[Script Info]`, `[V4+ Styles]`, `[Events]`, dynamic column ordering, centisecond rounding, and unknown section retention (`[Aegisub Project Garbage]`, `[Fonts]`, etc.).
  - **SubStation Alpha v4.00 (.ssa)**: Strict separation from ASS (uses `[V4 Styles]`, `Marked=` instead of `Layer`, and SSA 1–11 alignments).
- **Zero-State-Drift Architecture**:
  - Canonical `RawText` with internal linebreaks strictly normalized to `\n`.
  - Lazy, format-aware `PlainText` derivation.
  - VTT Voice `<v Name>` stays inline in `RawText`.
- **Resilient I/O & Encodings**:
  - Automatic BOM sniffer (UTF-32 LE/BE, UTF-16 LE/BE, UTF-8).
  - Strict UTF-8 trial with automatic fallback to Latin-1/ISO-8859-1 (no silent byte destruction into ``).
- **Save Safety**:
  - `Subtitle.Save` prevents accidental data loss by throwing if `targetFormat != doc.Format`. Format conversion is strictly explicit.

---

## Quick Start

```csharp
using SubtitleToolkit;
using SubtitleToolkit.Model;

// 1. Parse or load subtitle
var result = Subtitle.Load("sample.vtt");
SubtitleDocument doc = result.Document;

// 2. Access cues and clean plain text
foreach (var cue in doc.Cues)
{
    Console.WriteLine($"{cue.Start} -> {cue.End}: {cue.PlainText}");
}

// 3. Save with custom line endings
doc.Save("output.vtt", new SubtitleWriteOptions { LineEnding = "\r\n" });
```

---

## Package Targets

- `netstandard2.0` (Unity, .NET Framework 4.6.2+, Xamarin)
- `net8.0` (Modern .NET LTS)
