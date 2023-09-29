using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Subscriptions;

public class DeletePageModel : SkillFeatureEditPageModel
{
    readonly ISignalRepository _signalRepository;
    readonly IPermissionRepository _permissions;

    public DeletePageModel(
        ISignalRepository signalRepository,
        IPermissionRepository permissions)
    {
        _signalRepository = signalRepository;
        _permissions = permissions;
    }

    public string SignalName { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string skill, string name)
    {
        var (_, subscription) = await InitializePageState(skill, name);
        if (subscription is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill, string name)
    {
        var (user, subscription) = await InitializePageState(skill, name);
        if (subscription is null)
        {
            return NotFound();
        }

        await _signalRepository.RemoveAsync(subscription, user, subscription.Skill.Organization);

        return RedirectBack();
    }

    async Task<(User, SignalSubscription?)> InitializePageState(string skill, string name)
    {
        var member = Viewer;
        var (user, organization) = member;
        var subscription = await _signalRepository.GetAsync(name, skill, organization);

        if (subscription is null)
        {
            return (user, null);
        }

        if (!await _permissions.CanEditAsync(member, subscription.Skill))
        {
            return (user, null);
        }

        SignalName = subscription.Name;

        return (user, subscription);
    }
}
