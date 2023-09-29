using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Api;

public class TasksApiService
{
    readonly TaskRepository _taskRepository;
    readonly IUserRepository _userRepository;
    readonly CustomerRepository _customerRepository;

    public TasksApiService(
        TaskRepository taskRepository,
        IUserRepository userRepository,
        CustomerRepository customerRepository)
    {
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _customerRepository = customerRepository;
    }

    public async Task<IReadOnlyList<TaskItemInfo>> GetAllAsync(Organization organization)
    {
        var tasks = await _taskRepository.GetAllAsync(organization);
        return tasks.Select(task => task.ToTaskItemInfo()).WhereNotNull().ToList();
    }

    public async Task<TaskItemInfo?> GetAsync(int id, Organization organization)
    {
        var taskItem = await _taskRepository.GetByIdAsync(id, organization);

        return taskItem?.ToTaskItemInfo();
    }

    public async Task<TaskItemInfo> CreateTaskAsync(
        TaskRequest request,
        Member actor,
        Organization organization)
    {
        var assignee = request.Assignee is not null
            ? await _userRepository.GetMemberByPlatformUserId(request.Assignee.Id, organization)
            : null;

        var customer = request.Customer is not null
            ? await _customerRepository.GetByIdAsync(request.Customer.Id, organization)
            : null;

        var task = await _taskRepository.CreateTaskAsync(request.Title, assignee, customer, actor, organization);
        return task.ToTaskItemInfo();
    }

    public async Task<TaskItemInfo?> UpdateTaskAsync(
        Id<TaskItem> id,
        TaskRequest request,
        Member actor,
        Organization organization)
    {
        var task = await _taskRepository.GetByIdAsync(id, organization);
        if (task is null)
        {
            return null;
        }

        var assignee = request.Assignee is not null
            ? await _userRepository.GetMemberByPlatformUserId(request.Assignee.Id, organization)
            : null;
        var customer = request.Customer is not null
            ? await _customerRepository.GetByIdAsync(request.Customer.Id, organization)
            : null;

        await _taskRepository.UpdateTaskAsync(task, request.Title, assignee, customer, request.Status, actor, organization);

        return task.ToTaskItemInfo();
    }
}
