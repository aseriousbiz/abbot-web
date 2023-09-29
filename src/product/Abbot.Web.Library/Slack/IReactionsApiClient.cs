using System.Threading.Tasks;
using Refit;

namespace Serious.Slack;

/// <summary>
/// Client for managing reactions in Slack.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/methods?filter=reactions"/>.
/// </remarks>
public interface IReactionsApiClient
{
    /// <summary>
    /// Adds a reaction to an item.
    /// </summary>
    /// <remarks>
    /// See https://api.slack.com/methods/reactions.add
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="name">The reaction name. Example: robot</param>
    /// <param name="channel">Channel where the message to add reaction to was posted. Example: C1234567890</param>
    /// <param name="timestamp">Timestamp of the message to add reaction to. Example: 1234567890.123456</param>
    [Post("/reactions.add")]
    Task<ApiResponse> AddReactionAsync([Authorize] string accessToken, string name, string channel, string timestamp);

    /// <summary>
    /// Removes a reaction to an item.
    /// </summary>
    /// <remarks>
    /// See https://api.slack.com/methods/reactions.remove
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="name">The reaction name. Example: robot</param>
    /// <param name="channel">Channel where the reaction was posted. Example: C1234567890</param>
    /// <param name="timestamp">Timestamp of the message that has the reaction to remove. Example: 1234567890.123456</param>
    [Post("/reactions.remove")]
    Task<ApiResponse> RemoveReactionAsync([Authorize] string accessToken, string name, string channel, string timestamp);

    /// <summary>
    /// Get reactions for a message.
    /// </summary>
    /// <param name="accessToken">The Slack API access token.</param>
    /// <param name="channel">Channel where the message to get reactions for was posted.</param>
    /// <param name="timestamp">Timestamp of the message to get reactions for.</param>
    [Get("/reactions.get")]
    Task<ReactionsResponse> GetMessageReactionsAsync(
        [Authorize] string accessToken,
        string? channel = null,
        string? timestamp = null);
}
