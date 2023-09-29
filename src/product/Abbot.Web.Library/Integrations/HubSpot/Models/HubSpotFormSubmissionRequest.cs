using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot.Models;

/// <summary>
/// The body of a request to send a form submission.
/// <see href="https://legacydocs.hubspot.com/docs/methods/forms/submit_form_v3_authentication"/>
/// </summary>
/// <param name="Fields">A list of form field names and the values.</param>
public record HubSpotFormSubmissionRequest(

    [property: JsonProperty("fields")]
    [property: JsonPropertyName("fields")]
    IReadOnlyList<HubSpotField> Fields);

/// <summary>
/// A name and value pair for a form field.
/// </summary>
/// <param name="Name">The name of the field.</param>
/// <param name="Value">The value of the field.</param>
public record HubSpotField(
    [property: JsonProperty("name")]
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonProperty("value")]
    [property: JsonPropertyName("value")]
    string Value);
