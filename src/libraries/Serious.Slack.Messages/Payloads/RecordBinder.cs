using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Serious.Slack.Converters;

namespace Serious.Payloads;

/// <summary>
/// A helper class used to bind payloads to a simple record object.
/// </summary>
public class RecordBinder
{
    readonly Dictionary<Type, Binding> _bindings = new();

    record Binding(ConstructorInfo ConstructorInfo, IReadOnlyList<BindAttribute?> BindAttributes);

    /// <summary>
    /// Instantiates a new instance of the specified <typeparamref name="TActionsState"/> type and populates it
    /// by examining the <see cref="BindAttribute" /> applied to the properties of the object.
    /// </summary>
    /// <remarks>
    /// We don't enforce that the type is a record, but we do make the assumption that the properties and constructor
    /// arguments line up exactly. We can make this smarter in the future.
    /// </remarks>
    /// <typeparam name="TActionsState">The record type with a single constructor to bind to.</typeparam>
    /// <returns><c>true</c> if the specified type can be populated from value source.</returns>
    public bool TryBindRecord<TActionsState>(
        Func<BindAttribute?, object?> valueSource,
        [NotNullWhen(true)] out TActionsState? bound)
        where TActionsState : class
    {
        var bindingType = typeof(TActionsState);

        if (!_bindings.TryGetValue(bindingType, out var bindings))
        {
            bindings = CreateBindings<TActionsState>(bindingType);
            _bindings.Add(bindingType, bindings);
        }

        var values = bindings
            .BindAttributes
            .Select(valueSource)
            .ToArray();

        if (bindings.ConstructorInfo.GetParameters().Length != values.Length)
        {
            bound = null;
            return false;
        }

        bound = bindings.ConstructorInfo.Invoke(values) as TActionsState;
        return bound is not null;
    }

    static Binding CreateBindings<TActionsState>(Type bindingType) where TActionsState : class
    {
        var constructor = typeof(TActionsState).GetConstructors().Single();

        var bindAttributes = bindingType
            .GetProperties()
            .Select(GetBindAttribute)
            .ToList();
        return new Binding(constructor, bindAttributes);
    }

    static BindAttribute GetBindAttribute(PropertyInfo property)
    {
        var bindAttribute = property.GetCustomAttribute<BindAttribute>();
        if (bindAttribute is null)
        {
            bindAttribute = new BindAttribute(property.Name, property.Name);
        }
        else if (bindAttribute.ActionId is null)
        {
            bindAttribute = new BindAttribute(bindAttribute.BlockId, property.Name);
        }

        return bindAttribute;
    }
}
