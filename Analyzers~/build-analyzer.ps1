# Builds FPM.Analyzers against Microsoft.CodeAnalysis 3.8 and copies it to Runtime/Analyzers, where Unity applies it to
# FPM.Runtime and every assembly that references it (it checks only those marked [assembly: FPM.Deterministic]).
# Needs the .NET SDK (8 or newer). After the first copy, select the DLL in Unity and:
#   - uncheck every platform under "Select platforms for plugin" (it must never be included in a build);
#   - add the asset label "RoslynAnalyzer".
$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "FPM.Analyzers/FPM.Analyzers.csproj"
$built = Join-Path $PSScriptRoot "FPM.Analyzers/bin/Release/netstandard2.0/FPM.Analyzers.dll"
$target = Join-Path $PSScriptRoot "../Runtime/Analyzers"

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item $built $target -Force
Write-Host "Copied FPM.Analyzers.dll to $((Resolve-Path $target).Path)"
