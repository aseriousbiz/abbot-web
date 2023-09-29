using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Execution;

public class TasksClient : ITasksClient
{
    readonly ISkillApiClient _apiClient;

    /// <summary>
    /// Constructs a <see cref="TasksClient"/>.
    /// </summary>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> used to call skill runner APIs.</param>
    public TasksClient(ISkillApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    Uri TasksApiUrl => _apiClient.BaseApiUrl.Append("/tasks");

    Uri GetTaskUrl(int taskId) => TasksApiUrl.AppendEscaped($"{taskId}");

    public async Task<AbbotResponse<IReadOnlyList<TaskItemInfo>>> GetAllAsync()
        => await _apiClient.GetApiAsync<IReadOnlyList<TaskItemInfo>, List<TaskItemInfo>>(TasksApiUrl);

    public async Task<AbbotResponse<TaskItemInfo>> GetAsync(int id)
        => await _apiClient.GetApiAsync<TaskItemInfo>(GetTaskUrl(id));

    public async Task<AbbotResponse<TaskItemInfo>> CreateAsync(TaskRequest taskRequest)
        => await _apiClient.SendApiAsync<TaskRequest, TaskItemInfo>(
            TasksApiUrl,
            HttpMethod.Post,
            taskRequest);

    public async Task<AbbotResponse<TaskItemInfo>> UpdateAsync(int id, TaskRequest taskRequest)
        => await _apiClient.SendApiAsync<TaskRequest, TaskItemInfo>(
            GetTaskUrl(id),
            HttpMethod.Put,
            taskRequest);
}
