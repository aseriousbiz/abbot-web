using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Serious;

public static class ResourceExtensions
{
    /// <summary>
    /// Reads the embedded resource as a string. If the assembly is not supplied, assumes the assembly calling this method.
    /// </summary>
    /// <param name="assembly">The assembly to load the embedded resource from. Usually a call to <see cref="Assembly.GetExecutingAssembly"/> or `typeof(SomeTypeInTheAssembly).Assembly`.</param>
    /// <param name="namespace">The namespace of the resource. If not set, it uses the name of the calling assembly.</param>
    /// <param name="path">The path to the resource in the project.
    /// Resources are named using the default namespace of the project + the relative path in the project.
    /// </param>
    /// <param name="filename">The name of the resource on disk, or (if it was customized in the project file) the custom name of the resource.</param>
    /// <returns>A task containing the string of the resource.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the embedded resource is not found.</exception>
    public static async Task<string> ReadResourceAsync(this Assembly assembly, string? @namespace, string? path, string filename)
    {
        @namespace = string.IsNullOrEmpty(@namespace)
            ? assembly.GetName().Name
            : @namespace;
        path = string.IsNullOrEmpty(path)
            ? "."
            : path.Replace('\\', '.').Replace('/', '.') + ".";

        var resourceName = $"{@namespace}.{path}{filename}";

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new FileNotFoundException($"Missing embedded resource: {resourceName}");
        }

        // this will take care of closing the underlying stream
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Reads the embedded resource as a string. If the assembly is not supplied, assumes the assembly calling this method.
    /// </summary>
    /// <param name="assembly">The assembly to load the embedded resource from. Usually a call to <see cref="Assembly.GetExecutingAssembly"/> or `typeof(SomeTypeInTheAssembly).Assembly`.</param>
    /// <param name="path">The relative path to the resource, from the root of the project that compiled it, in either windows or linux format.
    /// For example, `Templates/Specialized` or `Serialization` or `My/Path`</param>
    /// <param name="filename">The resource filename.</param>
    /// <returns>A task containing the string of the resource.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the embedded resource is not found.</exception>
    public static Task<string> ReadResourceAsync(this Assembly assembly, string path, string filename)
        => ReadResourceAsync(assembly,
            null,
            path,
            filename);

    /// <summary>
    /// Reads the embedded resource as a string.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> in the same <see cref="Assembly"/> and namespace as the resource.</param>
    /// <param name="resourceName">The name of the resource within that namespace.</param>
    /// <returns>A task containing the string of the resource.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the embedded resource is not found.</exception>
    public static async Task<string> ReadResourceAsync(this Type type, string resourceName)
    {
        var stream = type.Assembly.GetManifestResourceStream(type, resourceName)
            ?? throw new FileNotFoundException($"Missing embedded resource: {type.Namespace}.{resourceName}");

        // this will take care of closing the underlying stream
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Reads the embedded resource as a string using the <paramref name="type"/> to get the assembly and namespace.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> in the same <see cref="Assembly"/> and namespace as the resource.</param>
    /// <param name="resourceName">The name of the resource within that namespace.</param>
    /// <returns>A task containing the string of the resource.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the embedded resource is not found.</exception>
    public static async Task<string> ReadAssemblyResourceAsync(this Type type, string resourceName)
    {
        var assemblyName = type.Assembly.GetName().Name.Require();
        if (!resourceName.StartsWith(assemblyName, StringComparison.Ordinal))
        {
            resourceName = $"{assemblyName}.{resourceName}";
        }

        var stream = type.Assembly.GetManifestResourceStream(type, resourceName)
                     ?? throw new FileNotFoundException($"Missing embedded resource: {resourceName}");

        // this will take care of closing the underlying stream
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
