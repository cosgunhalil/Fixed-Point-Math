## What changed

<!-- A short summary of the change. Link the issue it closes, for example "Closes #12". -->

## Why

<!-- The problem it solves or the feature it adds. -->

## How it was tested

<!-- Unity version, platforms and scripting backends, fixed-point format. -->

## Checklist

- [ ] The title follows [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/), for example `feat(math): ...` or `fix(analyzer): ...`
- [ ] Edit Mode tests pass in **Window → General → Test Runner**, in both formats (Q48.16 and `FPM_Q32_32`)
- [ ] `dotnet test` passes for `FPM.Analyzers.Tests` and `FPM.Tests`
- [ ] New behavior has tests, checked against exact arithmetic (`Oracle`)
- [ ] The golden values in `NumericsGoldenTests` are unchanged, or the change is intended, explained above, and released as a major version (any change to a result breaks the packages built on FPM)
- [ ] For performance changes: benchmark numbers before and after are in the description
- [ ] `.meta` files are included for new files and folders
- [ ] README, CHANGELOG and `Documentation~` are updated if the public API or behavior changed
