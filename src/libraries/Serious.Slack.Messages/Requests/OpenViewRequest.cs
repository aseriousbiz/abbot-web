using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Body of the request to create a new view in Slack via <c>views.open</c> or <c>views.push</c>.
/// </summary>
/// <remarks>
/// <see href="https://api.slack.com/methods/views.open"/> and <see href="https://api.slack.com/methods/views.push"/>
/// for more info.
/// </remarks>
/// <param name="TriggerId">An Id received when the user interacts with a UI component. This is required to create a view.</param>
/// <param name="View">The view payload.</param>
public record OpenViewRequest(
    [property:JsonProperty("trigger_id")]
    [property:JsonPropertyName("trigger_id")]
    string TriggerId,

    [property:JsonProperty("view")]
    [property:JsonPropertyName("view")]
    ViewUpdatePayload View);

/// <summary>
/// Body of the request to update an existing view in Slack via <c>views.update</c>. The view is identified by setting
/// <see cref="ViewId"/> (<c>view_id</c>) or <see cref="ExternalId"/> (<c>external_id</c>).
/// </summary>
/// <remarks>
/// <see href="https://api.slack.com/methods/views.open"/> and <see href="https://api.slack.com/methods/views.push"/>
/// for more info.
/// </remarks>
/// <param name="ViewId">The <c>view_id</c> of the view to update.</param>
/// <param name="ExternalId">The <c>external_id</c> of the view to update.</param>
/// <param name="View">The view payload.</param>
public record UpdateViewRequest(
    [property:JsonProperty("view_id")]
    [property:JsonPropertyName("view_id")]
    string? ViewId,

    [property:JsonProperty("external_id")]
    [property:JsonPropertyName("external_id")]
    string? ExternalId,

    [property:JsonProperty("view")]
    [property:JsonPropertyName("view")]
    ViewUpdatePayload View);

/// <summary>
/// Body of the request to update the App Home view.
/// </summary>
/// <param name="UserId">The Id of the user to publish the view to.</param>
/// <param name="View">The view payload.</param>
/// <remarks>
/// See <see href="https://api.slack.com/methods/views.publish" /> for more info.
/// </remarks>
public record PublishAppHomeRequest(
    [property:JsonProperty("user_id")]
    [property:JsonPropertyName("user_id")]
    string UserId,

    [property:JsonProperty("view")]
    [property:JsonPropertyName("view")]
    AppHomeView View);
