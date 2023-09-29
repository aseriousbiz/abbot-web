using System;

namespace Serious.Slack.Converters;

/// <summary>
/// Indicates that the attributed class is the deserialization target for the specified slack element <c>type</c> or
/// types.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
sealed class ElementAttribute : Attribute
{
    /// <summary>
    /// Constructs an <see cref="ElementAttribute" /> with the JSON <c>type</c> for the Slack element that the
    /// the attributed type can be deserialized to.
    /// </summary>
    /// <param name="jsonType">The JSON <c>type</c> for the Slack element that the attributed class can be deserialized to.</param>
    public ElementAttribute(string jsonType)
    {
        JsonType = jsonType;
    }

    /// <summary>
    /// When a <c>type</c> maps to more than one CLR type, the existence of a property with the name specified by
    /// this property in the JSON payload is used to discriminate between the CLR types.
    /// </summary>
    public string? Discriminator { get; set; }

    /// <summary>
    /// If a <see cref="Discriminator"/> is set, and this property is not <c>null</c>, this is used to map the CLR type
    /// to the JSON payload by comparing the value of the discriminator property in the JSON payload to the value of
    /// this property.
    /// </summary>
    public string? DiscriminatorValue { get; set; }

    /// <summary>
    /// The JSON <c>type</c> for the Slack element that the attributed class can be deserialized to.
    /// </summary>
    public string JsonType { get; }
}
