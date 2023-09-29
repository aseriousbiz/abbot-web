using System.IO;
using System.Threading.Tasks;

namespace Serious.Abbot.Compilation;

/// <summary>
/// The compilation for a skill code as a CSharp Script.
/// </summary>
public interface ISkillCompilation
{
    /// <summary>
    /// This is a computed hash of the code. This is the cache key for the assembly cache.
    /// </summary>
    /// <remarks>
    /// For Azure File Share, this ends up being the file name.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Emits the skill compilation to two streams, the assembly stream and the symbols stream..
    /// </summary>
    /// <param name="assemblyStream">The assembly stream</param>
    /// <param name="symbolsStream">The symbols stream</param>
    Task EmitAsync(Stream assemblyStream, Stream symbolsStream);
}
