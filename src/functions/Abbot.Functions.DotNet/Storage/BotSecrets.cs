using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Storage;

/// <summary>
/// Used to retrieve secrets stored for the skill.
/// </summary>
public class BotSecrets : ISecrets
{
    readonly ISkillApiClient _apiClient;

    /// <summary>
    /// Constructs a <see cref="BotSecrets"/> instance.
    /// </summary>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> used to call skill runner APIs.</param>
    public BotSecrets(ISkillApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Retrieves a stored secret for the current skill using the <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The key.</param>
    /// <returns>A task with the value of the secret.</returns>
    public async Task<string> GetAsync(string name)
    {
        var url = GetSecretUri(name);
        var response = await _apiClient.SendAsync<SkillSecretResponse>(url, HttpMethod.Get);
        return response?.Secret ?? string.Empty;
    }

    Uri GetSecretUri(string key)
    {
        return _apiClient.BaseApiUrl.Append($"/secret?key={Uri.EscapeDataString(key)}");
    }
}
