using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository for managing <see cref="Tag"/> entities that can be applied to a <see cref="Conversation"/> via the
/// <see cref="ConversationTag"/> mapping entity.
/// </summary>
public interface ITagRepository : IOrganizationScopedRepository<Tag>
{
    /// <summary>
    /// Retrieves the set of results that match the specified set of tag names.
    /// </summary>
    /// <param name="tagNames">The tag names.</param>
    /// <param name="organization"></param>
    /// <returns></returns>
    Task<IReadOnlyList<EntityLookupResult<Tag, string>>> GetTagsByNamesAsync(
        IEnumerable<string> tagNames,
        Organization organization);

    /// <summary>
    /// Retrieves the set of results that match the specified set of tag names.
    /// </summary>
    /// <param name="tagName">The tag names.</param>
    /// <param name="organization"></param>
    /// <returns></returns>
    Task<Tag?> GetTagByNameAsync(string tagName, Organization organization);

    /// <summary>
    /// Updates the set of tags for the <see cref="Conversation" /> to match the specified set of tag Ids.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="tagIds">The resulting set of tag Ids.</param>
    /// <param name="actor">The user adding the tags.</param>
    Task TagConversationAsync(Conversation conversation, IEnumerable<int> tagIds, User actor);

    /// <summary>
    /// Ensures all the specified tags exist, creating ones that don't.
    /// </summary>
    /// <param name="tags">The set of tags to ensure.</param>
    /// <param name="description">The description to use for any newly created tags.</param>
    /// <param name="actor">The <see cref="Member"/> that will create these tags.</param>
    /// <param name="organization">The organization the tags belong to.</param>
    Task<IReadOnlyList<Tag>> EnsureTagsAsync(
        IEnumerable<string> tags,
        string? description,
        Member actor,
        Organization organization);

    /// <summary>
    /// Retrieve all user tags and some AI generated tags. It filters out the AI tags we don't care to show to the user.
    /// </summary>
    /// <param name="organization">The organization the entities belongs to.</param>
    Task<IReadOnlyList<Tag>> GetAllVisibleTagsAsync(Organization organization);

    /// <summary>
    /// Retrieves a page of all entities for the organization.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>A <see cref="IPaginatedList{Tag}"/> containing the specified page of results.</returns>
    Task<IPaginatedList<Tag>> GetAllUserTagsAsync(Organization organization, int pageNumber, int pageSize);
}

public sealed class TagRepository : OrganizationScopedRepository<Tag>, ITagRepository
{
    readonly IAuditLog _auditLog;
    readonly IClock _clock;

    // Hard-coded filters for now. We can change this later.
    // This is an expression so we can use it in the Where() clause of a query.
#pragma warning disable CA1309
    public static Expression<Func<ConversationTag, bool>> VisibleConversationTagsExpression
        => t => !t.Tag.Name.StartsWith("state:")
                && !t.Tag.Name.Equals("sentiment:neutral")
                && !t.Tag.Name.Equals("topic:social")
                && !t.Tag.Name.Equals("sentiment:positive");
    public static Expression<Func<Tag, bool>> VisibleTagsExpression
        => t => !t.Name.StartsWith("state:")
                && !t.Name.Equals("sentiment:neutral")
                && !t.Name.Equals("topic:social")
                && !t.Name.Equals("sentiment:positive");
#pragma warning restore CA1309

    // Pay the cost once.
    public static readonly Func<ConversationTag, bool> VisibleTagsFilter = VisibleConversationTagsExpression.Compile();

    public TagRepository(AbbotContext db, IAuditLog auditLog, IClock clock) : base(db, auditLog)
    {
        _auditLog = auditLog;
        _clock = clock;
    }

    protected override DbSet<Tag> Entities => Db.Tags;

    protected override async Task LogEntityCreatedAsync(Tag entity, User creator)
    {
        await _auditLog.LogEntityCreatedAsync(entity, creator, entity.Organization);
    }

    protected override async Task LogEntityDeletedAsync(Tag entity, User actor)
    {
        await _auditLog.LogEntityDeletedAsync(entity, actor, entity.Organization);
    }

    protected override async Task LogEntityChangedAsync(Tag entity, User actor)
    {
        await _auditLog.LogEntityChangedAsync(entity, actor, entity.Organization);
    }

    public override IQueryable<Tag> GetQueryable(Organization organization)
    {
        return base.GetQueryable(organization)
            .Include(t => t.Conversations)
            .OrderBy(t => t.Name);
    }

