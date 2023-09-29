using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Serious.AspNetCore.DataAnnotations;

/// <summary>
/// Properties annotated with this attribute must have a value greater than the property of the model specified by
/// <see cref="DependentValidationAttribute.PropertyName"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class GreaterThanAttribute : DependentValidationAttribute, IClientModelValidator
{
    /// <summary>
    /// The constructor.
    /// </summary>
    /// <param name="propertyName">The name of the property that the property this attribute is applied to must be greater than.</param>
    public GreaterThanAttribute(string propertyName) : base(propertyName)
    {
    }

    /// <summary>
    ///     Validates a specified value with respect to the associated validation attribute.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>
    ///     An instance of the <see cref="System.ComponentModel.DataAnnotations.ValidationResult" /> class.
    /// </returns>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException"></exception>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If the value is null or empty, we let the required attribute handle things.
        if (value is null or string { Length: 0 })
        {
            return ValidationResult.Success;
        }

        var otherProperty = GetDependentPropertyInfo(validationContext);
        if (otherProperty.Value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not IComparable comparable)
        {
            throw new InvalidOperationException($"{validationContext.DisplayName} and {otherProperty.DisplayName} must implement IComparable.");
        }

        return comparable.CompareTo(otherProperty.Value) > 0
            ? ValidationResult.Success
            : new ValidationResult($"{validationContext.DisplayName} must be greater than {otherProperty.DisplayName}.", new[] { validationContext.MemberName!, PropertyName });
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        var modelType = context.ModelMetadata.ContainerType;
        var otherProperty = modelType.GetProperty(PropertyName)
            ?? throw new InvalidOperationException($"Property {PropertyName} does not exist on {modelType}");

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-greaterthan", $"{context.ModelMetadata.DisplayName} must be greater than {GetDisplayName(otherProperty)}");
        MergeAttribute(context.Attributes, "data-val-greaterthan-dependson", PropertyName);

        var javaScriptType = context.ModelMetadata.ModelType.GetJavaScriptType();
        if (!string.Equals(javaScriptType, "Number", StringComparison.Ordinal))
        {
            // TODO: At some point we can add support for Date and TimeSpan.
            throw new InvalidOperationException($"Client validation not supported for type {context.ModelMetadata.ModelType.Name}.");
        }
    }
}
