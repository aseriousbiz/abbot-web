using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serious.Abbot.Scripting.Utilities;

/// <summary>
/// A useful grab bag of utility methods for C# skill authors.
/// </summary>
public interface IUtilities
{
    /// <summary>
    /// Returns a class that derives from <see href="https://docs.microsoft.com/en-us/dotnet/api/system.random?view=net-5.0">Random</see>, but is cryptographically strong.
    /// </summary>
    Random CreateRandom();

    /// <summary>
    /// Retrieves a random element from a collection. This happens often enough it's nice having a utility method.
    /// </summary>
    /// <param name="list">The collection to retrieve the random item from.</param>
    T GetRandomElement<T>(IReadOnlyList<T> list);

    /// <summary>
    /// Returns the coordinates and name of an address. The address can be specified in the same way you'd use
    /// with mapping software such as specifying a zip, city, full address, name of a business, or
    /// even cross streets.
    /// </summary>
    /// <param name="address">An address to geocode.</param>
    /// <param name="includeTimezone">Optional. Whether or not to include the time zone for the location in the response.</param>
    /// <returns>A task with an <see cref="ILocation"/> that matches the address. If no location matches the input, then returns null.</returns>
    Task<ILocation?> GetGeocodeAsync(string address, bool includeTimezone = false);

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
    bool TryParseSlackUrl(string url, out IMessageTarget? conversation);

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
    bool TryParseSlackUrl(Uri url, out IMessageTarget? conversation);

    /// <summary>
    /// Serializes an object to json
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <param name="withTypes">When <c>true</c>, includes type information in the serialized output.</param>
    /// <returns></returns>
    string Serialize(object? value, bool withTypes = false);

    /// <summary>
    /// Deserialize a json string to a given object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">The JSON string to deserialize.</param>
    /// <returns></returns>
    T? Deserialize<T>(string? value);
}
