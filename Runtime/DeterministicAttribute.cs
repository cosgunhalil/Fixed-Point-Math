using System;

namespace FPM
{
    // Marks an assembly as deterministic simulation code: FPM.Analyzers then rejects floating point, System.Math,
    // hash-ordered collections, System.Random, time and threading APIs, lock, checked and default GetHashCode in it.
    // Apply it once, in any file of the assembly: [assembly: FPM.Deterministic].
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class DeterministicAttribute : Attribute
    {
    }
}
