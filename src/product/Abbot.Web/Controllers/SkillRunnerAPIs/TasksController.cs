using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Api;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Controllers;

public class TasksController : SkillRunnerApiControllerBase
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
        var task = await _apiService.CreateTaskAsync(request, Member, Organization);
        return Json(task);
    }

    [HttpPut("tasks/{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, TaskRequest request)
    {
        Id<TaskItem> taskId = new(id);
        var task = await _apiService.UpdateTaskAsync(taskId, request, Member, Organization);
        return Json(task);
    }
}
