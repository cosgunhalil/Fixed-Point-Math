# Fixed-Point Math (FPM)

Deterministic fixed-point math for Unity and .NET, built for **lockstep and rollback simulations**. Every operation gives **bit-identical results on every machine, platform and scripting backend**: Mono, IL2CPP, Burst or plain .NET.

> **Status: pre-release.** The math is complete and tested in both formats. The package hasn't had a release yet, so its API may still change. See the [changelog](CHANGELOG.md).

## What's in it

- **`FP`**: a fixed-point number in a `long`. Q48.16 by default; define `FPM_Q32_32` for Q32.32 across the whole project. `+` and `-` wrap like integers; `*` and `/` are exact before rounding once (to nearest, ties away from zero); `/` never throws and saturates.
- **`FP2`, `FP3`**: vectors with exact dot and cross products (summed in 128 bits, rounded once), exact lengths (the floor of the true length) and normalisation that stays accurate for tiny vectors.
- **`FPMath`**: `Abs`, `Sign`, `Min`, `Max`, `Clamp`, `Floor`, `Ceil`, `Round`, `Frac`, `Lerp`, `MulDiv`, `Sqrt` (the exact floor), and `Sin`, `Cos`, `SinCos` and `Atan2` from generated lookup tables.
- **`WideInt`**: 128-bit integers for exact intermediates: multiply, divide, square root.
- **`DeterministicRandom`**: PCG32, seedable and serializable (its state is two `ulong`s), with unbiased ranges. It isn't arithmetic, but a deterministic simulation needs both, so it ships here.
- **`FPM.Unity`** (optional assembly): conversions between `FP` and floats or Unity vectors, for presentation and input only.
- **`FPM.Analyzers`**: a Roslyn analyzer that fails the build when an assembly marked `[assembly: FPM.Deterministic]` uses floats, `System.Math`, hash-ordered collections, `System.Random`, time or threading APIs, `lock`, `checked` or default `GetHashCode`.

`FPM.Runtime` references no Unity assembly and is written in Burst's supported subset of C#.

## Requirements

Unity **2021.3** or newer (developed with Unity 6000.3), or any .NET Standard 2.1 runtime. No other packages.

## Installation

In Unity, open **Window → Package Manager**, click **+ → Add package from git URL…** and enter:

```
https://github.com/cosgunhalil/Fixed-Point-Math.git
```

To pin a release, add `#vX.Y.Z` to the URL. Packages that build on FPM declare it the same way in their own `package.json`.

## Using it

```csharp
using FPM;

FP speed = FP.FromRatio(7, 2);                  // 3.5, exactly
FP3 velocity = new FP3(speed, FP.Zero, FP.One);
FP length = velocity.Length;                    // floor of the true length
FP3 direction = velocity.Normalized;
FP angle = FPMath.Atan2(direction.Z, direction.X);

DeterministicRandom random = new DeterministicRandom(seed: 42);
FP roll = random.NextFP();                      // [0, 1), the same sequence everywhere
```

**Floats only at the edges.** Converting a float to `FP` is exact on the machine that does it, but another machine may round its float differently. Convert input once, where it comes from (the local player, or the Editor when baking), and send or store the `FP` value, never the float. `FPM.Unity` has the conversions (`ToFP`, `ToFP3`, `ToVector3`, `ToFloat`).

**Protect your own simulation.** Add `[assembly: FPM.Deterministic]` to any assembly that must stay deterministic: the analyzer then checks it like FPM itself. Assemblies without it (adapters, editor tools, tests) may use floats freely.

**One format per project.** `FPM_Q32_32` changes every `FP` in the project. Data stored as raw values (snapshots, replays) only loads into a build with the same format.

## Documentation

The [manual](Documentation~/index.md) covers the number formats, the rounding and overflow rules, the analyzer, and how to check determinism on your platforms.

## Contributing

See [CONTRIBUTING.md](.github/CONTRIBUTING.md).

## License

[MIT](LICENSE)
