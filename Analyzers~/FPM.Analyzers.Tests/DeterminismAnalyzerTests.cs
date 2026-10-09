using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace FPM.Analyzers.Tests
{
    public class DeterminismAnalyzerTests
    {
        [TestCase("float value;", "float")]
        [TestCase("double value;", "double")]
        [TestCase("decimal value;", "decimal")]
        [TestCase("System.Single value;", "float")]
        [TestCase("long value = (long)1.5;", "double")]
        [TestCase("long value = (long)2f;", "float")]
        [TestCase("object value = typeof(float);", "float")]
        [TestCase("System.Collections.Generic.List<double> value;", "double")]
        public void FloatingPointType_IsReported(string member, string typeName)
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze(Class(member));

            Assert.That(diagnostics.Any(d => d.Id == "FPM0001" && d.GetMessage().Contains("'" + typeName + "'")), Is.True, Describe(diagnostics));
        }

        [Test]
        public void InferredFloatingPoint_IsReported()
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze(Class("void M() { var value = System.BitConverter.Int64BitsToDouble(1); }"));

            Assert.That(Ids(diagnostics), Does.Contain("FPM0001"));
        }

        [Test]
        public void NestedFloatingPointExpression_IsReportedOnce()
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze(Class("long value = (long)(1.5 * 2.0 + 3.0);"));

            Assert.That(diagnostics.Count(d => d.Id == "FPM0001"), Is.EqualTo(1), Describe(diagnostics));
        }

        [TestCase("int value = System.Math.Abs(-1);", "System.Math")]
        [TestCase("System.Random random = new System.Random(1);", "System.Random")]
        [TestCase("long value = System.DateTime.Now.Ticks;", "System.DateTime")]
        [TestCase("int value = System.Environment.TickCount;", "System.Environment")]
        [TestCase("System.Diagnostics.Stopwatch watch;", "System.Diagnostics.Stopwatch")]
        [TestCase("System.Threading.Tasks.Task task;", "System.Threading.Tasks.Task")]
        [TestCase("void M(ref int value) { System.Threading.Interlocked.Increment(ref value); }", "System.Threading.Interlocked")]
        [TestCase("System.Collections.Generic.Dictionary<int, int> map;", "System.Collections.Generic.Dictionary<TKey, TValue>")]
        [TestCase("System.Collections.Generic.HashSet<int> set;", "System.Collections.Generic.HashSet<T>")]
        [TestCase("int value = System.Linq.Enumerable.Count(new int[1]);", "System.Linq.Enumerable")]
        [TestCase("System.Numerics.Vector3 position;", "System.Numerics.Vector3")]
        public void BannedType_IsReported(string member, string typeName)
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze(Class(member));

            Assert.That(diagnostics.Any(d => d.Id == "FPM0002" && d.GetMessage().StartsWith("'" + typeName + "'")), Is.True, Describe(diagnostics));
        }

        [Test]
        public void BannedTypeThroughUsingStatic_IsReported()
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze("using static System.Math;\n" + Class("int value = Abs(-1);"));

            Assert.That(Ids(diagnostics), Does.Contain("FPM0002"));
        }

        [Test]
        public void BannedTypeThroughAlias_IsReported()
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze("using Map = System.Collections.Generic.Dictionary<int, int>;\n" + Class("Map map;"));

            Assert.That(diagnostics.Count(d => d.Id == "FPM0002"), Is.EqualTo(2), Describe(diagnostics));
        }

        [TestCase("int M(int a, int b) { return checked(a + b); }")]
        [TestCase("int M(int a, int b) { checked { return a + b; } }")]
        public void Checked_IsReported(string member)
        {
            Assert.That(Ids(Analyze(Class(member))), Does.Contain("FPM0003"));
        }

        [Test]
        public void Lock_IsReported()
        {
            Assert.That(Ids(Analyze(Class("readonly object gate = new object(); void M() { lock (gate) { } }"))), Does.Contain("FPM0004"));
        }

        [TestCase("int M(string text) { return text.GetHashCode(); }")]
        [TestCase("int M(object value) { return value.GetHashCode(); }")]
        [TestCase("struct S { } int M(S value) { return value.GetHashCode(); }")]
        public void DefaultGetHashCode_IsReported(string member)
        {
            Assert.That(Ids(Analyze(Class(member))), Does.Contain("FPM0005"));
        }

        [TestCase("int M(int value) { return value.GetHashCode(); }")]
        [TestCase("struct S { public override int GetHashCode() { return 1; } } int M(S value) { return value.GetHashCode(); }")]
        public void ExplicitGetHashCode_IsNotReported(string member)
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze(Class(member));

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        [Test]
        public void IntegerCode_HasNoDiagnostics()
        {
            string source = Attribute + @"
namespace Simulation
{
    public unsafe struct Buffer
    {
        public long* Values;
        public int Count;

        public long Sum()
        {
            long sum = 0;
            for (int i = 0; i < Count; i++)
            {
                sum += unchecked(Values[i] * 3) >> 16;
            }
            return sum;
        }
    }

    public static class Lists
    {
        public static System.Collections.Generic.List<int> Create() { return new System.Collections.Generic.List<int>(); }
    }
}";
            ImmutableArray<Diagnostic> diagnostics = Analyze(source);

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        // Only assemblies marked [assembly: FPM.Deterministic] are checked: adapters, tools and tests are not.
        [Test]
        public void UnmarkedAssembly_IsNotAnalyzed()
        {
            ImmutableArray<Diagnostic> diagnostics = Analyze(AttributeType + Class("float value = 1.5f; System.Random random;", false));

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        // The attribute as FPM.Runtime defines it. Assembly attributes must come before any namespace, so Attribute
        // applies it first and declares it after.
        private const string AttributeType = "namespace FPM\n{\n    [System.AttributeUsage(System.AttributeTargets.Assembly)]\n"
            + "    public sealed class DeterministicAttribute : System.Attribute { }\n}\n";

        private const string Attribute = "[assembly: FPM.Deterministic]\n" + AttributeType;

        private static string Class(string members, bool marked = true)
        {
            return (marked ? Attribute : "") + "namespace Simulation\n{\n    public unsafe class Sample\n    {\n        " + members + "\n    }\n}\n";
        }

        private static ImmutableArray<Diagnostic> Analyze(string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "Simulation",
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp9)) },
                PlatformReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

            Diagnostic[] errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            Assert.That(errors, Is.Empty, "The sample itself must compile:\n" + string.Join("\n", errors.Select(e => e.ToString())));

            CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new DeterminismAnalyzer()));
            return withAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
        }

        private static IEnumerable<MetadataReference> PlatformReferences()
        {
            string assemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            return assemblies.Split(Path.PathSeparator).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
        }

        private static string[] Ids(ImmutableArray<Diagnostic> diagnostics)
        {
            return diagnostics.Select(d => d.Id).ToArray();
        }

        private static string Describe(ImmutableArray<Diagnostic> diagnostics)
        {
            return "Diagnostics:\n" + string.Join("\n", diagnostics.Select(d => d.ToString()));
        }
    }
}
