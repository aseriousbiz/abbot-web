using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Subscriptions;

public class AllPageModel : CustomSkillPageModel
{
    readonly ISignalRepository _repository;

    public IDictionary<string, IEnumerable<SignalSubscription>> Subscriptions { get; private set; } = null!;

    public AllPageModel(ISignalRepository repository, IUserRepository userRepository)
    {
        _repository = repository;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var subscriptions = await _repository.GetAllSubscriptionsAsync(Organization);
        Subscriptions = subscriptions.GroupBy(s => s.Name)
            .OrderBy(s => s.Key)
            .ToDictionary(s => s.Key, g => g.Select(s => s));
        return Page();
    }
}
