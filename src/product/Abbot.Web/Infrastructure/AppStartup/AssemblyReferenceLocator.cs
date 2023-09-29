using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Abbot.Scripting;
using Microsoft.CodeAnalysis;
using Serious.Abbot.Scripting;
using Serious.Abbot.Web;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

public static class AssemblyReferenceLocator
{
    /// <summary>
    /// Retrieve all assembly references needed by Roslyn for Mirror Sharp.
    /// </summary>
    /// <returns></returns>
    public static ImmutableList<MetadataReference> GetAllAssemblyReferences()
    {
        return GetAllReferences().ToImmutableList();
    }

    static IEnumerable<MetadataReference> GetAllReferences()
    {
        var outputDirectory = Path.GetDirectoryName(typeof(IScriptGlobals).Assembly.Location)!;
        var refsDirectory = Path.Combine(outputDirectory, "refs");
        var referenceAssemblyNames = AbbotScriptOptions
            .ReferenceAssemblyNames
            .Select(asn => asn.Name)
            .ToHashSet();

        foreach (var name in referenceAssemblyNames)
        {
            if (name is null)
                continue;
            var assemblyReference = DocumentedAssembly(refsDirectory, name, outputDirectory);
            if (assemblyReference is not null)
            {
                yield return assemblyReference;
            }
        }

        var runtimeAssemblyNames = AbbotScriptOptions
            .GetSkillEditorAssemblyNames()
            .Where(asn => !referenceAssemblyNames.Contains(asn.Name))
            .Select(asn => asn.Name);

        foreach (var name in runtimeAssemblyNames)
        {
            if (name is null)
                continue;
            var runtimeAssembly = DocumentedAssembly(outputDirectory, name);
            if (runtimeAssembly is not null)
            {
                yield return runtimeAssembly;
            }
        }
    }

    static MetadataReference? DocumentedAssembly(string directory, string assemblyName, string? backupDirectory = null)
    {
        var assemblyPath = Path.Combine(directory, $"{assemblyName}.dll");
        if (!File.Exists(assemblyPath) && backupDirectory is not null)
        {
            assemblyPath = Path.Combine(backupDirectory, $"{assemblyName}.dll");
        }

        var documentationPath = Path.Combine(directory, $"{assemblyName}.xml");

        try
        {
            return File.Exists(assemblyPath) && File.Exists(documentationPath)
                ? MetadataReference.CreateFromFile(
                    assemblyPath,
                    documentation: XmlDocumentationProvider.CreateFromFile(documentationPath))
                : File.Exists(assemblyPath)
                    ? MetadataReference.CreateFromFile(assemblyPath)
                    : null;
        }
        catch (FileNotFoundException e)
        {
            var log = ApplicationLoggerFactory.CreateLogger<Startup>();
            log.ExceptionLoadingAssembly(e, assemblyPath, documentationPath);
            return null;
        }
    }
}
