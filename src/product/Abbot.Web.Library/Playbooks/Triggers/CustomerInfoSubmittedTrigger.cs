using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Serious.Abbot.Playbooks.Triggers;

/// <summary>
/// A trigger that runs when customer info is submitted to Abbot via a POST or PUT request.
/// </summary>
/// <remarks>
/// Initially, this supports both JSON and an HTML form post.
/// </remarks>
public class CustomerInfoSubmittedTrigger : ITriggerType
{
    public const string Id = "http.webhook.customer";

    public StepType Type { get; } = new(Id, StepKind.Trigger)
    {
        Category = "http",
        Outputs =
        {
            new("customer", "Customer", PropertyType.Customer)
            {
                Description = "The customer submitted via the webhook or form post"
            },
        },
        Presentation = new StepPresentation
        {
            Label = "Customer Info Submitted",
            Icon = "fa-globe",
            Description = "Runs when customer information is submitted via a POST request to a URL (as JSON or a form post)",
        },
    };
}


/// <summary>
/// The payload for the <see cref="CustomerInfoSubmittedTrigger"/>.
/// </summary>
public record CustomerInfoSubmittedTriggerPayload
{
    /// <summary>
    /// Information about the customer.
    /// </summary>
    [JsonPropertyName("customer")]
    public required SubmittedCustomerInfo Customer { get; init; }

    /// <summary>
    /// Creates a <see cref="CustomerInfoSubmittedTriggerPayload"/> from a form post.
    /// </summary>
    /// <param name="form">The submitted form.</param>
    /// <returns>A <see cref="CustomerInfoSubmittedTriggerPayload"/></returns>
    public static CustomerInfoSubmittedTriggerPayload FromForm(Dictionary<string, string[]> form)
    {
        return new()
        {
            Customer = SubmittedCustomerInfo.FromForm(form),
        };
    }
}

/// <summary>
/// The information about the customer that was submitted.
/// </summary>
public record SubmittedCustomerInfo
{
    /// <summary>
    /// The customer name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The contact email for the customer.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// The customer segments the customer should be added to.
    /// </summary>
    [JsonPropertyName("segments")]
    public required IReadOnlyList<string> Segments { get; init; }

    /// <summary>
    /// Creates a <see cref="SubmittedCustomerInfo"/> from a form post.
    /// </summary>
    /// <param name="form">The submitted form.</param>
    /// <returns>A <see cref="SubmittedCustomerInfo"/></returns>
    public static SubmittedCustomerInfo FromForm(Dictionary<string, string[]> form)
    {
        return new()
        {
            Name = form.TryGetValue("customer.name", out var name) ? name.FirstOrDefault() ?? "" : "",
            Email = form.TryGetValue("customer.email", out var email) ? email.FirstOrDefault() ?? "" : "",
            Segments = form.TryGetValue("customer.segments", out var segments) ? segments : Array.Empty<string>(),
        };
    }
}


