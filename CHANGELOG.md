# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Any change to a computed result is a breaking change (a major version), because simulations built on FPM pin their results.

## [Unreleased]

### Added

- The fixed-point math, moved here from Deterministic Path Finding (where it lived in `DPF.Core.Numerics`) unchanged: every result is bit for bit what it was.
  - `FP`: Q48.16 by default, Q32.32 with `FPM_Q32_32`. `*` and `/` are exact and round once, with ties away from zero. `/` never throws and saturates. Exact 64-bit paths for small operands.
  - `FP2` and `FP3`: exact dot and cross products, exact lengths (floor of the true value), and normalization that stays accurate for tiny vectors.
  - `FPMath`: `Sqrt` (exact floor), `Sin`, `Cos`, `SinCos` and `Atan2` from generated lookup tables, plus `MulDiv`, rounding and clamping.
  - `WideInt`: 128-bit integer arithmetic for exact intermediates, with two-digit long division and Newton square roots.
  - `DeterministicRandom`: PCG32, matching the reference sequence, with unbiased ranges.
- `FPM.Unity`: an optional assembly with conversions between `FP` and floats or Unity vectors, for presentation and input only.
- `FPM.Analyzers`: a Roslyn analyzer for any assembly marked `[assembly: FPM.Deterministic]` (FPM.Runtime is), rejecting floating-point types, `System.Math`, hash-ordered collections, `System.Random`, time and threading APIs, `lock`, `checked` and default `GetHashCode` (diagnostics `FPM0001` to `FPM0005`).
- Tests: exact checks against `BigInteger` and `double` references, golden values per format, and `dotnet test` projects and CI that run them on Windows, Linux and macOS in both formats.
