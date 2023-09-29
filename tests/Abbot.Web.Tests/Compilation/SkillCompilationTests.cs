using System.IO;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Compilation;
using Xunit;

public class SkillCompilationTests
{
    public class TheEmitAsyncMethod
    {
        [Fact]
        public async Task WritesToAssemblyAndSymbolsStream()
        {
            var script = ScriptHelper.CreateScript<object>("return \"test\" + \" success\";");
            var compilation = new SkillCompilation("nomdeplume", script);
            var assemblyStream = new MemoryStream();
            var symbolsStream = new MemoryStream();

            await compilation.EmitAsync(assemblyStream, symbolsStream);

            Assert.Equal("nomdeplume", compilation.Name);
            var result = await ScriptHelper.InvokeAssemblyAsync(assemblyStream, symbolsStream);
            Assert.Equal("test success", result);
        }
    }
}
