using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Defines a response to interactions such as a <code>view_submission</code> event. Your app has three seconds
/// to respond.
/// </summary>
/// <param name="Action">The type of action, such as <code>update</code> to update a view, <code>push</code>,
/// to push a view, <code>clear</code> to close all views, or <code>errors</code> to report validation errors.</param>
public abstract record ResponseAction(
    [property: JsonProperty("response_action")]
    [property: JsonPropertyName("response_action")]
    string Action);

/// <summary>
/// If the submission contains validation errors, this contains a dictionary of <code>block_id</code>s that identify
/// the blocks in error and an error message.
/// </summary>
/// <param name="Errors"></param>
public record ErrorResponseAction(
    [property: JsonProperty("errors")]
    [property: JsonPropertyName("errors")]
    IReadOnlyDictionary<string, string> Errors) : ResponseAction("errors")
{
    /// <summary>
    /// Appends the provided errors to the current error response.
    /// </summary>
    /// <param name="errors">The errors to add.</param>
    public ErrorResponseAction Append(IReadOnlyDictionary<string, string> errors)
    {
        var merged = new Dictionary<string, string>(Errors);
        foreach (var (key, messageToAdd) in errors)
        {
            var errorMessage = Errors.TryGetValue(key, out var existingError)
                ? $"{existingError} {messageToAdd}"
                : messageToAdd;
            merged[key] = errorMessage;
        }

        return new ErrorResponseAction(merged);
    }
}

/// <summary>
/// A <see cref="ResponseAction"/> that closes all open views.
/// </summary>
public record ClearResponseAction() : ResponseAction("clear");

/// <summary>
/// A <see cref="ResponseAction"/> that updates the currently-open view with the provided content.
/// </summary>
/// <param name="View">The <see cref="View"/> to replace the currently-open view with.</param>
public record UpdateResponseAction(
    [property: JsonProperty("view")]
    [property: JsonPropertyName("view")]
    ViewUpdatePayload View) : ResponseAction("update");
