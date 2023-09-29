using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The body of the request to retrieve or create a multi-party DM.
/// </summary>
/// <param name="Channel">Resume a conversation by supplying an im or mpim's ID. Or provide the <paramref cref="Users"/> field instead.</param>
/// <param name="Users">Comma separated lists of users. If only one user is included, this creates a 1:1 DM.</param>
/// <param name="PreventCreation">Do not create a direct message or multi-person direct message. This is used to see if there is an existing dm or mpdm.</param>
/// <param name="ReturnIm">Boolean, indicates you want the full IM channel definition in the response.</param>
public record OpenConversationRequest(

    [property:JsonProperty("channel")]
    [property:JsonPropertyName("channel")]
    string? Channel,

    [property:JsonProperty("users")]
    [property:JsonPropertyName("users")]
    string? Users,

    [property:JsonProperty("prevent_creation")]
    [property:JsonPropertyName("prevent_creation")]
    bool PreventCreation,

    [property:JsonProperty("return_im")]
    [property:JsonPropertyName("return_im")]
    bool ReturnIm)
{
    public static OpenConversationRequest FromUsers(IEnumerable<string> userIds) => new(null, Users: string.Join(",", userIds), false, false);
}
