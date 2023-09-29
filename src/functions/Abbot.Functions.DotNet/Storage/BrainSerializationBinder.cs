using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Abbot.Functions.Tests")]

namespace Serious.Abbot.Storage;

/// <summary>
/// Serialization for Abbot skill storage. This adds support for the fact that when a skill changes, the
/// assembly name changes. So we need to massage things a bit to make it all work.
/// This is now mostly a helper class that parses type information that's stored in json by Newtonsoft.
/// Everything in here is lifted from the DefaultSerializationBinder class, which has very useful
/// methods and types that are unfortunately not accessible.
/// </summary>
public class BrainSerializationBinder
{
    readonly string _skillAssemblyName;
    readonly ThreadSafeStore<ValueTuple<string?, string>, Type> _typeCache;

    /// <summary>
    /// Constructs the binder
    /// </summary>
    public BrainSerializationBinder(string skillAssemblyName)
    {
        _skillAssemblyName = skillAssemblyName;
        _typeCache = new ThreadSafeStore<ValueTuple<string?, string>, Type>(GetTypeFromTypeNameKey);
    }

    public Type GetTypeByName(string typename, string? asmname = null) => _typeCache.Get(new ValueTuple<string?, string>(asmname, typename));

    Type GetTypeFromTypeNameKey(ValueTuple<string?, string> typeNameKey)
    {
        string? assemblyName = typeNameKey.Item1;
        string typeName = typeNameKey.Item2;
        if (assemblyName is null)
        {
            return Type.GetType(typeName)
                   ?? throw new JsonSerializationException($"Could not find type '{typeName}'.");
        }

        var isUserAssembly = assemblyName.StartsWith(BrainSerializer.RoslynScriptCompiledAssemblyPrefix, StringComparison.Ordinal);

        Assembly? assembly = null;

        // no point in trying to load the skill code user assembly from disk, it's dynamic
        if (!isUserAssembly)
        {
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch (FileNotFoundException)
            { }
        }

        if (assembly is null)
        {
            // find assemblies loaded dynamically or not from the local directory
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in loadedAssemblies)
            {
                // if the type we're looking for is in the skill code assembly, track it down in the loaded assemblies
                if (isUserAssembly)
                {
                    if (a.FullName?.StartsWith(_skillAssemblyName, StringComparison.Ordinal) ?? false)
                    {
                        assembly = a;
                        break;
                    }
                }
                else
                {
                    // check for both full name or partial name match
                    if (a.FullName == assemblyName || a.GetName().Name == assemblyName)
                    {
                        assembly = a;
                        break;
                    }
                }
            }
        }

        if (assembly is null)
        {
            throw new JsonSerializationException($"Could not load assembly '{assemblyName}'.");
        }

        // if generic type, try manually parsing the type arguments for the case of dynamically loaded assemblies
        // example generic typeName format: System.Collections.Generic.Dictionary`2[[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]
        try
        {
            // try the straight type name
            return assembly.GetType(typeName)
                   // if not found, see if it's a generic type
                   ?? GetGenericTypeFromTypeName(typeName, assembly)
                   // nothing found, throw
                   ?? throw new JsonSerializationException(
                       $"Could not find type '{typeName}' in assembly '{assembly.FullName}'.");
        }
        catch (JsonSerializationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonSerializationException($"Could not find type '{typeName}' in assembly '{assembly.FullName}'.", ex);
        }
    }

    /// <summary>
    /// CREDIT: https://github.com/JamesNK/Newtonsoft.Json/blob/52190a3a3de6ef9a556583cbcb2381073e7197bc/Src/Newtonsoft.Json/Serialization/DefaultSerializationBinder.cs#L130
    /// </summary>
    /// <param name="typeName"></param>
    /// <param name="assembly"></param>
    /// <returns></returns>
    Type? GetGenericTypeFromTypeName(ReadOnlySpan<char> typeName, Assembly assembly)
    {
        if (typeName.IndexOf('`') < 0)
        {
            return null;
        }

        int openBracketIndex = typeName.IndexOf('[');
        if (openBracketIndex < 0)
        {
            return null;
        }

        var genericTypeDef = assembly.GetType(typeName[..openBracketIndex].ToString());
        if (genericTypeDef is null)
        {
            return null;
        }

        List<Type> genericTypeArguments = new List<Type>();
        typeName = typeName[1..];
        int scope = 0;
        int typeArgStartIndex = 0;
        int endIndex = typeName.Length;
        for (int i = 0; i < endIndex; ++i)
        {
            switch (typeName[i])
            {
                case '[':
                    if (scope == 0)
                    {
                        typeArgStartIndex = i + 1;
                    }
                    ++scope;
                    break;
                case ']':
                    --scope;
                    if (scope == 0)
                    {
                        ValueTuple<string?, string> typeNameKey = SplitFullyQualifiedTypeName(typeName[(typeArgStartIndex + 1)..(i - 1)]);
                        genericTypeArguments.Add(GetTypeByName(typeNameKey));
                    }
                    break;
            }
        }
        return genericTypeDef.MakeGenericType(genericTypeArguments.ToArray());
    }

    /// <summary>
    /// From a string like `System.Collections.Generic.Dictionary``2[[System.String, mscorlib,Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089`,
    /// split the outer type name from its assembly name and return both. If there's no assembly name, return the type name
    /// </summary>
    /// <param name="fullyQualifiedTypeName"></param>
    /// <returns>A <see cref="ValueTuple{T1, T2}"/>(<see cref="string"/>?,<see cref="string"/>) where the first item is the assembly name and the second item is the type name.</returns>
    public static ValueTuple<string?, string> SplitFullyQualifiedTypeName(ReadOnlySpan<char> fullyQualifiedTypeName)
    {
        return TryGetAssemblyDelimiterIndex(fullyQualifiedTypeName, out var comma)
            ? (fullyQualifiedTypeName[(comma + 1)..].Trim().ToString(), fullyQualifiedTypeName[0..comma].Trim().ToString())
            : (null, fullyQualifiedTypeName.ToString());
    }

    static bool TryGetAssemblyDelimiterIndex(ReadOnlySpan<char> fullyQualifiedTypeName, out int pos)
    {
        // Adapted from Newtonsoft.Json and optimized by us.
        // CREDIT:  https://github.com/JamesNK/Newtonsoft.Json/blob/52190a3a3de6ef9a556583cbcb2381073e7197bc/Src/Newtonsoft.Json/Utilities/ReflectionUtils.cs#L859
        // we need to get the first comma following all surrounded in brackets in case of generic types
        // e.g. System.Collections.Generic.Dictionary`2[[System.String, mscorlib,Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
        int lastBracket = fullyQualifiedTypeName.LastIndexOf(']');
        lastBracket = lastBracket < 0 ? 0 : lastBracket;
        return (pos = fullyQualifiedTypeName[lastBracket..].IndexOf(',') + lastBracket) > lastBracket;
    }

    Type GetTypeByName(ValueTuple<string?, string> typeNameKey) => _typeCache.Get(typeNameKey);

    class ThreadSafeStore<TKey, TValue> where TKey : notnull
    {
        readonly ConcurrentDictionary<TKey, TValue> _concurrentStore;
        readonly Func<TKey, TValue> _creator;

        public ThreadSafeStore(Func<TKey, TValue> creator)
        {
            _creator = creator;
            _concurrentStore = new ConcurrentDictionary<TKey, TValue>();
        }

        public TValue Get(TKey key) => _concurrentStore.GetOrAdd(key, _creator);
    }
}
