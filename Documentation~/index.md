# Fixed-Point Math

FPM is fixed-point arithmetic that gives the same bits everywhere: on every CPU, platform and scripting backend, with or without Burst. It exists for simulations that run on several machines at once (lockstep and rollback multiplayer, replays, server validation), where one differing bit becomes a desync.

## Why not floats

The IEEE 754 operations themselves are deterministic, but what a compiler and runtime do with them isn't guaranteed: fused multiply-adds, extended precision, reordering, and math library functions (`sin`, `sqrt`) that differ between platforms. Burst, Mono and IL2CPP make different choices. Fixed point is integer arithmetic, which every platform does the same way.

## The number formats

`FP` stores `RawValue / 2^FractionalBits` in a `long`:

| Format | Define | Fraction | Step | Range |
|---|---|---|---|---|
| Q48.16 (default) | none | 16 bits | 1/65,536 ≈ 0.000015 | about ±1.4 × 10^14 |
| Q32.32 | `FPM_Q32_32` | 32 bits | 1/4,294,967,296 ≈ 2.3 × 10^-10 | about ±2.1 × 10^9 |

The define applies to the whole project. The API is the same in both. Q48.16 has the range for large worlds; Q32.32 has the precision for long integrations. Products of two large values overflow sooner in Q32.32, so check the worst case of any new product against ±2^31.

## Rounding and overflow

- `+` and `-` wrap on overflow, like integer arithmetic.
- `*` computes the exact product (in 128 bits when needed) and rounds once, to nearest, ties away from zero. It is symmetric: `(-a) * b == -(a * b)`. It wraps on overflow.
- `/` computes the exact quotient and rounds the same way. It never throws: dividing by zero gives `MaxValue`, `MinValue` or zero, and results outside the range saturate.
- `FPMath.MulDiv(a, b, c)` is `a * b / c` with one rounding and an exact 128-bit intermediate.
- `FPMath.Sqrt` and vector lengths are the floor of the exact root: never larger than the true value.
- Dot and cross products sum exact 128-bit products and round once.
- `Sin`, `Cos` and `Atan2` come from generated lookup tables with interpolation: the same table on every machine. `FPTablesTests` checks the tables' checksums.

## Floats at the edges

`FPM.Unity` converts between `FP` and floats (`ToFP`, `ToFloat`, `ToFP3`, `ToVector3`). Converting a float to `FP` is exact on the machine that does it, but a float computed on another machine may differ in its last bit. So convert input once, where it is created, and send or store the `FP` value. Converting back to floats for display is always safe.

## The analyzer

`FPM.Analyzers` checks every assembly marked with:

```csharp
[assembly: FPM.Deterministic]
```

It reports, as errors:

| ID | What |
|---|---|
| FPM0001 | `float`, `double`, `decimal` or `Half`, written or inferred, and floating-point literals |
| FPM0002 | `System.Math`, `System.Random`, time APIs, threads and tasks, hash-ordered collections, LINQ and `System.Numerics` float vectors |
| FPM0003 | `checked` arithmetic (Burst and managed code must overflow the same way) |
| FPM0004 | `lock` |
| FPM0005 | default `GetHashCode` on `object`, structs and strings |

Unity applies the analyzer to `FPM.Runtime` and to every assembly that references it, but only marked assemblies are checked. Mark your simulation's assembly to get the same protection; leave adapters, editor code and tests unmarked.

## The random generator

`DeterministicRandom` is PCG32: a `State` and an `Increment`, both public, so it can be stored in a snapshot and restored. Seed it with a value every machine agrees on. Ranges (`NextUInt(bound)`, `NextInt`, `NextFP(min, max)`) are unbiased, so the same seed always gives the same values.

## Checking a platform

`NumericsGoldenTests` hashes a fixed set of results per format. Run the EditMode tests (or `dotnet test`) on each platform you care about: the hash must match the pinned value. For player builds, compute the same values in a small scene and compare them with an Editor run.
