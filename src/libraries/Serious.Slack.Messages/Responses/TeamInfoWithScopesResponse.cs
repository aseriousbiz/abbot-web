using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Response from the <c>team.info</c> endpoint with scopes.
/// </summary>
public class TeamInfoWithScopesResponse : InfoResponse<TeamInfoWithScopes>
{
    readonly bool _ok;
    readonly TeamInfoWithScopes? _teamInfoWithScopes;

    /// <summary>
    /// Constructs a <see cref="TeamInfoWithScopes" /> from a <see cref="TeamInfoResponse" />
    /// and a set of scopes.
    /// </summary>
    /// <param name="teamInfoResponse"></param>
    /// <param name="scopes"></param>
    public TeamInfoWithScopesResponse(TeamInfoResponse teamInfoResponse, string scopes)
    {
        Error = teamInfoResponse.Error;
        ResponseMetadata = teamInfoResponse.ResponseMetadata;
        _ok = teamInfoResponse.Ok;
        _teamInfoWithScopes = teamInfoResponse.Body is not null
            ? new TeamInfoWithScopes(teamInfoResponse.Body, scopes)
            : null;
    }

    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok
    {
        get => _ok;
        init => throw new InvalidOperationException(
            $"{nameof(TeamInfoWithScopes)} is a readonly type.");
    }

    /// <summary>
    /// Information about a Slack Team. This is returned by the <c>team.info</c> endpoint.
    /// The scopes are populated from the request headers.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public override TeamInfoWithScopes? Body
    {
        get => _teamInfoWithScopes;
        init => throw new InvalidOperationException($"{nameof(TeamInfoWithScopes)}"
                                                    + "is a readonly type.");
    }
}
