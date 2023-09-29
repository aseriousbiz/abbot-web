using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;

namespace Serious.Abbot.Repositories;

public class LinkedIdentityRepository : ILinkedIdentityRepository
{
    readonly AbbotContext _db;
    readonly IAnalyticsClient _analyticsClient;

    public LinkedIdentityRepository(AbbotContext db, IAnalyticsClient analyticsClient)
    {
        _db = db;
        _analyticsClient = analyticsClient;
    }

    IQueryable<LinkedIdentity> Queryable => _db.LinkedIdentities
        .Include(id => id.Organization)
        .Include(id => id.Member.User);

    public async Task<(LinkedIdentity? Identity, T? Metadata)> GetLinkedIdentityAsync<T>(Organization organization,
        Member member, LinkedIdentityType type) where T : class =>
        ResolveMetadata<T>(await Queryable
            .SingleOrDefaultAsync(li =>
                li.OrganizationId == organization.Id && li.MemberId == member.Id && li.Type == type));

    public async Task<(LinkedIdentity? Identity, T? Metadata)> GetLinkedIdentityAsync<T>(Organization organization,
        LinkedIdentityType type, string externalId)
        where T : class =>
        ResolveMetadata<T>(await Queryable
            .SingleOrDefaultAsync(li =>
                li.OrganizationId == organization.Id && li.Type == type && li.ExternalId == externalId));

    public async Task<EntityResult<LinkedIdentity>> LinkIdentityAsync(
        Organization organization,
        Member member,
        LinkedIdentityType type,
        string externalId,
        string? externalName = null,
        object? externalMetadata = null)
    {
        var duplicate = await _db.LinkedIdentities
            .Where(li => li.Type == type)
            .Where(li => li.OrganizationId == organization.Id)
            .Where(li => li.MemberId == member.Id || li.ExternalId == externalId)
            .FirstOrDefaultAsync();

        if (duplicate is not null)
        {
            return (duplicate.MemberId == member.Id, duplicate.ExternalId == externalId) switch
            {
                (true, true) => duplicate,
                (true, false) =>
                    EntityResult.Conflict($"User already linked to another {type.Humanize()}.", duplicate),
                (false, true) =>
                    EntityResult.Conflict($"Another user is already linked to this {type.Humanize()}.", duplicate),
                _ => throw new UnreachableException(),
            };
        }

        var newIdentity = new LinkedIdentity
        {
            Organization = organization,
            Member = member,
            Type = type,
            ExternalId = externalId,
            ExternalName = externalName,
            ExternalMetadata = JsonConvert.SerializeObject(externalMetadata)
        };

        await _db.LinkedIdentities.AddAsync(newIdentity);
        await _db.SaveChangesAsync();
        _analyticsClient.Track(
            "Integration Identity Linked",
            AnalyticsFeature.Integrations,
            member,
            organization);
        return newIdentity;
    }

    public async Task UpdateLinkedIdentityAsync(LinkedIdentity identity, object? updatedMetadata = null)
    {
        if (updatedMetadata is not null)
        {
            identity.ExternalMetadata = JsonConvert.SerializeObject(updatedMetadata);
        }

        _db.LinkedIdentities.Update(identity);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveIdentityAsync(LinkedIdentity identity)
    {
        _db.LinkedIdentities.Remove(identity);
        _analyticsClient.Track(
            "Integration Identity Unlinked",
            AnalyticsFeature.Integrations,
            identity.Member,
            identity.Organization);
        await _db.SaveChangesAsync();
    }

    public async Task ClearIdentitiesAsync(Organization organization, LinkedIdentityType type)
    {
        var linkedIdentities = await _db.LinkedIdentities
            .Where(i => i.OrganizationId == organization.Id && i.Type == type)
            .ToListAsync();

        _db.LinkedIdentities.RemoveRange(linkedIdentities);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<LinkedIdentity>> GetAllLinkedIdentitiesForMemberAsync(Member subjectMember) =>
        await Queryable
            .Include(id => id.Organization)
            .Where(id => id.MemberId == subjectMember.Id)
            .ToListAsync();

#pragma warning disable CA1822
    (LinkedIdentity? Identity, T? Metadata) ResolveMetadata<T>(LinkedIdentity? identity) where T : class
#pragma warning restore CA1822
    {
        var metadata = identity is { ExternalMetadata.Length: > 0 }
            ? JsonConvert.DeserializeObject<T>(identity.ExternalMetadata)
            : null;

        return (identity, metadata);
    }
}
