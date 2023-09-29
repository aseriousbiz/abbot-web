using System.IO;
using System.Threading.Tasks;
using Serious.Abbot;
using Serious.Abbot.Compilation;
using Serious.Cryptography;

namespace Serious.TestHelpers
{
    public class FakeSkillCompilation : ISkillCompilation
    {
        readonly string _assembly;
        readonly string _symbols;

        public FakeSkillCompilation() : this("AssemblyContents", "AssemblySymbolsContent")
        {
        }

        public FakeSkillCompilation(string code) : this("AssemblyContents", "AssemblySymbolsContent")
        {
            Code = code;
            Name = code.ComputeHMACSHA256FileName(WebConstants.CodeCacheKeyHashSeed);
        }

        public FakeSkillCompilation(string assembly, string symbols)
        {
            _assembly = assembly;
            _symbols = symbols;
        }

        public string Name { get; set; } = string.Empty;
        public string? Code { get; }

        public async Task EmitAsync(Stream assemblyStream, Stream symbolsStream)
        {
            var assemblyWriter = new StreamWriter(assemblyStream);
            await assemblyWriter.WriteAsync(_assembly);
            await assemblyWriter.FlushAsync();

            var symbolsWriter = new StreamWriter(symbolsStream);
            await symbolsWriter.WriteAsync(_symbols);
            await symbolsWriter.FlushAsync();

        }
    }
}
