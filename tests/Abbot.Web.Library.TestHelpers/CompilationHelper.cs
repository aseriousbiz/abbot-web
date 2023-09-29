using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serious.Abbot.Compilation;

namespace Abbot.Common.TestHelpers
{
    public static class ScriptHelper
    {
        public class CollectibleAssemblyLoadContext : AssemblyLoadContext
        {
            public CollectibleAssemblyLoadContext() : base(isCollectible: true)
            {
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                return null;
            }
        }

        public static SkillScript CreateScript<TScriptGlobals>(string code)
        {
            var options = ScriptOptions.Default
                .WithImports("System")
                .WithEmitDebugInformation(true);
            var script = new DotNetScript(CSharpScript.Create<object>(code, options, typeof(TScriptGlobals)));
            script.Compile();
            return script;
        }

        public static async Task<string?> InvokeAssemblyAsync(
            Stream assemblyStream,
            Stream symbolsStream,
            object? scriptGlobals = null)
        {
            assemblyStream.Position = 0;
            symbolsStream.Position = 0;
            var assemblyLoadContext = new CollectibleAssemblyLoadContext();
            var assembly = assemblyLoadContext.LoadFromStream(assemblyStream, symbolsStream);
            var type = assembly.GetType("Submission#0");
            var method = type!.GetMethod("<Factory>");
            var parameters = new object[] { new[] { scriptGlobals ?? new object(), null } };
            var task = method!.Invoke(null, parameters) as Task<object>;
            var result = await task!;
            return result as string;
        }
    }
}
