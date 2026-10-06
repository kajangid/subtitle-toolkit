# SubtitleToolkit Agent Guidelines

## Identity & Philosophy
You are working on **SubtitleToolkit**, a modern, lightweight, dependency-free .NET library for parsing, writing, converting, and time-shifting SRT, WebVTT, ASS, and SSA subtitle files.

We operate under **Ponytail** principles:
- **Lazy senior developer**: The best code is the code never written. Solve problems using the fewest, cleanest lines possible.
- **Standard Library first**: Never add third-party dependencies for things the .NET BCL covers natively.
- **Zero-Dependency contract**: The NuGet package must have 0 runtime package dependencies on both `netstandard2.0` and `net8.0`.
- **Root-cause correctness**: Fix bugs where all callers route through. Never paper over symptoms.
- **Defensive immutability**: All operations (`TimeShift`, conversions) return new documents. Document models defensively copy collections on construction.
- **Strict single source of truth**: No dual-state drifting between raw strings and parsed helper properties.

## Working Modes & Boundaries
- **Target Frameworks**: Multi-target `netstandard2.0;net8.0`.
  - `netstandard2.0`: Maximum compatibility for Unity and .NET Framework. No `Span<T>` or `ImmutableArray<T>` packages. Use standard copied collections and string slices.
  - `net8.0`: High-performance LTS runtime.
- **Polyfills**: Only `PolySharp` and `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PrivateAssets="all"`. Never leak polyfill packages to consumers.
- **Non-Goals**: No video rendering/rasterization, no video container muxing (FFmpeg), no HLS playlist/segment chunking, no 300-format legacy long-tail in v1.0.

## Development Workflow
1. Read existing files and contracts before touching code.
2. Maintain strict round-trip fidelity:
   - SRT: 1-based sequential renumbering, `hh:mm:ss,fff`.
   - VTT: `WEBVTT` signature, `X-TIMESTAMP-MAP`, anchored non-cue blocks, `hh:mm:ss.fff`.
   - ASS: `[Script Info]`, `[V4+ Styles]`, `[Events]`, centisecond `h:mm:ss.cc`, unknown section retention.
   - SSA: `[V4 Styles]`, `Marked=` (not Layer), SSA 1–11 alignments (not numpad).
3. Test every non-trivial piece of logic with runnable unit tests and property tests before marking complete.
