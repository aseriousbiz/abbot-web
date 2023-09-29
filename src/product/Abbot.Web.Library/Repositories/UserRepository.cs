using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Extensions;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Repositories;

public class UserRepository : IUserRepository
{
    static readonly ILogger<UserRepository> Log = ApplicationLoggerFactory.CreateLogger<UserRepository>();

    readonly AbbotContext _db;
    readonly IClock _clock;

    static readonly Counter<long> UserUpdateCount = AbbotTelemetry.Meter.CreateCounter<long>(
        "users.ensure.count",
        "milliseconds",
        "The number of user 'ensures'.");

    static readonly Histogram<long> UserUpdateDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "users.ensure.duration",
        "milliseconds",
        "The time it takes to 'ensure' a user.");

    /// <summary>
    /// Constructs a <see cref="UserRepository"/> with some useful injected services.
    /// </summary>
    /// <param name="db">The <see cref="AbbotContext"/>.</param>
    /// <param name="clock">The clock.</param>
    public UserRepository(AbbotContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<User?> GetUserByPlatformUserId(string platformUserId)
    {
        return await GetUserQueryable().SingleOrDefaultAsync(u => u.PlatformUserId == platformUserId);
    }

    public async Task<Member?> GetMemberByPlatformUserId(string platformUserId, Organization organization)
    {
        return await GetMemberQueryable()
            .Where(m => m.User.PlatformUserId == platformUserId)
            .SingleEntityOrDefaultAsync();
    }

    public async Task<Member?> GetMemberByApiToken(string token)
    {
        return await GetMemberQueryable()
            .Include(m => m.ApiKeys)
            .Where(m => m.ApiKeys.Any(key => key.Token == token))
            .SingleEntityOrDefaultAsync();
    }

    public async Task<User> GetBillingUserByEmailAsync(string? email, Organization organization)
    {
        if (email is not null)
        {
            // First, we try users.
            var member = await GetMemberQueryable(organization)
                .Where(m => m.User.Email == email)
                .FirstOrDefaultAsync();
            if (member is not null)
            {
                return member.User;
            }

            var billingMatch = await GetMemberQueryable(organization)
                .Where(m => m.BillingEmail == email)
                .SingleEntityOrDefaultAsync();

            if (billingMatch is not null)
            {
                return billingMatch.User;
            }
        }

        return await EnsureBillingUserAsync();
    }

    public async Task<Member?> GetMemberByIdAsync(Id<Member> id, Id<Organization> organizationId)
    {
        return await GetMemberQueryable(organizationId)
            .SingleEntityOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Member?> GetMemberByIdAsync(Id<Member> id)
    {
        return await GetMemberQueryable()
            .SingleEntityOrDefaultAsync(m => m.Id == id.Value);
    }

    public async Task<Member?> GetByPlatformUserIdAsync(string platformUserId, Organization organization)
    {
        return await GetMemberQueryable(organization)
            .SingleEntityOrDefaultAsync(u => u.User.PlatformUserId == platformUserId);
    }

    public async Task<Member?> GetMemberByEmailAsync(Organization organization, string email)
    {
        IQueryable<Member> queryable = GetMemberQueryable(organization);
        if (organization.PlatformType == PlatformType.Slack)
        {
            // If we have the SlackTeamId, use it to fix up bad old data.
            queryable = queryable.Where(m => m.User.SlackTeamId == null || m.User.SlackTeamId == organization.PlatformId);
        }
        var results = await queryable
            .Where(u => u.User.Email == email)
            .Take(100) // Just to prevent a huge amount of data suddenly being returned.
            .ToListAsync();

        if (results.Count == 0)
        {
            return null;
        }

        if (results.Count > 1)
        {
            var ids = string.Join(", ", results.Select(m => $"{m.Id}"));
            // Bad data! We have some cases where there are multiple members in an org with the same email.
            // This is actually pretty unlikely, as it primarily happened to our org while we were testing foreign org things
            // So just return no result, and log it.
            Log.DuplicateUsersByEmailFound(organization.Id, organization.PlatformId, ids);

            // We don't want to completely blow up whatever is going on. So just return no result.
            // We've done enough to notify Staff to come try to fix it up.
            return null;
        }

        return results[0];
    }

    public async Task UpdateUserAsync()
    {
        await _db.SaveChangesAsync();
    }

    public async Task UpdateWorkingHoursAsync(
        Member member,
        WorkingHours workingHours,
        WorkingDays workingDays)
    {
        var workingHoursChanged = member.WorkingHours != workingHours || member.Properties.WorkingDays != workingDays;

        member.WorkingHours = workingHours;
        member.Properties = member.Properties with
        {
            WorkingDays = workingDays
        };

        if (workingHoursChanged)
        {
            // UPDATE Pending notifications for this member.
            var pendingNotifications = await _db.PendingMemberNotifications
                .Where(n => n.MemberId == member.Id)
                .Where(n => n.DateSentUtc == null)
                .Where(n => n.NotBeforeUtc != null)
                .ToListAsync();

            var notBeforeUtc = member.GetNextWorkingHoursStartDateUtc(_clock.UtcNow);
            foreach (var pendingNotification in pendingNotifications)
            {
                pendingNotification.NotBeforeUtc = notBeforeUtc;
            }
        }

        await UpdateUserAsync();
    }

    public async Task<Member?> GetCurrentMemberAsync(ClaimsPrincipal principal)
    {
        var (platformUserId, platformTeamId) = GetPlatformUserAndTeamIds(principal);
        var currentUser = await GetMemberQueryable()
            .Where(m => m.Organization.PlatformId == platformTeamId)
            .SingleEntityOrDefaultAsync(m => m.User.PlatformUserId == platformUserId);

        return currentUser;
    }

    public async Task<ApiKey> CreateApiKeyAsync(string name, int expiresInDays, Member owner)
    {
        var apiKey = new ApiKey
        {
            Name = name,
            Owner = owner,
            ExpiresIn = expiresInDays,
            Created = _clock.UtcNow
        };
        SetToken(apiKey);
        await _db.ApiKeys.AddAsync(apiKey);
        await _db.SaveChangesAsync();
        return apiKey;
    }

    public async Task RegenerateApiKeyAsync(ApiKey apiKey)
    {
        SetToken(apiKey);
        apiKey.Created = _clock.UtcNow;
        await _db.SaveChangesAsync();
    }

    static void SetToken(ApiKey apiKey)
    {
        apiKey.Token = TokenCreator.CreateStrongAuthenticationToken("abk");
    }

    public Task DeleteApiKeyAsync(ApiKey apiKey, Member member)
    {
        _db.Remove(apiKey);
        return _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ApiKey>> GetApiKeysAsync(Member member)
    {
        // We can't rely on `member.ApiKeys` being null to determine if the collection is loaded.
        // Also, LoadAsync() already checks `IsLoaded`. See: https://haacked.com/archive/2022/09/30/ef-core-collection-pitfalls/
        await _db.Entry(member).Collection(m => m.ApiKeys).LoadAsync();
        return member.ApiKeys;
    }

    static (string, string) GetPlatformUserAndTeamIds(ClaimsPrincipal principal)
    {
        var platformUserId = principal.GetPlatformUserId()
            ?? throw new InvalidOperationException("Platform User Id Claim Not Set.");
        var platformTeamId = principal.GetPlatformTeamId()
            ?? throw new InvalidOperationException("Platform Team Id Claim Not Set.");
        return (platformUserId, platformTeamId);
    }

    public async Task<Member> EnsureCurrentMemberWithRolesAsync(ClaimsPrincipal principal, Organization organization)
    {
        var nameIdentifier = principal.GetNameIdentifier();
        var platformUserId = principal.GetPlatformUserId();
        if (platformUserId is null)
        {
            throw new InvalidOperationException($"Platform User ID claim is null. Authenticated: {principal.IsAuthenticated()}, NameIdentifier: {nameIdentifier}");
        }

        void UpdateMember(Member member, EntityEnsureState userEntityState, EntityEnsureState memberEntityState)
        {
            var user = member.User;
            if (userEntityState == EntityEnsureState.Creating)
            {
                // Only set this if we're creating the user from the ClaimsPrincipal. Otherwise we'd set this
                // via Slack and that would have the actual correct value.
                user.DisplayName = principal.GetName() ?? "unknown";

                // Leave null to fill in from Slack API
                user.RealName = null;
            }
            user.NameIdentifier ??= nameIdentifier;
            user.Email = principal.GetEmail() ?? user.Email;
            if (user.Avatar != principal.GetAvatar())
            {
                user.Avatar = principal.GetAvatar() ?? user.Avatar;
            }
        }

        return await EnsureMemberAsync(
            platformUserId,
            null,
            organization,
            UpdateMember);
    }

    /// <summary>
    /// Ensures the <see cref="Member"/> exists for the specified <see cref="User"/> and <see cref="Organization"/>.
    /// If not, creates the <see cref="Member"/>. Then updates the <see cref="User"/> and <see cref="Member"/>
    /// from the specified <see cref="UserEventPayload"/>.
    /// </summary>
    /// <param name="userEventPayload">The <see cref="UserEventPayload"/> with information about a user change event coming from the chat platform.</param>
    /// <param name="user">The <see cref="User" /> to update the member for.</param>
    /// <param name="userOrganization">The organization the user belongs to.</param>
    public async Task<Member> EnsureMemberAsync(User? user, UserEventPayload userEventPayload, Organization userOrganization)
    {
        void UpdateMember(Member member, EntityEnsureState userEntityState, EntityEnsureState memberEntityState)
        {
            member.UpdateMemberInstanceFromUserEventPayload(userEventPayload);
        }

        return await EnsureMemberAsync(
            userEventPayload.Id,
            user,
            userOrganization,
            UpdateMember);
    }

    /// <summary>
    /// Ensures the <see cref="User"/> and <see cref="Member"/> exists for the organization. If not, creates the
    /// <see cref="User"/> and <see cref="Member"/>. Then updates the <see cref="User"/> and <see cref="Member"/>
    /// from the specified <see cref="UserEventPayload"/>.
    /// </summary>
    /// <param name="userEventPayload">The <see cref="UserEventPayload"/> with information about a user change event coming from the chat platform.</param>
    /// <param name="userOrganization">The organization the user belongs to.</param>
    public async Task<Member> EnsureAndUpdateMemberAsync(UserEventPayload userEventPayload,
        Organization userOrganization)
        => await EnsureMemberAsync(user: null, userEventPayload, userOrganization);

    async Task<Member> EnsureMemberAsync(
        string platformUserId,
        User? user,
        Organization userOrganization,
        Action<Member, EntityEnsureState, EntityEnsureState>? updateMemberMethod)
    {
        var metricTags = AbbotTelemetry.CreateOrganizationTags(userOrganization);
        var stopwatch = Stopwatch.StartNew();

        if (platformUserId.Length is 0 || platformUserId[0] is not ('U' or 'W'))
        {
            // We DO sometimes create a ChannelUser with a non-'U' or 'W' identifier in Slack.
            // The main example being for `bot_added`, where the message doesn't come from anyone specific.
            // We put the bot's `Bxxxxxxx` identifier in as the user ID there.
            // That code path should never end up calling here, but to be absolutely sure, we have this check in place.
            throw new InvalidOperationException(
                $"Shouldn't ever be trying to create a user for a Slack identifier that doesn't start 'U' or 'W'. Identifier {platformUserId} found.");
        }

        EntityEnsureState userEntityState = EntityEnsureState.Existing;
        user ??= await GetUsersQueryableWithRoomAssignments()
            .FirstOrDefaultAsync(u => u.PlatformUserId == platformUserId);

        if (user is null)
        {
            Log.UserNotFound(platformUserId);
            user = new User
            {
                PlatformUserId = platformUserId
            };
            userEntityState = EntityEnsureState.Creating;
        }
        else
        {
            Log.UserFound(user.Id, platformUserId);
        }
        metricTags.Add("user_entity_state", userEntityState.ToString());

        var (member, memberEntityState) = user.GetOrCreateMemberInstanceForOrganization(userOrganization);
        metricTags.Add("member_entity_state", memberEntityState.ToString());

        updateMemberMethod?.Invoke(member, userEntityState, memberEntityState);

        if (userOrganization.PlatformType is PlatformType.Slack && user.SlackTeamId is null)
        {
            user.SlackTeamId = userOrganization.PlatformId;
        }

        if (memberEntityState is EntityEnsureState.Creating)
        {
            await _db.Members.AddAsync(member);
        }

        metricTags.Add("user_ef_entity_state", _db.Entry(user).State.ToString());
        metricTags.Add("member_ef_entity_state", _db.Entry(member).State.ToString());

        try
        {
            await _db.SaveChangesAsync();
            metricTags.SetSuccess();
        }
        catch (DbUpdateException e) when (e.GetDatabaseError() is UniqueConstraintError constraintError)
        {
#pragma warning disable CA1508
            if (constraintError is { TableName: "Users", ColumnNames: [var columnName] }
#pragma warning restore CA1508
                && columnName.Equals(nameof(User.PlatformUserId), StringComparison.Ordinal))
            {
                member = await TryReloadExistingMember(platformUserId, user, member, userOrganization);
                if (member is not null)
                {
                    Log.RecoveredFromDuplicateUserException(e, platformUserId, userOrganization.PlatformId);
                    metricTags.Add("recovered", true);
                    return member;
                }

                Log.ExceptionDuplicateUser(e, platformUserId, userOrganization.PlatformId);
            }

            metricTags.SetFailure(e);
            throw;
        }
        catch (Exception ex)
        {
            metricTags.SetFailure(ex);
        }
        finally
        {
            UserUpdateCount.Add(1, metricTags);
            UserUpdateDuration.Record(stopwatch.ElapsedMilliseconds, metricTags);
        }

        return member;
    }

    async Task<Member?> TryReloadExistingMember(
        string platformUserId,
        User user,
        Member member,
        Organization userOrganization)
    {
        // We've tried to create a new user, but the user already exists. We'll try and recover and
        // reload the existing user. But the attempt to insert the user is still in the change tracker,
        // so any calls to `SaveChangesAsync` will fail. This is why we detach the entity in favor
        // of the one we reload, if any.
        _db.Entry(member).State = EntityState.Detached;

        // HACK! If we have a duplicate exception, we would hope that EF would update these entities.
        // But just in case they don't, we try to run a new query here to retrieve these from the db.
        if (user.Id is 0 || member.Id is 0)
        {
            // Try to retrieve user and member.
            var reloadedUser = await GetUserQueryable().FirstOrDefaultAsync(u => u.PlatformUserId == platformUserId);
            if (reloadedUser is null)
            {
                return null;
            }
            user = reloadedUser;
            var reloadedMember = user.Members.SingleOrDefault(m => m.OrganizationId == userOrganization.Id);

            if (reloadedMember is null)
            {
                return null;
            }

            member = reloadedMember;
        }

        return member.Id is not 0 ? member : null;
    }

    public async Task ArchiveMemberAsync(Member subject, Member actor)
    {
        if (!actor.IsAdministrator())
        {
            throw new InvalidOperationException("Only administrators can archive members.");
        }

        if (!subject.RoomAssignments.IsLoaded)
        {
            await _db.Entry(subject).Collection(m => m.RoomAssignments).LoadAsync();
        }

        subject.ArchiveMemberInstance();
        await _db.SaveChangesAsync();
    }

    public async Task<IPartialList<User>> GetUsersNearAsync(
        Member me,
        Point location,
        double radiusKilometers,
        int count,
        Organization organization)
    {
        var radius = radiusKilometers / 100.00; // Convert to geocode coordinate system
        var queryable = GetMemberQueryable(organization)
            .Where(m => m.Location != null
                        && !m.IsGuest
                        && m.Id != me.Id
                        && m.Location.Distance(location) <= radius)
            .Select(m => m.User);
        var total = await queryable.CountAsync();
        var items = await queryable
            .OrderBy(u => u.DisplayName)
            .Take(count)
            .ToListAsync();
        return new PartialList<User>(items, total);
    }

    public async Task<IReadOnlyList<Member>> FindMembersAsync(
        Organization organization,
        string? nameQuery,
        int limit,
        string? requiredRole = null)
    {
        var query = GetMemberQueryable(organization)
            .Where(m =>
                !m.User.IsBot && !m.IsGuest
                && m.Active
                // For Slack Orgs, we need to make sure their SlackTeamId is not null and matches the org's Team Id.
                && (organization.PlatformType != PlatformType.Slack || m.User.SlackTeamId == organization.PlatformId));
        if (nameQuery is { Length: > 0 })
        {
            nameQuery = nameQuery.ToUpperInvariant();
            // Disabling CA1304 because the ToUpper is actually happening in the database.
#pragma warning disable CA1304
            query = query.Where(m => m.User.DisplayName.ToUpper().Contains(nameQuery) || (m.User.RealName != null && m.User.RealName.ToUpper().Contains(nameQuery)));
#pragma warning restore CA1304
        }

        if (requiredRole is { Length: > 0 })
        {
            query = query.Where(m => m.MemberRoles.Any(r => r.Role.Name == requiredRole));
        }
        query = query.OrderBy(m => m.User.DisplayName).Take(limit);

        var matches = await query.ToListAsync();

        if (nameQuery is { Length: > 0 })
        {
            // Re-sort by a cheap "relevance" score by just putting those with the match at the start of the name first.
            matches = matches.OrderBy(m =>
                    m.User.DisplayName.StartsWith(nameQuery, StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : (m.User.RealName != null && m.User.RealName.StartsWith(nameQuery, StringComparison.OrdinalIgnoreCase))
                            ? 0
                            : 1)
                .ThenBy(m => m.User.DisplayName)
                .ToList();
        }
        return matches;
    }

    public async Task<IReadOnlyList<Member>> GetMembersForTypeAheadQueryAsync(Organization organization, string? memberNameFilter, int limit)
    {
        IQueryable<Member> query = GetActiveMembersQueryable(organization);

        if (memberNameFilter is not null)
        {
            memberNameFilter = memberNameFilter.ToUpperInvariant();
            query = query.Where(m =>
                m.User.DisplayName.ToUpper().Contains(memberNameFilter) ||
                (m.User.RealName != null && m.User.RealName.ToUpper().Contains(memberNameFilter)));
        }
        query = query.OrderBy(m => m.User.DisplayName);

        var limitedQuery = limit > 0
            ? query.Take(limit)
            : query;

        return await limitedQuery.ToListAsync();
    }

    public IQueryable<Member> GetActiveMembersQueryable(Organization organization)
    {
        return GetMemberQueryable(organization)
            .Where(m => !m.User.IsBot && !m.IsGuest && m.Active && m.MemberRoles.Any())
            .Where(m => m.User.PlatformUserId != "USLACKBOT"); // See https://github.com/aseriousbiz/abbot/issues/4700
    }

    public IQueryable<Member> GetPendingMembersQueryable(Organization organization)
    {
        return GetMemberQueryable(organization)
            .Where(m => !m.User.IsBot
                && !m.IsGuest
                && !m.MemberRoles.Any()
                && m.User.NameIdentifier != null
                // For Slack Orgs, we need to make sure their SlackTeamId is not null and matches the org's Team Id.
                && (organization.PlatformType != PlatformType.Slack || m.User.SlackTeamId == organization.PlatformId)
                && m.Active);
    }

    public IQueryable<Member> GetArchivedMembersQueryable(Organization organization)
    {
        return GetMemberQueryable(organization)
            .Where(m => !m.Active)
            .Where(m => !m.User.IsBot)
            .Where(m => !m.IsGuest)
            .Where(m => m.User.NameIdentifier != null);
    }

    public async Task<Member> EnsureAbbotMemberAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        var abbotMember = await GetExistingAbbotMemberAsync(organization, cancellationToken);
        if (abbotMember is null)
        {
            var abbotUser = await EnsureAbbotUserAsync();
            abbotMember = OrganizationRepository.CreateAbbotBotMemberInstance(abbotUser, organization);
            await _db.Members.AddAsync(abbotMember, cancellationToken);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.GetDatabaseError() is UniqueConstraintError { ColumnNames: [nameof(Member.UserId), nameof(Member.OrganizationId)] })
            {
                Log.RecoveredFromDuplicateAbbotMemberException(e);
                abbotMember = await GetExistingAbbotMemberAsync(organization, cancellationToken).Require();
            }
        }

        return abbotMember;
    }

    async Task<Member?> GetExistingAbbotMemberAsync(Organization organization, CancellationToken cancellationToken)
    {
        return await GetMemberQueryable(organization)
            .Where(m => m.User.IsAbbot)
            // Prefer the installed bot
            .OrderByDescending(m => m.User.PlatformUserId == organization.PlatformBotUserId)
            // If none installed, prefer the system bot
            .ThenByDescending(m => m.User.NameIdentifier == Member.AbbotNameIdentifier)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> EnsureAbbotUserAsync()
    {
        var user = await EnsureSystemBotUserAsync("abbot", "/img/abbot-avatar.png");
        if (!user.IsAbbot)
        {
            user.IsAbbot = true;
            await _db.SaveChangesAsync();
        }
        return user;
    }

    /// <inheritdoc />
    public Task<User> EnsureBillingUserAsync() =>
        EnsureSystemBotUserAsync("billing", "/img/abbot-avatar-medium.png");

    public async Task<User?> GetByIdAsync(Id<User> id)
    {
        return await GetUserQueryable().SingleEntityOrDefaultAsync(e => e.Id == id.Value);
    }

    public async Task<IReadOnlyList<Member>> GetDefaultFirstRespondersAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        return await GetMemberQueryable(organization)
            .Where(m => m.IsDefaultFirstResponder)
            .Where(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Member>> GetDefaultEscalationRespondersAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        return await GetMemberQueryable(organization)
            .Where(m => m.IsDefaultEscalationResponder)
            .Where(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent))
            .ToListAsync(cancellationToken);
    }

    async Task<User> EnsureSystemBotUserAsync(
        string userName,
        string avatar)
    {
        var nameIdentifier = $"system|{userName}";
        var systemUser = await _db.Users
            .Where(u => u.NameIdentifier == nameIdentifier)
            .SingleOrDefaultAsync();
        if (systemUser is null)
        {
            systemUser = new User
            {
                NameIdentifier = nameIdentifier,
                DisplayName = userName,
                RealName = userName,
                Avatar = avatar,
                PlatformUserId = userName,
                IsAbbot = true,
                IsBot = true
            };
            await _db.Users.AddAsync(systemUser);
            await _db.SaveChangesAsync();
        }

        return systemUser;
    }

    IIncludableQueryable<Member, Role> GetMemberQueryable(Id<Organization>? organizationId = null)
    {
        IQueryable<Member> queryable = _db.Members.Include(u => u.User);
        if (organizationId is not null)
        {
            queryable = queryable.Where(m => m.OrganizationId == organizationId.Value);
        }
        return queryable.Include(u => u.Organization)
            .Include(m => m.MemberRoles)
            .ThenInclude(mr => mr.Role);
    }

    IQueryable<User> GetUserQueryable()
    {
        return _db.Users.Include(u => u.Members)
            .ThenInclude(m => m.Organization)
            .Include(u => u.Members)
            .ThenInclude(m => m.MemberRoles)
            .ThenInclude(mr => mr.Role);
    }

    IQueryable<User> GetUsersQueryableWithRoomAssignments()
    {
        return GetUserQueryable()
            .Include(u => u.Members)
            .ThenInclude(m => m.RoomAssignments);
    }
}

static partial class UserRepositoryLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "User not found (PlatformUserId: {PlatformUserId}) Creating user…")]
    public static partial void UserNotFound(this ILogger<UserRepository> logger, string platformUserId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "User found (Id: {UserId}, PlatformUserId: {PlatformUserId})")]
    public static partial void UserFound(this ILogger<UserRepository> logger, int userId, string platformUserId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Unable to recover from duplicate user error (PlatformUserId: {PlatformUserId}, PlatformId: {PlatformId})")]
    public static partial void ExceptionDuplicateUser(
        this ILogger<UserRepository> logger,
        Exception exception,
        string platformUserId,
        string platformId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Critical,
        Message = "Multiple users with the same email address found in organization {OrganizationId} ('{OrganizationPlatformId}'): {DuplicateMemberIds}.")]
    public static partial void DuplicateUsersByEmailFound(
        this ILogger<UserRepository> logger,
        int organizationId,
        string organizationPlatformId,
        string duplicateMemberIds);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Recovered from duplicate user error (PlatformUserId: {PlatformUserId}, PlatformId: {PlatformId})")]
    public static partial void RecoveredFromDuplicateUserException(
        this ILogger<UserRepository> logger,
        Exception exception,
        string platformUserId,
        string platformId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Recovered from duplicate Abbot Member error")]
    public static partial void RecoveredFromDuplicateAbbotMemberException(
        this ILogger<UserRepository> logger,
        Exception exception);
}
