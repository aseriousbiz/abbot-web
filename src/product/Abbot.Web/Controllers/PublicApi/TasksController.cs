using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Api;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;

namespace Serious.Abbot.Controllers.PublicApi;

[ApiController]
[AbbotApiHost]
[Route("api/tasks")]
[Authorize(Policy = AuthorizationPolicies.PublicApi)]
public class TasksController : UserControllerBase
{
    readonly TasksApiService _apiService;

    public TasksController(TasksApiService apiService)
    {
        _apiService = apiService;
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetAllAsync()
    {
        return Json(await _apiService.GetAllAsync(Organization));
    }

    [HttpGet("tasks/{id:int}")]
    public async Task<IActionResult> GetAsync(int id)
    {
        var task = await _apiService.GetAsync(id, Organization);
        return task is not null
            ? Json(task)
            : NotFound();
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateAsync(TaskRequest request)
    {
        var task = await _apiService.CreateTaskAsync(request, CurrentMember, Organization);
        return Json(task);
    }

    [HttpPut("tasks/{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, TaskRequest request)
    {
        Id<TaskItem> taskId = new(id);
        var task = await _apiService.UpdateTaskAsync(taskId, request, CurrentMember, Organization);
        return Json(task);
    }
}
