using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Serious.AspNetCore.DataAnnotations;

/// <summary>
/// Properties annotated with this attribute are required if the property of the model specified by
/// <see cref="DependentValidationAttribute.PropertyName"/> has value specified by <see cref="Value"/>. If <see cref="Value"/> is null, then the
/// property annotated by this attribute are required if the property specified by <see cref="DependentValidationAttribute.PropertyName"/> is not
/// null.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class RequiredIfAttribute : DependentValidationAttribute, IClientModelValidator
{
    /// <summary>
    /// The constructor.
    /// </summary>
    /// <param name="propertyName">
    /// The name of the property used to determine if the property this attribute is applied to
    /// should be required.
    /// </param>
    /// <param name="value">
    /// The property specified by <see cref="DependentValidationAttribute.PropertyName"/> must equal <see cref="Value"/>
    /// in order for the property this attribute is applied to should be required.
    /// </param>
    public RequiredIfAttribute(string propertyName, object? value) : base(propertyName)
    {
        Value = value;
    }

    /// <summary>
    /// The constructor.
    /// </summary>
    /// <param name="propertyName">
    /// The name of the property used to determine if the property this attribute is applied to
    /// should be required.
    /// </param>
    public RequiredIfAttribute(string propertyName) : this(propertyName, null)
    {
    }

    /// <summary>
    /// If set, then the property specified by <see cref="DependentValidationAttribute.PropertyName"/> must equal <see cref="Value"/>
    /// in order for the property this attribute is applied to should be required.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public object? Value { get; }

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
        // If the value is not null and not an empty string, then it's successful.
        if (value is not null or string { Length: > 0 })
        {
            return ValidationResult.Success;
        }

        // At this point, this value is null or empty, so we need to check the other property.

        // Get other property value.
        var otherProperty = GetDependentPropertyInfo(validationContext);

        var isRequired = Value is null
            ? otherProperty.Value is not null
            : Value.Equals(otherProperty.Value);

        return isRequired
            ? new ValidationResult($"{validationContext.DisplayName} is required.", new[] { validationContext.MemberName! })
            : ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-requiredif", $"{context.ModelMetadata.DisplayName ?? context.ModelMetadata.Name} is required.");
        MergeAttribute(context.Attributes, "data-val-requiredif-dependson", PropertyName);
        if (Value is not null)
        {
            MergeAttribute(context.Attributes, "data-val-requiredif-value", Value.ToString() ?? "");
        }
    }
}
