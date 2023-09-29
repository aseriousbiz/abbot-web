using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Logging;

namespace Serious.Abbot.Functions.Clients;

/// <summary>
/// Api Client used by the brain to call the Brain API endpoint in order to store and retrieve data items
/// for a skill.
/// </summary>
public class BrainApiClient : IBrainApiClient
{
    static readonly ILogger<BrainApiClient> Log = ApplicationLoggerFactory.CreateLogger<BrainApiClient>();

    readonly ISkillApiClient _apiClient;
    readonly ISkillContextAccessor _skillContextAccessor;

    /// <summary>
    /// Constructs a <see cref="BrainApiClient"/>.
    /// </summary>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> to use to make requests.</param>
    /// <param name="skillContextAccessor"></param>
    public BrainApiClient(ISkillApiClient apiClient, ISkillContextAccessor skillContextAccessor)
    {
        _apiClient = apiClient;
        _skillContextAccessor = skillContextAccessor;
    }

    SkillContext SkillContext => _skillContextAccessor.SkillContext
        ?? throw new InvalidOperationException($"The {nameof(SkillContext)} needs to be set for this request.");

    Uri GetDataUri(string? key, SkillDataScope? scope = null, string? contextId = null)
    {
        var uri = _apiClient.BaseApiUrl.Append("/brain");
        if (key is null or { Length: 0 })
        {
            return uri;
        }

        if (scope is not null && scope != SkillDataScope.Organization && string.IsNullOrWhiteSpace(contextId))
        {
            throw new ArgumentNullException(nameof(contextId), $"Can't query data with scope ${scope} without a context id");
        }

        var uriString = uri.ToString();
        uriString = QueryHelpers.AddQueryString(uriString, "key", key);
        if (scope is not null)
        {
            uriString = QueryHelpers.AddQueryString(uriString, "scope", scope.ToString());
            if (scope != SkillDataScope.Organization)
            {
                uriString = QueryHelpers.AddQueryString(uriString, "contextId", contextId);
            }
        }

        return new Uri(uriString);
    }

    /// <summary>
    /// Retrieves all the skill data keys.
    /// </summary>
    public async Task<SkillDataResponse?> GetSkillDataAsync(string key)
    {
        // grab the skill scope and context id, so that we query the correct data
        var scope = SkillContext.SkillRunnerInfo.Scope;
        var contextId = SkillContext.SkillRunnerInfo.ContextId;

        var url = GetDataUri(key, scope != SkillDataScope.Organization ? scope : null, contextId);
        try
        {
            return await _apiClient.SendAsync<SkillDataResponse>(url, HttpMethod.Get);
        }
        catch (Exception e)
        {
            Log.ExceptionRetrievingBrainData(e, key, url);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all the skill data keys.
    /// </summary>
    public async Task<SkillDataResponse?> GetSkillDataAsync(string key, SkillDataScope scope, string? contextId)
    {
        var url = GetDataUri(key, scope, contextId);
        try
        {
            return await _apiClient.SendAsync<SkillDataResponse>(url, HttpMethod.Get);
        }
        catch (Exception e)
        {
            Log.ExceptionRetrievingBrainData(e, key, url);
            throw;
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllDataAsync()
    {
        var url = GetDataUri(null);
        return await _apiClient.SendAsync<IReadOnlyDictionary<string, string>>(url, HttpMethod.Get)
            ?? new Dictionary<string, string>();
    }

    public async Task<IReadOnlyList<string>> GetSkillDataKeysAsync()
    {
        var all = await GetAllDataAsync();
        return all.Keys.ToReadOnlyList();
    }

    /// <summary>
    /// Deletes a single data item.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    public async Task DeleteDataAsync(string key)
    {
        // grab the skill scope and context id, so that we query the correct data
        var scope = SkillContext.SkillRunnerInfo.Scope;
        var contextId = SkillContext.SkillRunnerInfo.ContextId;

        var url = GetDataUri(key, scope != SkillDataScope.Organization ? scope : null, contextId);
        await _apiClient.SendAsync(url, HttpMethod.Delete);
    }

    /// <summary>
    /// Deletes a single data item.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    /// <param name="scope">The scope of the skill data item</param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    public async Task DeleteDataAsync(string key, SkillDataScope scope, string? contextId)
    {
        var url = GetDataUri(key, scope, contextId);
        await _apiClient.SendAsync(url, HttpMethod.Delete);
    }

    /// <summary>
    /// Stores a key value pair (data item) in the Bot's brain.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    /// <param name="value">The value of the data item.</param>
    /// <returns>A <see cref="SkillDataResponse"/> with information about the created data item.</returns>
    public Task<SkillDataResponse?> PostDataAsync(
        string key,
        string value)
        // grab the skill scope and context id, so that we query the correct data
        => PostDataAsync(key, value, SkillContext.SkillRunnerInfo.Scope, SkillContext.SkillRunnerInfo.ContextId);

    /// <summary>
    /// Stores a key value pair (data item) in the Bot's brain.
    /// </summary>
    /// <param name="key">The key of the data item.</param>
    /// <param name="value">The value of the data item.</param>
    /// <param name="scope">The scope of the skill data item</param>
    /// <param name="contextId">The context id corresponding to the scope</param>
    /// <returns>A <see cref="SkillDataResponse"/> with information about the created data item.</returns>
    public async Task<SkillDataResponse?> PostDataAsync(
        string key,
        string value,
        SkillDataScope scope,
        string? contextId)
    {
        var updateRequest = new SkillDataUpdateRequest
        {
            Value = value,
            Scope = scope,
            ContextId = contextId,
        };
        var url = GetDataUri(key);
        return await _apiClient.SendJsonAsync<SkillDataUpdateRequest, SkillDataResponse>(
            url,
            HttpMethod.Post,
            updateRequest);
    }
}
