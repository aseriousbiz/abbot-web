using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Compilation;

public class DotNetScript : SkillScript
{
    readonly Script _script;

    public DotNetScript(Script script) : base(CodeLanguage.CSharp, script.Code)
    {
        _script = script;
    }

    public override async Task GetCompiledStreamAsync(Stream assemblyStream, Stream? symbolsStream)
    {
        _script.GetCompilation().Emit(assemblyStream, symbolsStream);
        await assemblyStream.FlushAsync();
        if (symbolsStream is not null)
            await symbolsStream.FlushAsync();
    }

    public override ImmutableArray<ICompilationError> Compile() =>
        _script.Compile().Select(CompilationError.Create).ToImmutableArray();

    public override async Task<ImmutableArray<ICompilationError>> AnalyzeAsync(IScriptVerifier verifier) =>
        await verifier.RunAnalyzersAsync(_script.GetCompilation());
}
