using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository that manages patterns for skills. Used to create, retrieve, update, and delete
/// triggers.
/// </summary>
public class PatternRepository : IPatternRepository
{
    /// <summary>
    /// Constructs a <see cref="PatternRepository"/>.
    /// </summary>
    /// <param name="db">The <see cref="AbbotContext"/>.</param>
    public PatternRepository(AbbotContext db)
    {
        Db = db;
    }

    AbbotContext Db { get; }

    /// <summary>
    /// Retrieves all the <see cref="SkillPattern"/> instances in the organization.
    /// </summary>
    /// <param name="organization">The organization to retrieve skill patterns for.</param>
    /// <param name="enabledPatternsOnly">Only return patterns that are enabled.</param>
    /// <returns>All the <see cref="SkillPattern"/> instances for the specified organization.</returns>
    public async Task<IReadOnlyList<SkillPattern>> GetAllAsync(Organization organization, bool enabledPatternsOnly = false)
    {
        // Can't use var because `Include` returns `IIncludableQueryable`.
        // We need query to be a plain IQueryable for later.
        IQueryable<SkillPattern> query = Db.SkillPatterns.Include(p => p.Skill);

        if (enabledPatternsOnly)
        {
            query = query.Where(p => p.Enabled);
        }

        // We need to load all the patterns in the system, but ignore the ones
        // that belong to disabled or deleted skills.
        return await query
            .Where(p => p.Skill.OrganizationId == organization.Id && p.Skill.Enabled && !p.Skill.IsDeleted)
            .OrderBy(p => p.Created) // Oldest patterns first.
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all the <see cref="SkillPattern"/>s for the specified <see cref="Skill"/>.
    /// </summary>
    /// <param name="skill">The skill the patterns belong to.</param>
    /// <returns>All the <see cref="SkillPattern"/> instances for the specified skill.</returns>
    public async Task<IReadOnlyList<SkillPattern>> GetAllForSkillAsync(Skill skill)
    {
        // We need to load all the patterns in the system, but ignore the ones
        // that belong to disabled or deleted skills.
        return await Db.SkillPatterns
            .Include(p => p.Skill)
            .Where(p => p.Skill.Id == skill.Id && !skill.IsDeleted)
            .OrderBy(p => p.Created) // Oldest patterns first.
            .ToListAsync();
    }

    /// <summary>
    /// Retrieve a pattern by skill, name, and organization combination.
    /// </summary>
    /// <param name="skill">The skill the pattern belongs to.</param>
    /// <param name="slug">The slug of the pattern.</param>
    /// <param name="organization">The organization the pattern belongs to.</param>
    public async Task<SkillPattern?> GetAsync(string skill, string slug, Organization organization)
    {
        // We need to load all the patterns in the system, but ignore the ones
        // that belong to disabled or deleted skills.
        return await Db.SkillPatterns
            .Include(p => p.Skill)
            .Where(p => p.Skill.Name == skill && !p.Skill.IsDeleted && p.Skill.OrganizationId == organization.Id && p.Slug == slug)
            .SingleOrDefaultAsync();
    }

    public async Task<SkillPattern> CreateAsync(
        string name,
        string pattern,
        PatternType patternType,
        bool caseSensitive,
        Skill skill,
        User creator,
        bool enabled,
        bool allowExternalCallers = false)
    {
        var now = DateTime.UtcNow;

        if (name is { Length: 0 })
        {
            throw new ArgumentException("Empty names are not allowed.", nameof(pattern));
        }

        if (pattern is { Length: 0 })
        {
            throw new ArgumentException("Empty patterns are not allowed.", nameof(pattern));
        }

        if (patternType is PatternType.RegularExpression)
        {
            var _ = Regex.Match("", pattern); // Throw ArgumentException if pattern isn't a regular expression.
        }

        var skillPattern = new SkillPattern
        {
            Name = name,
            Slug = name.ToSlug(), // We generate a slug for skill patterns.
            Pattern = pattern,
            PatternType = patternType,
            CaseSensitive = caseSensitive,
            Creator = creator,
            Created = now,
            ModifiedBy = creator,
            Modified = now,
            Enabled = enabled,
            AllowExternalCallers = allowExternalCallers,
        };
        skill.Patterns.Add(skillPattern);
        await Db.SaveChangesAsync();
        return skillPattern;
    }

    /// <summary>
    /// Updates the specified pattern.
    /// </summary>
    /// <param name="pattern">The updated pattern.</param>
    /// <param name="modifier">The user making the change.</param>
    public async Task UpdateAsync(SkillPattern pattern, User modifier)
    {
        pattern.Slug = pattern.Name.ToSlug(); // Update the slug if the name changed.
        pattern.Modified = DateTime.UtcNow;
        pattern.ModifiedBy = modifier;
        await Db.SaveChangesAsync();
    }

    /// <summary>
    /// Delete the specified pattern.
    /// </summary>
    /// <param name="pattern">The pattern to delete.</param>
    /// <param name="deleteBy">The user deleting the pattern.</param>
    public async Task DeleteAsync(SkillPattern pattern, User deleteBy)
    {
        Db.SkillPatterns.Remove(pattern);
        await Db.SaveChangesAsync();
    }
}
