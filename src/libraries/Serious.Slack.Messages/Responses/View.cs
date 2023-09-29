using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;
using Serious.Slack.Payloads;

namespace Serious.Slack;

/// <summary>
/// Information about a modal view retrieved from the Slack API or as part of a
/// <see cref="BlockActionsPayload"/>. This is not used to create or publish views.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/surfaces/views"/> for more information.
/// </remarks>
[Element("modal")]
public record ModalView() : View("modal");

/// <summary>
/// Information about an App Home view retrieved from the Slack API or as part of a
/// <see cref="BlockActionsPayload"/>. This is not used to create or publish views.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/surfaces/views"/> for more information.
/// </remarks>
[Element("home")]
public record AppHomeView() : View("home");

/// <summary>
/// Information about a view retrieved from the Slack API or as part of a
/// <see cref="BlockActionsPayload"/>. This is not used to create or publish views.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/surfaces/views"/> for more information.
/// </remarks>
public abstract record View(string Type) : ViewUpdatePayload(Type)
{
    /// <summary>
    /// The view Id. Ex. VMHU10V25
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    /// <summary>
    /// The Id of the Slack team. Ex. T8N4K1JN
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string TeamId { get; init; } = null!;

    /// <summary>
    /// A unique value which is optionally accepted in <c>views.update</c> and <c>views.publish</c> API
    /// calls. When provided to those APIs, the <c>hash</c> is validated such that only the most recent
    /// view can be updated. This should be used to ensure the correct view is being updated when updates
    /// are happening asynchronously.
    /// </summary>
    [JsonProperty("hash")]
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }

    /// <summary>
    /// If this view is stacked, this property returns the Id of the root view in the stack.
    /// </summary>
    [JsonProperty("root_view_id")]
    [JsonPropertyName("root_view_id")]
    public string? RootViewId { get; init; }

    /// <summary>
    /// If this view is stacked, this property returns the Id of the previous view in the stack.
    /// </summary>
    [JsonProperty("previous_view_id")]
    [JsonPropertyName("previous_view_id")]
    public string? PreviousViewId { get; init; }

    /// <summary>
    /// The Id of the App.
    /// </summary>
    [JsonProperty("app_id")]
    [JsonPropertyName("app_id")]
    public string AppId { get; init; } = null!;

    /// <summary>
    /// Id of the bot associated with this view.
    /// </summary>
    [JsonProperty("bot_id")]
    [JsonPropertyName("bot_id")]
    public string? BotId { get; init; }

    /// <summary>
    /// When a view contains stateful interactive components, this contains the state of those components.
    /// </summary>
    [JsonProperty("state")]
    [JsonPropertyName("state")]
    public BlockActionsState? State { get; init; }

    /// <summary>
    /// NOT officially documented.
    /// Appears to be present in <c>view_submission</c> payloads and indicates the ID of the team in which the app that _generated_ the original view is installed.
    /// </summary>
    /// <remarks>
    /// Saw this in the payload.
    /// </remarks>
    [JsonProperty("app_installed_team_id")]
    [JsonPropertyName("app_installed_team_id")]
    public string? AppInstalledTeamId { get; init; }
}
