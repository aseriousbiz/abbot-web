using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Abbot.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serious.Abbot.Compilation;
using Serious.Abbot.Execution;
using Serious.Runtime;
using Xunit.Abstractions;

namespace Serious.TestHelpers
{
    public static class TestSkillCompiler
    {
        const string InkHeader = @"";

        public static async Task<(Stream, Stream)> CompileCodeToStreamsAsync<TScriptGlobals>(string code)
        {
            var script = CreateScript<TScriptGlobals>(code);
            var assemblyStream = new MemoryStream();
            var symbolsStream = new MemoryStream();
            script.GetCompilation().Emit(assemblyStream, symbolsStream);
            await assemblyStream.FlushAsync();
            await symbolsStream.FlushAsync();
            assemblyStream.Position = 0;
            symbolsStream.Position = 0;

            return (assemblyStream, symbolsStream);
        }

        public static Script<object> CreateScript<TScriptGlobals>(string code)
        {
            var options = ScriptOptions.Default
                .WithImports("System")
                .WithEmitDebugInformation(true);
            var script = CSharpScript.Create<object>(code, options, typeof(TScriptGlobals));
            script.Compile();
            return script;
        }

        public static ICompiledSkill CompileAsync(string code) =>
            new CSharpAssembly(CompileCSharp(code));

        public static Assembly CompileCSharp(string code)
        {
            var options = CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, options, typeof(ScriptGlobals));
            var assemblyStream = new MemoryStream();
            var symbolsStream = new MemoryStream();
            var result = script.GetCompilation().Emit(assemblyStream, symbolsStream);
            if (result.Diagnostics.Length > 0)
            {
                throw new CompilationErrorException(string.Join(Environment.NewLine, result.Diagnostics), result.Diagnostics);
            }
            var context = new CollectibleAssemblyLoadContext();
            assemblyStream.Position = 0;
            symbolsStream.Position = 0;
            return context.LoadFromStream(assemblyStream, symbolsStream);
        }

        public static async Task<ICompiledSkill> CompileInkAsync(string name, string code, ITestOutputHelper output)
        {
            var script = new InkScript(code);
            var errors = script.Compile();
            if (errors.Length > 0)
            {
                var errorList = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
                throw new InvalidOperationException($"Compilation failed. Errors: {Environment.NewLine}{errorList}");
            }

            var jsonStream = new MemoryStream();
            await script.GetCompiledStreamAsync(jsonStream, null);
            var json = Encoding.UTF8.GetString(jsonStream.ToArray());

            return new CompiledInkScript(name, json);
        }

        static ScriptOptions CreateScriptOptions()
        {
            var nameSpaces = AbbotScriptOptions.NameSpaces;
            var references = AbbotScriptOptions.GetSkillCompilerAssemblyReferences();

            return ScriptOptions.Default
                .WithImports(nameSpaces)
                .WithEmitDebugInformation(true)
                .WithReferences(references)
                .WithAllowUnsafe(false);
        }
    }
}
