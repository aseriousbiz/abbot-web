using System;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;

namespace Serious.Abbot.Functions.Execution;

/// <summary>
/// Used to retrieve information about Slack users.
/// </summary>
public class UsersClient : IUsersClient
{
    readonly ISkillApiClient _apiClient;

    public UsersClient(ISkillApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public IUserMessageTarget GetTarget(string id) => new UserMessageTarget(id);

    /// <inheritdoc />
    public async Task<AbbotResponse<IUserDetails>> GetUserAsync(string id)
    {
        var url = GetUserUrl(id);
        return await _apiClient.GetApiAsync<IUserDetails, UserDetails>(url);
    }

    Uri UsersApiUrl => _apiClient.BaseApiUrl.Append("/users");

    Uri GetUserUrl(string userId)
    {
        return UsersApiUrl.AppendEscaped(userId);
    }
}
