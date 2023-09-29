using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Compilation;

public abstract class SkillScript
{
    public string Code { get; }
    public CodeLanguage Language { get; }

    protected SkillScript(CodeLanguage language, string code)
    {
        Language = language;
        Code = code;
    }

    public abstract Task GetCompiledStreamAsync(Stream assemblyStream, Stream? symbolsStream);
    public abstract ImmutableArray<ICompilationError> Compile();
    public abstract Task<ImmutableArray<ICompilationError>> AnalyzeAsync(IScriptVerifier verifier);

}
