using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Manifests;

/// <summary>
/// A group of settings corresponding to the Features section of the app config pages.
/// </summary>
public record ManifestFeatures
{
    /// <inheritdoc cref="ManifestAppHome" />
    [JsonProperty("app_home")]
    [JsonPropertyName("app_home")]
    public ManifestAppHome? AppHome { get; set; }

    /// <inheritdoc cref="ManifestBotUser" />
    [JsonProperty("bot_user")]
    [JsonPropertyName("bot_user")]
    public ManifestBotUser? BotUser { get; set; }

    /// <summary>
    /// An array of settings groups that describe <a href="https://api.slack.com/interactivity/shortcuts">shortcuts</a> configuration.
    /// A maximum of 5 shortcuts can be included in this array.
    /// </summary>
    [JsonProperty("shortcuts")]
    [JsonPropertyName("shortcuts")]
    public List<ManifestShortcut>? Shortcuts { get; set; }

    /// <summary>
    /// An array of settings groups that describe <a href="https://api.slack.com/interactivity/slash-commands">slash commands</a> configuration.
    /// A maximum of 5 slash commands can be included in this array.
    /// </summary>
    [JsonProperty("slash_commands")]
    [JsonPropertyName("slash_commands")]
    public List<ManifestSlashCommand>? SlashCommands { get; set; }

    /// <summary>
    /// An array of strings containing valid <a href="https://api.slack.com/reference/messaging/link-unfurling#configuring_domains">unfurl domains</a> to register.
    /// A maximum of 5 unfurl domains can be included in this array.
    /// </summary>
    [JsonProperty("unfurl_domains")]
    [JsonPropertyName("unfurl_domains")]
    public List<string>? UnfurlDomains { get; set; }

    /// <summary>
    /// An array of settings groups that describe <a href="https://api.slack.com/workflows/steps">workflow steps</a> configuration.
    /// A maximum of 10 workflow steps can be included in this array.
    /// </summary>
    [JsonProperty("workflow_steps")]
    [JsonPropertyName("workflow_steps")]
    public List<ManifestWorkflowStep>? WorkflowSteps { get; set; }
}

/// <summary>
/// A subgroup of settings that describe <a href="https://api.slack.com/surfaces/tabs">App Home</a> configuration.
/// </summary>
/// <param name="HomeTabEnabled">
/// A boolean that specifies whether or not the <a href="https://api.slack.com/surfaces/tabs">Home tab</a> is enabled.
/// </param>
/// <param name="MessagesTabEnabled">
/// A boolean that specifies whether or not the <a href="https://api.slack.com/surfaces/tabs">Messages tab in your App Home</a> is enabled.
/// </param>
/// <param name="MessagesTabReadOnlyEnabled">
/// A boolean that specifies whether or not the users can send messages to your app in the <a href="https://api.slack.com/surfaces/tabs">Messages tab of your App Home</a>.
/// </param>
public record ManifestAppHome(
    [property:JsonProperty("home_tab_enabled")]
    [property:JsonPropertyName("home_tab_enabled")]
    bool? HomeTabEnabled = null,

    [property:JsonProperty("messages_tab_enabled")]
    [property:JsonPropertyName("messages_tab_enabled")]
    bool? MessagesTabEnabled = null,

    [property:JsonProperty("messages_tab_read_only_enabled")]
    [property:JsonPropertyName("messages_tab_read_only_enabled")]
    bool? MessagesTabReadOnlyEnabled = null);

/// <summary>
/// A subgroup of settings that describe <a href="https://api.slack.com/bot-users">bot user</a> configuration.
/// </summary>
/// <param name="DisplayName">
/// The display name of the bot user.
/// Maximum length is 80 characters.
/// </param>
/// <param name="AlwaysOnline">
/// A boolean that specifies whether or not the bot user will always appear to be online.
/// </param>
public record ManifestBotUser(
    [property:JsonProperty("display_name")]
    [property:JsonPropertyName("display_name")]
    string DisplayName,

    [property:JsonProperty("always_online")]
    [property:JsonPropertyName("always_online")]
    bool? AlwaysOnline = null);