    public async Task<Tag?> GetTagByNameAsync(string tagName, Organization organization)
    {
        var result = await GetTagsByNamesAsync(new[] { tagName }, organization);
        return result.SingleOrDefault()?.Entity;
    }

    public async Task TagConversationAsync(Conversation conversation, IEnumerable<int> tagIds, User actor)
    {
        var tagsToRemove = conversation.Tags
            .Where(t => !tagIds.Contains(t.TagId))
            .Where(t => !t.Tag.Generated)
            .ToList();
        foreach (var tagToRemove in tagsToRemove)
        {
            conversation.Tags.Remove(tagToRemove);
        }

        var existingTagIds = conversation.Tags.Select(t => t.TagId).ToArray();
        var tagIdsToAdd = tagIds.Where(id => !existingTagIds.Contains(id));
        // Tags to add.
        var tagsToAdd = await Entities
            .Where(t => t.OrganizationId == conversation.OrganizationId)
            .Where(t => tagIdsToAdd.Contains(t.Id))
            .ToListAsync();
        foreach (var tagToAdd in tagsToAdd)
        {
            conversation.Tags.Add(new ConversationTag
            {
                Conversation = conversation,
                Tag = tagToAdd,
                Created = _clock.UtcNow,
                Creator = actor,
            });
        }
        await Db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<EntityLookupResult<Tag, string>>> GetTagsByNamesAsync(
        IEnumerable<string> tagNames,
        Organization organization)
    {
        var tagNamesList = tagNames.ToList();
        var normalizedTagNames = tagNamesList.Select(t => t.ToUpperInvariant()).ToList();
        var tags = await GetQueryable(organization)
            .Where(tag => normalizedTagNames.Contains(tag.Name.ToUpper()))
            .ToListAsync();
        // TODO: Use `ToDictionaryAsync` after we clean up the existing data.
        var groupedTags = tags
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(t => t.Key, t => t.First(), StringComparer.OrdinalIgnoreCase);

        EntityLookupResult<Tag, string> GatherResult(string tagName)
        {
            return groupedTags.TryGetValue(tagName, out var tag)
                ? EntityResult.Success(tag, tagName)
                : EntityResult.NotFound<Tag, string>(tagName);
        }

        return tagNamesList.Select(GatherResult).OrderBy(t => t.Key).ToList();
    }

    public async Task<IReadOnlyList<Tag>> EnsureTagsAsync(
        IEnumerable<string> tags,
        string? description,
        Member actor,
        Organization organization)
    {
        var results = await GetTagsByNamesAsync(tags, organization);

        var tagsToCreate = results
            .Where(r => !r.IsSuccess)
            .Where(r => Tag.IsValidTagName(r.Key, allowGenerated: true))
            .Select(r => new Tag
            {
                Name = r.Key,
                Organization = organization,
                Created = _clock.UtcNow,
                Creator = actor.User,
                ModifiedBy = actor.User,
                Description = description,
            })
            .ToList();

        Entities.AddRange(tagsToCreate);
        var existingTags = results.Where(r => r.IsSuccess).Select(r => r.Entity).WhereNotNull();
        await Db.SaveChangesAsync();
        return existingTags.Concat(tagsToCreate).ToList();
    }

    public async Task<IReadOnlyList<Tag>> GetAllVisibleTagsAsync(Organization organization)
    {
        return (await GetQueryable(organization)
            .Where(VisibleTagsExpression)
            .ToListAsync())
            .OrderBy(t => t.Generated)
            .ToList();
    }

    public async Task<IPaginatedList<Tag>> GetAllUserTagsAsync(Organization organization, int pageNumber, int pageSize)
    {
        var queryable = GetQueryable(organization)
            .Include(t => t.Conversations)
            .ThenInclude(ct => ct.Conversation)
            .Where(t => !EF.Functions.Like(t.Name, "%:%"));
        return await PaginatedList.CreateAsync(queryable, pageNumber, pageSize);
    }
}

public static class TagRepositoryExtensions
{
    /// <summary>
    /// Returns all the tags that have been created by users (aka filters out system generated tags).
    /// </summary>
    /// <param name="tagRepository">The <see cref="ITagRepository"/>.</param>
    /// <param name="organization">The <see cref="Organization"/>.</param>
    /// <returns></returns>
    public static async Task<IReadOnlyList<Tag>> GetAllUserTagsAsync(this ITagRepository tagRepository,
        Organization organization)
    {
        var tags = await tagRepository.GetAllAsync(organization);
        return tags.Where(t => !t.Generated).ToList();
    }
}
