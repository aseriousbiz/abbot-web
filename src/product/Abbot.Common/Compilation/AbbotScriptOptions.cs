using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp.RuntimeBinder;
using Serious.Abbot;
using Serious.Abbot.Scripting;

namespace Abbot.Scripting;

public static class AbbotScriptOptions
{
    static readonly Lazy<IReadOnlyList<AssemblyName>> SkillEditorAssemblyNames = new(ComputeSkillEditorAssemblyNames);

    public static readonly LanguageVersion LanguageVersion = LanguageVersion.Latest;

    static ImmutableHashSet<string> IgnoredAssemblies { get; } = new[]
    {
        "System.Memory",
        "System.IO.UnmanagedMemoryStream",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Runtime.CompilerServices.VisualC"
    }.ToImmutableHashSet();

    public static IEnumerable<string> NameSpaces { get; } =
        new[]
        {
            "System",
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Collections.Generic",
            "System.Collections.Immutable",
            "System.Data",
            "System.Dynamic",
            "System.Globalization",
            "System.Linq",
            "System.Linq.Expressions",
            "System.Net.Http",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Threading.Tasks",
            "Serious.Abbot.Scripting",
            "Serious.Slack.BlockKit",
            "Humanizer",
            "NodaTime",
        };

    // .NET Reference assemblies exposed to skills.
    public static IEnumerable<AssemblyName> ReferenceAssemblyNames { get; } =
        new[]
        {
            "mscorlib",
            "netstandard",
            "System",
            "System.Core",
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Collections.Immutable",
            "System.Collections.Specialized",
            "System.ComponentModel.Annotations",
            "System.ComponentModel.Primitives",
            "System.Data",
            "System.Data.Common",
            "System.Globalization",
            "System.Globalization.Calendars",
            "System.Globalization.Extensions",
            "System.Linq",
            "System.Linq.Expressions",
            "System.Linq.Parallel",
            "System.Net",
            "System.Net.Http",
            "System.Net.Primitives",
            "System.Runtime",
            "System.Runtime.Extensions",
            "System.Text.Encoding",
            "System.Text.Encodings.Web",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Threading.Tasks.Parallel",
            "System.Xml.ReaderWriter",
            "System.Web.HttpUtility"
        }.Select(name => new AssemblyName(name));

    // Packaged assemblies exposed to skills that aren't directly referenced by IScriptGlobals.
    // These need to be referenced by both Abbot.Functions.DotNet and Abbot.Web.
    static IEnumerable<AssemblyName> GetAdditionalAssemblies()
        => ScriptAssemblies.Assemblies;

    // Get the assemblies needed for the skill editor
    public static IEnumerable<AssemblyName> GetSkillEditorAssemblyNames() => SkillEditorAssemblyNames.Value;

    // Get the assemblies needed for the running skills
    public static IEnumerable<Assembly> GetSkillCompilerAssemblyReferences()
    {
        return GetSkillEditorAssemblyNames()
            .Select(Assembly.Load)
            .Append(typeof(CSharpArgumentInfo).Assembly);
    }

    static IReadOnlyList<AssemblyName> ComputeSkillEditorAssemblyNames()
    {
        return GetScriptGlobalsReferencedAssemblyNames()
            .Concat(GetAdditionalAssemblies())
            .DistinctBy(x => x.Name)
            .ToList();
    }

    static IEnumerable<AssemblyName> GetScriptGlobalsReferencedAssemblyNames()
    {
        var assembly = typeof(IScriptGlobals).Assembly;
        yield return assembly.GetName();
        foreach (var name in assembly.GetReferencedAssemblies())
        {
            var simpleName = name.Name;
            if (simpleName is null || IgnoredAssemblies.Contains(simpleName))
            {
                continue;
            }

            yield return name;
        }
    }
}
