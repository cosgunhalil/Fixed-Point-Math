using System.Runtime.CompilerServices;

// The fixed-point math itself. It references no Unity assembly (noEngineReferences), and FPM.Analyzers checks it like
// any assembly marked deterministic, so everything here runs bit-identically under Burst, Mono, IL2CPP and plain .NET.
[assembly: FPM.Deterministic]
[assembly: InternalsVisibleTo("FPM.Tests")]
