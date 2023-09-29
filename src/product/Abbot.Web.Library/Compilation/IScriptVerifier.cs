using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Serious.Abbot.Compilation;

public interface IScriptVerifier
{
    Task<ImmutableArray<ICompilationError>> RunAnalyzersAsync(Microsoft.CodeAnalysis.Compilation compilation);
}
