using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Serious.Abbot.Compilation;

public class ScriptVerifier : IScriptVerifier
{
    // CREDIT: https://joymonscode.blogspot.com/2015/06/running-roslyn-analyzers-from-console.html
    static CompilationWithAnalyzers GetAnalyzerAwareCompilation(Microsoft.CodeAnalysis.Compilation compilation)
    {
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());

        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            new CancellationToken());
        return compilationWithAnalyzers;
    }

    public async Task<ImmutableArray<ICompilationError>> RunAnalyzersAsync(Microsoft.CodeAnalysis.Compilation compilation)
    {
        CompilationWithAnalyzers compilationWithAnalyzers = GetAnalyzerAwareCompilation(compilation);

        ImmutableArray<Diagnostic> diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        return diagnosticResults.Select(d => new CompilationError(d))
            .Cast<ICompilationError>()
            .ToImmutableArray();
    }
}
