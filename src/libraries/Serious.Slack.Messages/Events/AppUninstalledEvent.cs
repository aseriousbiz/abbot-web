using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Event raised when someone uninstalls Abbot from Slack.
/// </summary>
[Element("app_uninstalled")]
public record AppUninstalledEvent() : EventBody("app_uninstalled");

/// <summary>
/// Event raised when API tokens for your app are revoked. <see href="https://api.slack.com/events/tokens_revoked"/>.
/// </summary>
/// <param name="Tokens">Information about the revoked tokens.</param>
[Element("tokens_revoked")]
public record TokensRevokedEvent(
    [property: JsonProperty("tokens")]
    [property: JsonPropertyName("tokens")]
    Tokens Tokens) : EventBody("tokens_revoked")
{
    /// <summary>
    /// Constructs an empty <see cref="TokensRevokedEvent"/>.
    /// </summary>
    public TokensRevokedEvent() : this(new Tokens())
    {
    }
};

/// <summary>
/// Contains information about revoked tokens.
/// </summary>
/// <param name="OAuth">The OAuth tokens revoked.</param>
/// <param name="Bot">The Bot tokens revoked.</param>
public record Tokens(
    [property: JsonProperty("oauth")]
    [property: JsonPropertyName("oauth")]
    IReadOnlyList<string> OAuth,

    [property: JsonProperty("bot")]
    [property: JsonPropertyName("bot")]
    IReadOnlyList<string> Bot)
{
    /// <summary>
    /// Creates an empty <see cref="Tokens"/> object.
    /// </summary>
    public Tokens() : this(Array.Empty<string>(), Array.Empty<string>())
    {
    }
};
