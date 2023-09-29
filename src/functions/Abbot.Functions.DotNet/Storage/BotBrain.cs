using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Functions.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Scripting;
using Serious.Abbot.Storage;
using Serious.Logging;

namespace Serious.Abbot.Functions.Storage;

public class BotBrain : IExtendedBrain
{
    static readonly ILogger<BotBrain> Log = ApplicationLoggerFactory.CreateLogger<BotBrain>();

    readonly IBrainApiClient _apiClient;
    readonly IBrainSerializer _brainSerializer;

    public BotBrain(IBrainApiClient apiClient, IBrainSerializer brainSerializer)
    {
        _apiClient = apiClient;
        _brainSerializer = brainSerializer;
    }

    public async Task<dynamic?> GetAsync(string key)
    {
        Log.BrainAction("Retrieve", key, null);
        var response = await _apiClient.GetSkillDataAsync(key);

        var retrieved = response?.Value is null
            ? null
            : _brainSerializer.Deserialize(response.Value);

        return retrieved;
    }

    public async Task<dynamic?> GetAsync(string key, SkillDataScope scope, string? contextId)
    {
        Log.BrainAction("Retrieve", key, null);
        var response = await _apiClient.GetSkillDataAsync(key, scope, contextId);

        var retrieved = response?.Value is null
            ? null
            : _brainSerializer.Deserialize(response.Value);

        return retrieved;
    }

    public async Task<T?> GetAsAsync<T>(string key)
    {
        Log.RetrieveBrainDataAsType(key, typeof(T));
        var response = await _apiClient.GetSkillDataAsync(key);

        return response?.Value is null
            ? default
            : _brainSerializer.Deserialize<T>(response.Value);
    }

    public async Task<T?> GetAsAsync<T>(string key, SkillDataScope scope, string? contextId)
    {
        Log.RetrieveBrainDataAsType(key, typeof(T));
        var response = await _apiClient.GetSkillDataAsync(key, scope, contextId);

        return response?.Value is null
            ? default
            : _brainSerializer.Deserialize<T>(response.Value);
    }

    public async Task<T> GetAsAsync<T>(string key, T defaultValue)
    {
        Log.RetrieveBrainDataAsType(key, typeof(T));

        var response = await _apiClient.GetSkillDataAsync(key);

        var retrieved = response?.Value is null
            ? defaultValue
            : _brainSerializer.Deserialize<T>(response.Value);

        return retrieved!;
    }

    public async Task WriteAsync(string key, object value)
    {
        var serializedValue = _brainSerializer.SerializeObject(value);
        Log.BrainAction("Write", key, serializedValue);

        await _apiClient.PostDataAsync(key, serializedValue);
    }

    public async Task WriteAsync(string key, object value, SkillDataScope scope, string? contextId)
    {
        var serializedValue = _brainSerializer.SerializeObject(value);
        Log.BrainAction("Write", key, serializedValue);

        await _apiClient.PostDataAsync(key, serializedValue, scope, contextId);
    }

    public Task DeleteAsync(string key)
    {
        Log.BrainAction("Delete", key, null);
        return _apiClient.DeleteDataAsync(key);
    }

    public Task DeleteAsync(string key, SkillDataScope scope, string? contextId)
    {
        Log.BrainAction("Delete", key, null);
        return _apiClient.DeleteDataAsync(key, scope, contextId);
    }

    public async Task<IReadOnlyList<string>> GetKeysAsync(string? fuzzyKeyFilter = null)
    {
        var keys = await _apiClient.GetSkillDataKeysAsync();
        return fuzzyKeyFilter is null
            ? keys
            : keys.WhereFuzzyMatch(fuzzyKeyFilter).ToReadOnlyList();
    }

    public async Task<IReadOnlyList<ISkillDataItem>> GetAllAsync(string? fuzzyKeyFilter = null)
    {
        var values = await _apiClient.GetAllDataAsync();
        var results = values.Select(v => new SkillDataItem(v.Key, v.Value, _brainSerializer)).ToReadOnlyList();
        return fuzzyKeyFilter is null
            ? results
            : results.WhereFuzzyMatch(r => r.Key, fuzzyKeyFilter).ToReadOnlyList();
    }
}
