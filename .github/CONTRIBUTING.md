# Contributing to Fixed-Point Math

Thanks for your interest in FPM! Bug reports, ideas and pull requests are all welcome. By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Reporting bugs and suggesting features

Search the existing issues first, then open a new one. For a bug, include:

- your FPM, Unity and Burst versions, and the fixed-point format (Q48.16, or Q32.32 with `FPM_Q32_32`);
- the platforms and scripting backends involved (Mono, IL2CPP, Burst on or off);
- the smallest code that shows the problem, with raw values (`FP.RawValue`) rather than floats;
- what you expected, what happened, and any errors from the Console.

Security problems should not be reported in public issues. See the [security policy](SECURITY.md).

## Setting up

FPM is a Unity package with no standalone build. To work on it:

1. Create or open a Unity project (2021.3 or newer; development uses Unity 6000.3).
2. Clone the repository into the project's `Assets` or `Packages` folder.
3. Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer, to build the analyzer and run the tests outside Unity.
4. Build the analyzer with `Analyzers~/build-analyzer.ps1`. The first time, select `Runtime/Analyzers/FPM.Analyzers.dll` in Unity, uncheck every platform and add the asset label `RoslynAnalyzer`. `AnalyzerSetupTests` checks both.

The code is split into these assemblies:

- `FPM.Runtime` (`Runtime/`) is the math. It references no Unity assembly (`noEngineReferences`), so it also compiles and runs under plain .NET, and it is marked `[assembly: FPM.Deterministic]`.
- `FPM.Unity` (`Runtime/Unity/`) converts between fixed point and Unity types. It is optional.
- `FPM.Tests` (`Tests/`) is Unity-free and also runs under `dotnet test`. `FPM.Tests.Editor` (`Tests/Editor/`) checks the analyzer's import settings in Unity.
- `FPM.Analyzers` (`Analyzers~/`) is the Roslyn analyzer, built into `Runtime/Analyzers/`.

## Making changes

**Every result is a contract.** Simulations built on FPM pin their results, so any change that alters even one bit of one result is a breaking change: it needs a major version and a clear entry in the changelog. Performance work must give exactly the same integers, and must be checked against `BigInteger` (`Oracle`) to prove it.

In `FPM.Runtime`:

- Use no `float`, `double`, `decimal`, `System.Math` or `System.Numerics` vectors. The analyzer enforces this.
- Guard every division, shift and square root explicitly, and never use `checked`. Burst and managed code must behave the same.
- Write in Burst's supported subset of C#: structs, no classes, and no managed arrays as parameters.
- Keep rounding documented: once, to nearest, ties away from zero, unless a function says otherwise (floors for square roots and lengths).

The other rules:

- **Keep the style of the code around you**: naming, comment density and structure. `.editorconfig` covers formatting.
- **Commit the `.meta` files** Unity creates for new files and folders, and never edit their GUIDs by hand.
- **Keep the minimum Unity version.** Don't use APIs newer than Unity 2021.3 without an `#if UNITY_x_OR_NEWER` guard.
- **Update the README and CHANGELOG** when the public API or behavior changes.

## Tests

Run the tests before opening a pull request, in both formats:

- In Unity: **Window → General → Test Runner → EditMode**, then again with `FPM_Q32_32` in **Scripting Define Symbols**.
- Outside Unity:

  ```
  dotnet test "Analyzers~/FPM.Analyzers.Tests/FPM.Analyzers.Tests.csproj"
  dotnet test "Tests~/DotNet/FPM.Tests/FPM.Tests.csproj"
  dotnet test "Tests~/DotNet/FPM.Tests/FPM.Tests.csproj" -p:FpmDefines=FPM_Q32_32
  ```

Add tests for new behavior, checked against exact arithmetic. `NumericsGoldenTests` pins one hash of many results per format: if it fails, your change altered results that existing simulations depend on.

## Commit messages

Commits follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
type(scope): short description
```

- **Types**: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`.
- **Scopes** (optional): `math`, `vectors`, `random`, `unity`, `analyzer`, `ci`, `setup`.
- **Breaking changes** are marked with `!` after the type or scope, or with a `BREAKING CHANGE:` footer. Any change to a computed result is breaking.
- The description is lowercase and imperative, with no trailing period, and the subject line is at most 72 characters.

Examples: `feat(math): add exact cube root`, `fix(vectors): normalize vectors near the format's range`.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each pull request focused on one change.
3. Fill in the pull request template: what changed, why, and how you tested it.
4. Make sure the tests pass and the README and CHANGELOG are up to date.

By contributing, you agree that your contributions are licensed under the project's [MIT License](../LICENSE).
