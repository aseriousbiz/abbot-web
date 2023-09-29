using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Abbot.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Compilation;

public class SkillCompiler : ISkillCompiler
{
    static readonly ILogger<SkillCompiler> Log = ApplicationLoggerFactory.CreateLogger<SkillCompiler>();

    readonly IScriptVerifier _scriptVerifier;

    public SkillCompiler(IScriptVerifier scriptVerifier)
    {
        _scriptVerifier = scriptVerifier;
    }

    public async Task<ICompilationResult> CompileAsync(CodeLanguage language, string code)
    {
        IImmutableList<ICompilationError> errors;
        SkillScript script;

        var cacheKey = ComputeCacheKey(code);

        if (language == CodeLanguage.CSharp)
        {
            var options = CreateScriptOptions();
            using var activity = AbbotTelemetry.ActivitySource.StartActivity($"{nameof(SkillCompiler)}:CreateScript");
            script = new DotNetScript(CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals),
                options: options));
        }
        else // ink
        {
            script = new InkScript(code);
        }

        try
        {
            ImmutableArray<ICompilationError> compilation;
            using (AbbotTelemetry.ActivitySource.StartActivity($"{nameof(SkillCompiler)}:CompileScript"))
            {
                compilation = script.Compile();
            }

            var diagnostics = await script.AnalyzeAsync(_scriptVerifier);
            errors = compilation
                .Where(d => !DiagnosticIsIgnored(d))
                .Union(diagnostics.Where(d => !DiagnosticIsIgnored(d)))
                .ToImmutableList();
        }
        catch (Exception e)
        {
            Log.ExceptionCompilingCode(e, cacheKey, script.Code);
            errors = new[] { CompilationError.Create("The code crashed the compiler.") }
                .ToImmutableList();
        }

        return new SkillCompilationResult(new SkillCompilation(cacheKey, script), errors);
    }

    static readonly HashSet<string> IgnoredWarnings = new HashSet<string>()
    {
        // These are ignored _automatically_ by the .NET SDK.
        // They represent checks that only apply to .NET Framework and Binding Redirects.
        // See:
        // * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1701
        // * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1702
        // * https://github.com/dotnet/roslyn/issues/19640
        // * https://github.com/dotnet/sdk/blob/5f64de40b65f5e1154b3309339563335d8b15d3e/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.CSharp.props#L15
        "CS1701",
        "CS1702",
    };

    static bool DiagnosticIsIgnored(ICompilationError compilationError)
    {
        // For now, we treat warnings as errors. We could make this configurable in the future.
        return IgnoredWarnings.Contains(compilationError.ErrorId);
    }

    public static ScriptOptions CreateScriptOptions()
    {
        var nameSpaces = AbbotScriptOptions.NameSpaces;
        var references = AbbotScriptOptions.GetSkillCompilerAssemblyReferences();

        return ScriptOptions.Default
            .WithLanguageVersion(AbbotScriptOptions.LanguageVersion)
            .WithImports(nameSpaces)
            .WithEmitDebugInformation(true)
            .WithReferences(references)
            .WithAllowUnsafe(false);
    }

    public static string ComputeCacheKey(string code)
    {
        return code.ComputeHMACSHA256FileName(WebConstants.CodeCacheKeyHashSeed);
    }
}
