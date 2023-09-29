using System.Threading.Tasks;
using Refit;

namespace Serious.Slack;

/// <summary>
/// A client used to edit or delete a message specified by a <c>response_url</c>.
/// </summary>
/// <remarks>
/// <para>
/// Depending on the source, the interaction payload your app receives may contain a <c>response_url</c>.
/// This <c>response_url</c> is unique to each payload, and can be used to publish messages back to the place where
/// the interaction happened. <see href="https://api.slack.com/interactivity/handling#deleting_message_response">
/// See the Slack documentation for more details.</see>
/// </para>
/// <para>
/// These responses can be sent up to 5 times within 30 minutes of receiving the payload.
/// </para>
/// </remarks>
public interface IResponseUrlClient
{
    /// <summary>
    /// Updates the message.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("")]
    Task<ApiResponse> UpdateAsync([Authorize] string accessToken, ResponseUrlUpdateMessageRequest request);

    /// <summary>
    /// Deletes the message.
    /// </summary>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="request">The request body.</param>
    [Post("")]
    Task<ApiResponse> DeleteAsync([Authorize] string accessToken, ResponseUrlDeleteMessageRequest request);
}
