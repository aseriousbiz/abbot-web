using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Rebuild all the scheduled triggers.
/// </summary>
public sealed class ScheduledTaskFixupSeeder : IDataSeeder
{
    readonly AbbotContext _db;
    readonly IBackgroundJobClient _jobClient;
    readonly IScheduledSkillClient _scheduledSkillClient;

    public ScheduledTaskFixupSeeder(
        AbbotContext db,
        IBackgroundJobClient jobClient,
        IScheduledSkillClient scheduledSkillClient)
    {
        _db = db;
        _jobClient = jobClient;
        _scheduledSkillClient = scheduledSkillClient;
    }

    /// <summary>
    /// Fixes all the Slack organizations with bad PlatformId.
    /// </summary>
    public Task SeedDataAsync()
    {
        _jobClient.Schedule(() => RunSeedDataAsync(), TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }

    [DisplayName("Rebuild Scheduled Triggers")]
    [Queue(HangfireQueueNames.Maintenance)]
    public async Task RunSeedDataAsync()
    {
        // Grab all the scheduled triggers for enabled skills.
        var triggers = await _db.SkillScheduledTriggers
            .Include(t => t.Skill)
            .ThenInclude(s => s.Organization)
            .Where(t => t.Skill.Enabled)
            .ToListAsync();
        foreach (var trigger in triggers)
        {
            // This will rebuild the scheduled job (it won't schedule it twice).
            _scheduledSkillClient.ScheduleSkill(trigger);
        }
    }

#if DEBUG
    public bool Enabled => true;
#else
        public bool Enabled => false; // We don't need to run this in production anymore.
#endif
}
