using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace FPM.Analyzers
{
    // Rejects code that could make a deterministic simulation differ between machines, platforms or scripting
    // backends, in every assembly marked [assembly: FPM.Deterministic] (FPM.Runtime is, and so can any simulation
    // built on FPM). Unity applies an analyzer to the assembly it sits in and to every assembly that references it,
    // so unmarked assemblies (adapters, editor tools, tests) are left alone and may use these APIs.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
    {
        // The attribute that opts an assembly in, by metadata name: FPM.Runtime defines it.
        public const string DeterministicAttributeName = "FPM.DeterministicAttribute";

        private const string Category = "Determinism";

        public static readonly DiagnosticDescriptor FloatingPointRule = new DiagnosticDescriptor(
            "FPM0001",
            "Floating-point type in a deterministic assembly",
            "Floating-point type '{0}' is not allowed in a deterministic assembly: float results differ between platforms and compilers. Use FPM fixed-point types.",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor BannedTypeRule = new DiagnosticDescriptor(
            "FPM0002",
            "Nondeterministic API in a deterministic assembly",
            "'{0}' is not allowed in a deterministic assembly: {1}",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor CheckedRule = new DiagnosticDescriptor(
            "FPM0003",
            "Checked arithmetic in a deterministic assembly",
            "'checked' is not allowed in a deterministic assembly: overflow must behave the same under Burst and managed code. Use the widening helpers instead.",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor LockRule = new DiagnosticDescriptor(
            "FPM0004",
            "lock in a deterministic assembly",
            "'lock' is not allowed in a deterministic assembly: deterministic code never synchronizes threads. Parallel work writes only its own slots.",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor HashCodeRule = new DiagnosticDescriptor(
            "FPM0005",
            "Default GetHashCode in a deterministic assembly",
            "'{0}.GetHashCode' is not allowed in a deterministic assembly: its value can differ between processes and runtimes. Hash with an explicit, stable function.",
            Category,
            DiagnosticSeverity.Error,
            true);

        private const string MathReason = "its results depend on the platform's floating-point library. Use FPM fixed-point math.";
        private const string RandomReason = "its algorithm isn't specified and can change between runtimes. Use FPM.DeterministicRandom.";
        private const string TimeReason = "wall-clock time and machine state differ between machines. Count work units per tick.";
        private const string ThreadingReason = "deterministic code never schedules work itself. The host runs it in a fixed order.";
        private const string HashOrderReason = "its iteration order depends on hash codes. Use arrays indexed by ID.";
        private const string LinqReason = "some operators order results by hash codes, and all of them allocate. Write plain loops.";
        private const string FloatVectorReason = "it is built on floats. Use FPM fixed-point vectors.";

        private static readonly KeyValuePair<string, string>[] BannedTypeNames =
        {
            new KeyValuePair<string, string>("System.Math", MathReason),
            new KeyValuePair<string, string>("System.MathF", MathReason),
            new KeyValuePair<string, string>("System.Random", RandomReason),
            new KeyValuePair<string, string>("System.DateTime", TimeReason),
            new KeyValuePair<string, string>("System.DateTimeOffset", TimeReason),
            new KeyValuePair<string, string>("System.Environment", TimeReason),
            new KeyValuePair<string, string>("System.Diagnostics.Stopwatch", TimeReason),
            new KeyValuePair<string, string>("System.Threading.Thread", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.ThreadPool", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Interlocked", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Monitor", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Volatile", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Tasks.Task", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Tasks.Task`1", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Tasks.ValueTask", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Tasks.ValueTask`1", ThreadingReason),
            new KeyValuePair<string, string>("System.Threading.Tasks.Parallel", ThreadingReason),
            new KeyValuePair<string, string>("System.Collections.Hashtable", HashOrderReason),
            new KeyValuePair<string, string>("System.Collections.Generic.Dictionary`2", HashOrderReason),
            new KeyValuePair<string, string>("System.Collections.Generic.HashSet`1", HashOrderReason),
            new KeyValuePair<string, string>("System.Collections.Concurrent.ConcurrentDictionary`2", HashOrderReason),
            new KeyValuePair<string, string>("System.Collections.Concurrent.ConcurrentBag`1", HashOrderReason),
            new KeyValuePair<string, string>("System.Linq.Enumerable", LinqReason),
            new KeyValuePair<string, string>("System.Numerics.Vector`1", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Vector2", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Vector3", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Vector4", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Matrix3x2", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Matrix4x4", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Quaternion", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Plane", FloatVectorReason),
            new KeyValuePair<string, string>("System.Numerics.Complex", FloatVectorReason),
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            FloatingPointRule, BannedTypeRule, CheckedRule, LockRule, HashCodeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!IsDeterministic(context.Compilation))
            {
                return;
            }

            Dictionary<INamedTypeSymbol, string> bannedTypes = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
            foreach (KeyValuePair<string, string> entry in BannedTypeNames)
            {
                INamedTypeSymbol type = context.Compilation.GetTypeByMetadataName(entry.Key);
                if (type != null)
                {
                    bannedTypes[type] = entry.Value;
                }
            }

            context.RegisterSyntaxNodeAction(AnalyzePredefinedType, SyntaxKind.PredefinedType);
            context.RegisterSyntaxNodeAction(c => AnalyzeName(c, bannedTypes), SyntaxKind.IdentifierName, SyntaxKind.GenericName);
            context.RegisterSyntaxNodeAction(AnalyzeChecked, SyntaxKind.CheckedExpression, SyntaxKind.CheckedStatement);
            context.RegisterSyntaxNodeAction(AnalyzeLock, SyntaxKind.LockStatement);
            context.RegisterOperationAction(AnalyzeFloatingValue,
                OperationKind.Literal, OperationKind.Conversion, OperationKind.Binary, OperationKind.Unary,
                OperationKind.Invocation, OperationKind.PropertyReference, OperationKind.FieldReference,
                OperationKind.ObjectCreation);
            context.RegisterOperationAction(AnalyzeHashCode, OperationKind.Invocation);
        }

        // float, double and decimal keywords anywhere: declarations, casts, typeof, generic arguments.
        private static void AnalyzePredefinedType(SyntaxNodeAnalysisContext context)
        {
            PredefinedTypeSyntax syntax = (PredefinedTypeSyntax)context.Node;
            SyntaxKind kind = syntax.Keyword.Kind();
            if (kind == SyntaxKind.FloatKeyword || kind == SyntaxKind.DoubleKeyword || kind == SyntaxKind.DecimalKeyword)
            {
                context.ReportDiagnostic(Diagnostic.Create(FloatingPointRule, syntax.GetLocation(), syntax.Keyword.Text));
            }
        }

        // Named types: banned types however they are written (qualified, generic, aliased, `using static`), and
        // floating-point types written by name (`Single`) or inferred through `var`.
        private static void AnalyzeName(SyntaxNodeAnalysisContext context, Dictionary<INamedTypeSymbol, string> bannedTypes)
        {
            SymbolInfo info = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken);
            ISymbol symbol = info.Symbol;
            if (symbol == null && info.CandidateSymbols.Length > 0)
            {
                symbol = info.CandidateSymbols[0];
            }

            if (symbol is IAliasSymbol alias)
            {
                symbol = alias.Target;
            }

            if (!(symbol is ITypeSymbol type))
            {
                return;
            }

            if (IsFloatingPoint(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(FloatingPointRule, context.Node.GetLocation(), type.ToDisplayString()));
                return;
            }

            if (type is INamedTypeSymbol named && bannedTypes.TryGetValue(named.OriginalDefinition, out string reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(BannedTypeRule, context.Node.GetLocation(), named.OriginalDefinition.ToDisplayString(), reason));
            }
        }

        private static void AnalyzeChecked(SyntaxNodeAnalysisContext context)
        {
            SyntaxToken keyword = context.Node is CheckedExpressionSyntax expression
                ? expression.Keyword
                : ((CheckedStatementSyntax)context.Node).Keyword;
            context.ReportDiagnostic(Diagnostic.Create(CheckedRule, keyword.GetLocation()));
        }

        private static void AnalyzeLock(SyntaxNodeAnalysisContext context)
        {
            LockStatementSyntax syntax = (LockStatementSyntax)context.Node;
            context.ReportDiagnostic(Diagnostic.Create(LockRule, syntax.LockKeyword.GetLocation()));
        }

        // Floating-point values that never name a type: literals like 1.5 or 2f, and floats returned by BCL members.
        // Only the outermost floating-point expression is reported, so `1.5 * 2.0` gives one diagnostic, not three.
        private static void AnalyzeFloatingValue(OperationAnalysisContext context)
        {
            IOperation operation = context.Operation;
            if (!IsFloatingPoint(operation.Type) || (operation.IsImplicit && operation.Kind != OperationKind.Conversion))
            {
                return;
            }

            IOperation parent = operation.Parent;
            if (parent != null && IsFloatingPoint(parent.Type))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(FloatingPointRule, operation.Syntax.GetLocation(), operation.Type.ToDisplayString()));
        }

        // Object.GetHashCode is identity-based, ValueType.GetHashCode is runtime-defined and String.GetHashCode is
        // randomized per process on .NET Core. Overrides in our own types are explicit and allowed.
        private static void AnalyzeHashCode(OperationAnalysisContext context)
        {
            IInvocationOperation invocation = (IInvocationOperation)context.Operation;
            IMethodSymbol method = invocation.TargetMethod;
            if (method.Name != "GetHashCode" || method.Parameters.Length != 0)
            {
                return;
            }

            SpecialType owner = method.ContainingType.SpecialType;
            if (owner == SpecialType.System_Object || owner == SpecialType.System_ValueType || owner == SpecialType.System_String)
            {
                context.ReportDiagnostic(Diagnostic.Create(HashCodeRule, invocation.Syntax.GetLocation(), method.ContainingType.ToDisplayString()));
            }
        }

        // Whether the compilation carries [assembly: FPM.Deterministic].
        private static bool IsDeterministic(Compilation compilation)
        {
            INamedTypeSymbol attribute = compilation.GetTypeByMetadataName(DeterministicAttributeName);
            if (attribute == null)
            {
                return false;
            }
            foreach (AttributeData data in compilation.Assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsFloatingPoint(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                    return true;
            }

            return type.Name == "Half" && type.ContainingNamespace != null && type.ContainingNamespace.ToDisplayString() == "System";
        }
    }
}