/// <summary>
/// A settings group that describes <a href="https://api.slack.com/interactivity/shortcuts">shortcuts</a> configuration.
/// </summary>
/// <param name="Name">
/// A string containing the name of the shortcut.
/// </param>
/// <param name="CallbackId">
/// A string containing the <c>callback_id</c> of this shortcut.
/// Maximum length is 255 characters.
/// </param>
/// <param name="Description">
/// A string containing a short description of this shortcut.
/// Maximum length is 150 characters.
/// </param>
/// <param name="Type">
/// Which <a href="https://api.slack.com/interactivity/shortcuts">type of shortcut</a> is being described.
/// </param>
public record ManifestShortcut(
    [property:JsonProperty("name")]
    [property:JsonPropertyName("name")]
    string Name,

    [property:JsonProperty("type", DefaultValueHandling = DefaultValueHandling.Include)]
    [property:JsonPropertyName("type")]
    ManifestShortcutType Type,

    [property:JsonProperty("callback_id")]
    [property:JsonPropertyName("callback_id")]
    string CallbackId,

    [property:JsonProperty("description")]
    [property:JsonPropertyName("description")]
    string Description);

/// <summary>
/// The <a href="https://api.slack.com/interactivity/shortcuts">type of shortcut</a> for a <see cref="ManifestShortcut"/>.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum ManifestShortcutType
{
    /// <summary>
    /// <c>message</c> — Message shortcuts are shown to users in the context menus of messages within Slack.
    /// Your app will receive info about the source message when the shortcut is used.
    /// </summary>
    [EnumMember(Value = "message")]
    Message,

    /// <summary>
    /// <c>global</c> — Global shortcuts are available to users via the shortcuts button in the composer, and when using search in Slack.
    /// </summary>
    [EnumMember(Value = "global")]
    Global,
}

/// <summary>
/// A settings group that describes <a href="https://api.slack.com/interactivity/slash-commands">slash commands</a> configuration.
/// </summary>
/// <param name="Command">
/// A string containing the actual slash command.
/// Maximum length is 32 characters, and should include the leading <c>/</c> character.
/// </param>
/// <param name="Description">
/// A string containing a description of the slash command that will be displayed to users.
/// Maximum length is 2000 characters.
/// </param>
/// <param name="ShouldEscape">
/// A boolean that specifies whether or not channels, users, and links typed with the slash command should be escaped.
/// </param>
/// <param name="Url">
/// A string containing the full <c>https</c> URL that acts as the slash command's <a href="https://api.slack.com/interactivity/slash-commands#creating_commands">request URL</a>.
/// </param>
/// <param name="UsageHint">
/// A string a short usage hint about the slash command for users.
/// Maximum length is 1000 characters.
/// </param>
public record ManifestSlashCommand(
    [property:JsonProperty("command")]
    [property:JsonPropertyName("command")]
    string Command,

    [property:JsonProperty("description")]
    [property:JsonPropertyName("description")]
    string Description,

    [property:JsonProperty("should_escape")]
    [property:JsonPropertyName("should_escape")]
    bool? ShouldEscape = null,

    [property:JsonProperty("url")]
    [property:JsonPropertyName("url")]
    string? Url = null,

    [property:JsonProperty("usage_hint")]
    [property:JsonPropertyName("usage_hint")]
    string? UsageHint = null);

/// <summary>
/// A settings group that describes <a href="https://api.slack.com/workflows/steps">workflow steps</a> configuration.
/// </summary>
/// <param name="Name">
/// A string containing the name of the workflow step.
/// Maximum length of 50 characters.
/// </param>
/// <param name="CallbackId">
/// A string containing the <c>callback_id</c> of the workflow step.
/// Maximum length of 50 characters.
/// </param>
public record ManifestWorkflowStep(
    [property:JsonProperty("name")]
    [property:JsonPropertyName("name")]
    string Name,

    [property:JsonProperty("callback_id")]
    [property:JsonPropertyName("callback_id")]
    string CallbackId);
