using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;

namespace Serious.Abbot.Functions.Clients;

public class MetadataClient : IMetadataClient
{
    readonly ISkillApiClient _apiClient;

    public MetadataClient(ISkillApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<AbbotResponse<IReadOnlyList<MetadataFieldInfo>>> GetAllAsync()
        => await _apiClient.GetApiAsync<IReadOnlyList<MetadataFieldInfo>, List<MetadataFieldInfo>>(MetadataApiUrl);


    public async Task<AbbotResponse<MetadataFieldInfo>> GetByNameAsync(string name)
        => await _apiClient.GetApiAsync<MetadataFieldInfo>(GetFieldUrl(name));

    public async Task<AbbotResponse<MetadataFieldInfo>> CreateAsync(MetadataFieldInfo metadataField)
        => await _apiClient.SendApiAsync<MetadataFieldInfo, MetadataFieldInfo>(
            MetadataApiUrl,
            HttpMethod.Post,
            metadataField);

    public async Task<AbbotResponse<MetadataFieldInfo>> UpdateAsync(string name, MetadataFieldInfo metadataField)
        => await _apiClient.SendApiAsync<MetadataFieldInfo, MetadataFieldInfo>(
            GetFieldUrl(name),
            HttpMethod.Put,
            metadataField);

    Uri MetadataApiUrl => _apiClient.BaseApiUrl.Append("/metadata");

    Uri GetFieldUrl(string name)
    {
        return MetadataApiUrl.AppendEscaped(name);
    }
}
