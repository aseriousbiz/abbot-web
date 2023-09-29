using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;
using Serious.Abbot.Storage;
using Serious.Cryptography;

namespace Serious.Abbot.Execution;

/// <summary>
/// A useful grab bag of utility methods for C# skill authors.
/// </summary>
public class BotUtilities : IUtilities
{
    static readonly CryptoRandom Random = new();
    readonly ISkillApiClient _apiClient;
    readonly IBrainSerializer _serializer;

    /// <summary>
    /// Constructs a <see cref="BotUtilities"/>.
    /// </summary>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> used to call skill runner APIs.</param>
    /// <param name="serializer"></param>
    public BotUtilities(ISkillApiClient apiClient, IBrainSerializer serializer)
    {
        _apiClient = apiClient;
        _serializer = serializer;
    }

    /// <summary>
    /// Returns a class that derives from <see href="https://docs.microsoft.com/en-us/dotnet/api/system.random?view=net-5.0">Random</see>, but is cryptographically strong.
    /// </summary>
    public Random CreateRandom()
    {
        return new CryptoRandom();
    }

    /// <summary>
    /// Retrieves a random element from a collection. This happens often enough it's nice having a utility method.
    /// </summary>
    /// <param name="list">The collection to retrieve the random item from.</param>
    public T GetRandomElement<T>(IReadOnlyList<T> list)
    {
        var randomIndex = Random.Next(0, list.Count);
        return list[randomIndex];
    }

    /// <summary>
    /// Returns the coordinates and name of an address. The address can be specified in the same way you'd use
    /// with mapping software such as specifying a zip, city, full address, name of a business, or
    /// even cross streets.
    /// </summary>
    /// <param name="address">An address to geocode.</param>
    /// <param name="includeTimezone">Optional. Whether or not to include the time zone for the location in the response.</param>
    /// <returns>A task with an <see cref="ILocation"/> that matches the address. If no location matches the input, then returns null.</returns>
    public async Task<ILocation?> GetGeocodeAsync(string address, bool includeTimezone = false)
    {
        var url = _apiClient.BaseApiUrl.Append($"geo?address={HttpUtility.UrlEncode(address)}&includeTimezone={includeTimezone}");
        var location = await _apiClient.SendAsync<Location>(url, HttpMethod.Get);
        return location;
    }

    /// <summary>
    /// Attempts to parse a Slack URL into an <see cref="IMessageTarget"/> that can be used to send to it.
    /// </summary>
    /// <remarks>
    /// You can pass any of the following URLs to this method:
    /// * A URL to a Slack message, to refer to a thread, for example: https://myorg.slack.com/archives/C0000000000/p0000000000000000
    /// * A URL to a Slack DM session, for example: https://myorg.slack.com/archives/D0000000000
    /// * A URL to a Slack channel, for example: https://myorg.slack.com/archives/C0000000000
    /// </remarks>
    /// <param name="url">The URL to parse.</param>
    /// <param name="conversation">An <see cref="IMessageTarget"/> that can be used to send messages to that destination.</param>
    /// <returns>A boolean indicating if parsing was successful.</returns>
    public bool TryParseSlackUrl(Uri url, out IMessageTarget? conversation) => TryParseSlackUrl(url.ToString(), out conversation);

    /// <summary>
    /// Attempts to parse a Slack URL into an <see cref="IMessageTarget"/> that can be used to send to it.
    /// </summary>
    /// <remarks>
    /// You can pass any of the following URLs to this method:
    /// * A URL to a Slack message, to refer to a thread, for example: https://myorg.slack.com/archives/C0000000000/p0000000000000000
    /// * A URL to a Slack DM session, for example: https://myorg.slack.com/archives/D0000000000
    /// * A URL to a Slack channel, for example: https://myorg.slack.com/archives/C0000000000
    /// * A URL to a Slack user, for example: https://myorg.slack.com/team/U0000000000
    /// </remarks>
    /// <param name="url">The URL to parse.</param>
    /// <param name="conversation">An <see cref="IMessageTarget"/> that can be used to send messages to that destination.</param>
    /// <returns>A boolean indicating if parsing was successful.</returns>
    public bool TryParseSlackUrl(string url, out IMessageTarget? conversation)
    {
        if (!SlackUrl.TryParse(url, out var slackUrl))
        {
            conversation = null;
            return false;
        }

        if (slackUrl is SlackUserUrl userUrl)
        {
            conversation = new MessageTarget(new ChatAddress(ChatAddressType.User, userUrl.UserId));
            return true;
        }

        if (slackUrl is SlackConversationUrlBase convoUrl)
        {
            if (SlackIdUtility.GetChatAddressTypeFromSlackId(convoUrl.ConversationId) is not { } addressType)
            {
                conversation = null;
                return false;
            }

            if (convoUrl is SlackMessageUrl messageUrl)
            {
                conversation = new MessageTarget(new ChatAddress(addressType,
                    messageUrl.ConversationId,
                    messageUrl.ThreadTimestamp ?? messageUrl.Timestamp));

                return true;
            }

            conversation = new MessageTarget(new ChatAddress(addressType, convoUrl.ConversationId));
            return true;
        }

        conversation = null;
        return false;
    }


    public string Serialize(object? value, bool withTypes = false)
    {
        return value is null ? string.Empty : _serializer.SerializeObject(value, withTypes);
    }

    public T? Deserialize<T>(string? value)
    {
        return value is null ? default : _serializer.Deserialize<T>(value);
    }
}
