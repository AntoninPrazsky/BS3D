using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace BS3D.Tools.VectorBoundary
{
    /// <summary>
    /// Finds every crossing between <c>System.Numerics</c> and <c>Microsoft.Xna.Framework</c> that goes through
    /// a user-defined conversion operator rather than a named call (#400). CLAUDE.md's convention is
    /// <c>ToNumerics()</c> outbound and <c>ToXna()</c> inbound at every crossing, so the boundary shows on the
    /// line; MonoGame 3.8.5 also declares an implicit operator for the inbound direction, and a crossing through
    /// it compiles with nothing on the line at all — a regex cannot see one, the compiler's bound tree can.
    /// <para>
    /// It reports <b>VEC001</b> as a warning, <c>IMPLICIT</c> for a conversion the source never spelled and
    /// <c>CAST</c> for one written as a cast. A hand-run scan, not wired into any project: see
    /// <c>VectorBoundary.targets</c> and "The vector boundary scan" in <c>docs/formats-and-tools.md</c>.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class VectorBoundaryAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new(
            "VEC001", "Xna/Numerics crossing through a conversion operator",
            "{0} conversion {1} -> {2} through {3}'s operator; cross with ToXna()/ToNumerics()",
            "BS3D", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(Check, OperationKind.Conversion);
        }

        private static void Check(OperationAnalysisContext context)
        {
            var conversion = (IConversionOperation)context.Operation;
            IMethodSymbol op = conversion.OperatorMethod;
            if (op == null) return;   //built-in conversions only; a named ToXna() is an invocation, not this

            string from = conversion.Operand.Type?.ToDisplayString() ?? "?";
            string to = conversion.Type?.ToDisplayString() ?? "?";
            bool crosses = (IsNumerics(from) && IsXna(to)) || (IsXna(from) && IsNumerics(to));
            if (!crosses) return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, conversion.Syntax.GetLocation(),
                conversion.IsImplicit ? "IMPLICIT" : "CAST", from, to, op.ContainingType.ToDisplayString()));
        }

        private static bool IsNumerics(string type) => type.StartsWith("System.Numerics.");
        private static bool IsXna(string type) => type.StartsWith("Microsoft.Xna.Framework.");
    }
}
