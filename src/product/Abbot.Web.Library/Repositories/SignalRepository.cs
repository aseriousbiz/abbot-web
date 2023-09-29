using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository that manages patterns for skills. Used to create, retrieve, update, and delete
/// triggers.
/// </summary>
public class SignalRepository : ISignalRepository
{
    readonly IAuditLog _auditLog;

    /// <summary>
    /// Constructs a <see cref="PatternRepository"/>.
    /// </summary>
    /// <param name="db">The <see cref="AbbotContext"/>.</param>
    /// <param name="auditLog">The audit log.</param>
    public SignalRepository(AbbotContext db, IAuditLog auditLog)
    {
        _auditLog = auditLog;
        Db = db;
    }

    AbbotContext Db { get; }

    public async Task<IReadOnlyList<string>> GetAllAsync(Organization organization)
    {
        // We need to load all the signals in the system, but ignore the ones 
        // that belong to disabled or deleted skills.
        return await GetSubscriptionsQueryable(organization)
            .Select(s => s.Name)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SignalSubscription>> GetAllSubscriptionsAsync(Organization organization)
    {
        // We need to load all the signals in the system, but ignore the ones
        // that belong to disabled or deleted skills.
        return await GetSubscriptionsQueryable(organization).ToListAsync();
    }

    IQueryable<SignalSubscription> GetSubscriptionsQueryable(Id<Organization> organizationId)
    {
        return Db.SignalSubscriptions
            .Include(s => s.Skill)
            .ThenInclude(sk => sk.Organization)
            .Include(s => s.Creator)
            .Where(s => s.Skill.OrganizationId == organizationId && !s.Skill.IsDeleted && s.Skill.Enabled);
    }

    public async Task<SignalSubscription?> GetAsync(string name, string skill, Organization organization)
    {
        var normalizedName = name.ToLowerInvariant();
        return await GetSubscriptionsQueryable(organization)
            .Where(s => s.Skill.Name == skill && s.Name == normalizedName)
            .SingleOrDefaultAsync();
    }

    public async Task<SignalSubscription> CreateAsync(
        string name,
        string? argumentsPattern,
        PatternType argumentsPatternType,
        bool caseSensitive,
        Skill skill,
        User creator)
    {
        var now = DateTime.UtcNow;

        if (name is { Length: 0 })
        {
            throw new ArgumentException("Empty names are not allowed.", nameof(name));
        }

        var subscription = new SignalSubscription
        {
            Name = name.ToLowerInvariant(),
            ArgumentsPattern = argumentsPattern,
            ArgumentsPatternType = argumentsPatternType,
            CaseSensitive = caseSensitive,
            Skill = skill,
            Created = now,
            Creator = creator
        };
        await Db.SignalSubscriptions.AddAsync(subscription);
        await Db.SaveChangesAsync();
        await _auditLog.LogEntityCreatedAsync(subscription, creator, skill.Organization);

        return subscription;
    }

    /// <summary>
    /// Delete the specified pattern.
    /// </summary>
    /// <param name="subscription">The signal to remove from the skill.</param>
    /// <param name="deleteBy">The user deleting the pattern.</param>
    /// <param name="organization">The organization to remove the subscription from.</param>
    public async Task RemoveAsync(SignalSubscription subscription, User deleteBy, Organization organization)
    {
        await _auditLog.LogEntityDeletedAsync(subscription, deleteBy, organization);
        Db.SignalSubscriptions.Remove(subscription);
        await Db.SaveChangesAsync();
    }
}
