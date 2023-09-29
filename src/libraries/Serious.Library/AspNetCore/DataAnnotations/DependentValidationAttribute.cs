using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Serious.AspNetCore.DataAnnotations;

/// <summary>
/// Base class for validation attributes that are dependent on another property.
/// </summary>
public abstract class DependentValidationAttribute : ValidationAttribute
{
    protected DependentValidationAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }

    /// <summary>
    /// The name of the property this validation attribute is dependent on in some way.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public string PropertyName { get; }

    protected static string GetDisplayName(MemberInfo propertyInfo)
    {
        var attribute = propertyInfo.GetCustomAttribute<DisplayAttribute>();
        return attribute is { Name: { Length: > 0 } name }
            ? name
            : propertyInfo.Name;
    }

    protected DependentPropertyInfo GetDependentPropertyInfo(ValidationContext validationContext)
    {
        var dependentProperty = validationContext.ObjectInstance.GetType().GetProperty(PropertyName)
            ?? throw new InvalidOperationException($"{PropertyName} property not found.");

        var value = dependentProperty.GetValue(validationContext.ObjectInstance);
        var displayName = GetDisplayName(dependentProperty);

        return new DependentPropertyInfo(value, displayName);
    }

    protected static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (attributes.ContainsKey(key))
        {
            return;
        }

        attributes.Add(key, value);
    }

    protected record DependentPropertyInfo(object? Value, string DisplayName);
}
