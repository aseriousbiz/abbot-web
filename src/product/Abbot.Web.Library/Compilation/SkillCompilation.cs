using System.IO;
using System.Threading.Tasks;

namespace Serious.Abbot.Compilation;

public class SkillCompilation : ISkillCompilation
{
    readonly SkillScript _script;

    public SkillCompilation(string name, SkillScript script)
    {
        Name = name;
        _script = script;
    }


    public string Name { get; }

    public async Task EmitAsync(Stream assemblyStream, Stream symbolsStream)
    {
        await _script.GetCompiledStreamAsync(assemblyStream, symbolsStream);
    }
}
