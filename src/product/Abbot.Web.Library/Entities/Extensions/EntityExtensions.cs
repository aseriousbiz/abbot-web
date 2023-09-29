using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Compilation;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.AspNetCore;
using Serious.Slack.InteractiveMessages;
using Serious.Text;

namespace Serious.Abbot.Entities;

// The result of ensuring an entity.
public enum EntityEnsureState
{
    /// <summary>
    /// The entity didn't exist and we are creating it.
    /// </summary>
    Creating,

    /// <summary>
    /// The entity is an existing entity.
    /// </summary>
    Existing
}

/// <summary>
/// Helpful extensions on entity classes.
/// </summary>
public static class EntityExtensions
{
    static readonly ILogger Log = ApplicationLoggerFactory.CreateLogger(typeof(EntityExtensions));

    /// <summary>
    /// Tags an EF query with the specified value.
    /// Use this to include _non-secret_ values in the SQL query for debugging purposes.
    /// </summary>
    /// <param name="q">The query to tag.</param>
    /// <param name="value">The value to tag the query with.</param>
    /// <param name="name">An optional name. This will be derived from the expression used in the <paramref name="value"/> parameter if not specified.</param>
    public static IQueryable<TEntity> TagWithValue<TEntity, TValue>(this IQueryable<TEntity> q, TValue value,
        [CallerArgumentExpression(nameof(value))] string? name = null) where TValue : notnull =>
        q.TagWith($"{name ?? "value"}={value}");

    /// <summary>
    /// Converts a list of entities into a list of <see cref="Id{T}"/> values representing the IDs of the entities.
    /// </summary>
    /// <param name="entities">The entities to get IDs for.</param>
    public static IReadOnlyList<Id<T>> ToIds<T>(this IReadOnlyList<T> entities)
        where T : EntityBase<T> =>
        entities.Select(e => (Id<T>)e).ToList();

    /// <summary>
    /// Asserts that the provided entity is associated with the provided organization.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <param name="organizationId">The ID of the <see cref="Organization"/> to which the entity must belong.</param>
    /// <exception cref="InvalidOperationException">Thrown if the entity is not associated with the provided organization.</exception>
    public static void RequireParent<T>(this T entity, Id<Organization> organizationId)
        where T : OrganizationEntityBase<T>
    {
        if (entity.OrganizationId != organizationId)
        {
            throw new InvalidOperationException(
                $"Entity {entity.Id} is not part of organization {organizationId}.");
        }
    }

    /// <summary>
    /// Retrieve the unprotected api token if <see cref="Organization.ApiToken"/> is not null and not empty.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="unprotectedApiToken">The unprotected api token.</param>
    /// <returns></returns>
    public static bool TryGetUnprotectedApiToken(this Organization organization, [NotNullWhen(true)] out string? unprotectedApiToken)
    {
        if (organization is { ApiToken: { Empty: false } apiToken })
        {
            unprotectedApiToken = apiToken.Reveal();
            return true;
        }

        unprotectedApiToken = null;
        return false;
    }

    /// <summary>
    /// Retrieve the unprotected api token if <see cref="SlackAuthorization.ApiToken"/> is not null and not empty.
    /// </summary>
    /// <param name="authorization">The <see cref="SlackAuthorization"/>.</param>
    /// <param name="unprotectedApiToken">The unprotected api token.</param>
    /// <returns></returns>
    public static bool TryGetUnprotectedApiToken(this SlackAuthorization? authorization, [NotNullWhen(true)] out string? unprotectedApiToken)
    {
        if (authorization is { ApiToken: { Empty: false } apiToken })
        {
            unprotectedApiToken = apiToken.Reveal();
            return true;
        }

        unprotectedApiToken = null;
        return false;
    }

    public static Uri GetMessageUrl(this Announcement announcement)
    {
        var organization = announcement.Organization;
        return SlackFormatter.MessageUrl(organization.Domain,
            announcement.SourceRoom.PlatformRoomId,
            announcement.SourceMessageId);
    }

    /// <summary>
    /// Returns <c>true</c> if the organization has a non-empty API token.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns><c>true</c> if the Api Token is not null and not empty.</returns>
    public static bool HasApiToken(this Organization organization)
    {
        return organization is { ApiToken.Empty: false };
    }

    /// <summary>
    /// Returns <c>true</c> if the authorization has a non-empty API token.
    /// </summary>
    /// <param name="authorization">The authorization.</param>
    /// <returns><c>true</c> if the Api Token is not null and not empty.</returns>
    public static bool HasApiToken(this SlackAuthorization? authorization)
    {
        return authorization is { ApiToken.Empty: false };
    }

    /// <summary>
    /// Returns the revealed API token for an organization. If the org does not have an API token, then this throws
    /// an exception.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static string RequireAndRevealApiToken(this Organization organization)
    {
        if (!organization.HasApiToken())
        {
            throw new InvalidOperationException(
                $"Organization {organization.PlatformId} with Bot {organization.PlatformBotUserId} (Id: {organization.Id}) does not have an API token as expected.");
        }

        return organization.ApiToken.Require().Reveal();
    }

    /// <summary>
    /// Returns the revealed API token for an authorization. If the auth does not have an API token, then this throws
    /// an exception.
    /// </summary>
    /// <param name="authorization">The authorization.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static string RequireAndRevealApiToken(this SlackAuthorization authorization)
    {
        if (!authorization.HasApiToken())
        {
            throw new InvalidOperationException(
                $"Authorization {authorization.BotUserId} does not have an API token as expected.");
        }

        return authorization.ApiToken.Require().Reveal();
    }

    /// <summary>
    /// If the organization is one of ours, this returns true.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    public static bool IsSerious(this Organization organization)
    {
        return organization.PlatformId
            is WebConstants.ASeriousBizSlackId
            or WebConstants.FunnyBusinessSlackId;
    }

    /// <summary>
    /// Returns <c>true</c> if the organization default response times set.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns><c>true</c> the default response times are not <c>null</c>.</returns>
    public static bool HasDefaultResponseTimes(this Organization organization)
    {
        return organization.DefaultTimeToRespond.Warning is not null
               || organization.DefaultTimeToRespond.Deadline is not null;
    }

    /// <summary>
    /// Returns the specified UTC date time in the time zone of the member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="utcDateTime">The date time.</param>
    /// <returns>The <see cref="DateTime"/> in the member's timezone.</returns>
    public static DateTime ToTimeZone(this Member member, DateTime utcDateTime)
    {
        var tz = member.TimeZoneId ?? "America/Los_Angeles";
        return utcDateTime.ToLocalDateTimeInTimeZone(tz).ToDateTimeUnspecified();
    }

    /// <summary>
    /// Returns true if the organization is in need of a repair.
    /// </summary>
    /// <param name="organization">The organization to check.</param>
    public static bool NeedsRepair(this Organization organization)
    {
        // Identifies if an organization needs to be "repaired"
        if (organization is { PlatformType: PlatformType.Slack, ApiToken.Empty: false, PlatformId.Length: > 0, PlatformBotId.Length: > 0 })
        {
            // This organization has been installed into Slack and we have a token.
            // So check that all the data we should have is non-null
            return organization.PlatformBotId is not { Length: > 0 } || organization.PlatformBotUserId is not { Length: > 0 } ||
                organization.BotName is not { Length: > 0 } || organization.BotAvatar is not { Length: > 0 } ||
                organization.BotAppName is not { Length: > 0 } || organization.BotAppId is not { Length: > 0 };
        }

        return false;
    }

    /// <summary>
    /// Returns true if the bot is known to be installed for the organization. It's known if Abbot receives a
    /// message from the chat platform.
    /// </summary>
    /// <param name="organization">The organization to check.</param>
    public static bool IsBotInstalled(this Organization organization)
    {
        return organization.PlatformBotId is not null && organization.ApiToken is not null;
    }

    /// <summary>
    /// Returns true if the organization has the required scope.
    /// </summary>
    /// <param name="organization">The organization to check.</param>
    /// <param name="scope">The scope to test for.</param>
    public static bool HasRequiredScope(this Organization organization, string scope)
    {
        return organization is not { PlatformType: PlatformType.Slack }
               || organization.Scopes is not null
               && organization.Scopes.Contains(scope, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if the bot has the required scope.
    /// </summary>
    /// <param name="bot">The bot to check.</param>
    /// <param name="scope">The scope to test for.</param>
    public static bool HasRequiredScope(this BotChannelUser bot, string scope)
    {
        return bot is not SlackBotChannelUser
            || bot.Scopes is not null
            && bot.Scopes.Contains(scope, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if the organization is completely installed. This means the organization's Name, Domain,
    /// and Avatar are all known.
    /// </summary>
    /// <param name="organization">The organization to check.</param>
    public static bool IsComplete(this Organization organization)
    {
        return organization is not { Name: null }
            and not { PlanType: PlanType.None }
            and not { Domain: null }
            and not { Avatar: Organization.DefaultAvatar };
    }

    /// <summary>
    /// Retrieve the bot name for the organization or the default "abbot" name.
    /// </summary>
    /// <param name="organization">The organization</param>
    /// <returns>The bot name</returns>
    public static string GetBotName(this Organization? organization)
    {
        return organization?.BotName ?? "abbot";
    }

    /// <summary>
    /// Retrieves the URL to configure the Abbot Slack App installation. The main reason we link to this is
    /// so Slack admins can change the bot name for Abbot.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="slackOptions">The Slack options.</param>
    /// <returns></returns>
    public static Uri? GetSlackAppConfigurationUrl(this Organization organization, SlackOptions slackOptions)
    {
        if (organization is not { BotAppName.Length: > 0, Domain.Length: > 0, BotAppId.Length: > 0 })
        {
            return null;
        }

        var urlFormat = slackOptions.AppConfigurationUrl ?? "https://{DOMAIN}/apps/{APP_ID}-{BOT_APP_NAME}?tab=settings";
        var sanitizedAppName =
            Regex.Replace(
                Regex.Replace(
                    organization.BotAppName,
                    @"[^\w\s-]+", ""),
                    @"[\s-]+", "-")
            .ToLowerInvariant();
        var url = urlFormat.Replace("{DOMAIN}", organization.Domain, StringComparison.OrdinalIgnoreCase)
            .Replace("{APP_ID}", organization.BotAppId, StringComparison.OrdinalIgnoreCase)
            .Replace("{BOT_APP_NAME}", sanitizedAppName, StringComparison.OrdinalIgnoreCase);
        return new Uri(url);
    }

    /// <summary>
    /// Gets a boolean indicating whether the organization has all of the provided <see cref="PlanFeature"/>s enabled.
    /// </summary>
    /// <param name="organization">The organization</param>
    /// <param name="feature">The plan feature to test.</param>
    public static bool HasPlanFeature(this Organization organization, PlanFeature feature) =>
        organization.GetPlan().HasFeature(feature);

    /// <summary>
    /// Gets a <see cref="Plan"/> describing the features and limits for the plan this organization is on.
    /// </summary>
    /// <param name="organization">The organization</param>
    public static Plan GetPlan(this Organization organization)
    {
        if (organization.Trial is { Plan: var trialPlan, Expiry: var expiry } && expiry > DateTimeOffset.UtcNow)
        {
            return trialPlan.GetFeatures();
        }

        return organization.PlanType.GetFeatures();
    }

    /// <summary>
    /// Returns <c>true</c> if the organization is on a trial plan that is active.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="utcNow">The current UTC date and time.</param>
    public static bool IsActiveTrialPlan(this Organization organization, DateTime utcNow)
    {
        return organization.Trial is { Expiry: var expiry } && expiry > utcNow;
    }

    /// <summary>
    /// Returns <c>true</c> if the organization is allowed to add an agent based on their plan or their purchased
    /// agent count.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="agentCount">The current agent count.</param>
    /// <param name="utcNow">The current date.</param>
    public static bool CanAddAgent(this Organization organization, int agentCount, DateTime utcNow)
    {
        return organization.PurchasedSeatCount > agentCount
               || organization.IsActiveTrialPlan(utcNow)
               || organization.PlanType is PlanType.Free
                   or PlanType.Team
                   or PlanType.Beta
                   or PlanType.Unlimited
                   or PlanType.FoundingCustomer;
    }

    /// <summary>
    /// Creates a <see cref="PlatformUser" /> from a <see cref="Member" /> instance.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>A <see cref="PlatformUser" />.</returns>
    public static PlatformUser ToPlatformUser(this Member member)
    {
        var user = member.User;
        return new PlatformUser(user.PlatformUserId,
            user.DisplayName,
            user.DisplayName,
            user.Email,
            member.TimeZoneId,
            member.FormattedAddress,
            member.Location?.X,
            member.Location?.Y,
            member.WorkingHours);
    }

    public static PlatformUser ToPlatformUser(this User user)
    {
        return new PlatformUser(user.PlatformUserId,
            user.DisplayName,
            user.DisplayName,
            user.Email);
    }

    public static PackageVersion GetLatestVersion(this Package package)
    {
        return package.Versions.OrderByDescending(p => p.Created).First();
    }

    public static PackageVersion CreateNextVersion(this PackageVersion version, ChangeType changeType)
    {
        var (major, minor, patch) = changeType switch
        {
            ChangeType.Major => (version.MajorVersion + 1, 0, 0),
            ChangeType.Minor => (version.MajorVersion, version.MinorVersion + 1, 0),
            ChangeType.Patch => (version.MajorVersion, version.MinorVersion, version.PatchVersion + 1),
            _ => throw new InvalidOperationException($"Unexpected change type {changeType}")
        };

        return new()
        {
            MajorVersion = major,
            MinorVersion = minor,
            PatchVersion = patch,
            Created = DateTime.UtcNow,
            Package = version.Package
        };
    }

    public static string ToVersionString(this PackageVersion version)
    {
        return $"{version.MajorVersion}.{version.MinorVersion}.{version.PatchVersion}";
    }

    /// <summary>
    /// Returns a string containing the code to retrieve a secret.
    /// </summary>
    /// <param name="language">The language for the code sample</param>
    /// <param name="secretName">The name of the secret to retrieve</param>
    /// <returns></returns>
    public static string FormatSecretUsage(this CodeLanguage language, string secretName)
    {
        return language switch
        {
            CodeLanguage.CSharp => $"await Bot.Secrets.GetAsync(\"{secretName}\")",
            CodeLanguage.Python => $"bot.secrets.read(\"{secretName}\")",
            CodeLanguage.JavaScript => $"await bot.secrets.read(\"{secretName}\")",
            CodeLanguage.Ink => $"uuuh good question",
            _ => throw new InvalidOperationException($"Unknown code language {language}")
        };
    }

    public static string ToSlug(this CodeLanguage language)
    {
        return language.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Returns true if this skill was installed from a package and has code
    /// changes that diverge from the package source. If it diverges, we want
    /// to warn users that updates may cause them to lose changes.
    /// </summary>
    /// <param name="skill">The skill.</param>
    public static bool DivergesFromPackage(this Skill skill)
    {
        return skill.SourcePackageVersion is not null
               && skill.SourcePackageVersion.Package.Versions.All(
                   v => !v.CodeCacheKey.Equals(skill.CacheKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns true if this skill has an associated published package and there
    /// have been changes to the skill since the package was last published.
    /// </summary>
    /// <param name="skill">The skill</param>
    /// <param name="package">The published package for the skill.</param>
    public static bool HasUnpublishedChanges(this Skill skill, Package package)
    {
        return skill.UsageText != package.UsageText
               || skill.Description != package.Description
               || skill.Code != package.Code;
    }

    public static string GetPreposition(this AuditOperation auditOperation)
    {
        return auditOperation switch
        {
            AuditOperation.Removed => "from",
            _ => "for"
        };
    }

    public static string GetTriggerTypeRouteParameter(this SkillTrigger trigger)
    {
        return trigger switch
        {
            SkillHttpTrigger => "http",
            SkillScheduledTrigger => "schedule",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Retrieves the <see cref="User"/> <see cref="Member"/> for the specified <see cref="Organization"/>.
    /// If it doesn't exist, creates a <see cref="Member"/> instance and adds it to the <see cref="User"/>.
    /// </summary>
    /// <param name="user">The existing user.</param>
    /// <param name="organization">The organization the user belongs to.</param>
    public static (Member, EntityEnsureState) GetOrCreateMemberInstanceForOrganization(
        this User user,
        Organization organization)
    {
        var platformUserId = user.PlatformUserId;

        // Does this user have a membership to their own organization?
        var member = user.Members.SingleOrDefault(m => m.OrganizationId == organization.Id);

        if (member is not null)
        {
            return (member, EntityEnsureState.Existing);
        }

        // Nope, create user membership.
        Log.UserHasNoMembershipToOrganization(user.Id, platformUserId, organization.PlatformId);
        member = new Member
        {
            Organization = organization,
            Active = true,
            User = user,
        };

        user.Members.Add(member);
        return (member, EntityEnsureState.Creating);
    }

    /// <summary>
    /// Updates the properties of <see cref="Member"/> from the details in the <see cref="UserEventPayload"/>, but does
    /// not save the changes yet.
    /// </summary>
    /// <param name="member">The existing member.</param>
    /// <param name="userEvent">The incoming user change event.</param>
    public static void UpdateMemberInstanceFromUserEventPayload(
        this Member member,
        UserEventPayload userEvent)
    {
        // Update common information we'll store for all users, including foreign org users.
        var user = member.User;
        // Once Abbot; always Abbot
        user.IsAbbot |= userEvent.Id == member.Organization.PlatformBotUserId;
        user.Avatar = userEvent.Avatar ?? user.Avatar;
        user.DisplayName = userEvent.DisplayName;
        user.RealName = userEvent.RealName;

        // USLACKBOT doesn't appear as IsBot?!
        // https://github.com/aseriousbiz/abbot/issues/4700
        user.IsBot = userEvent.IsBot || userEvent.Id == "USLACKBOT";

        member.TimeZoneId = userEvent.TimeZoneId ?? member.TimeZoneId;
        member.IsGuest = userEvent.IsGuest;

        if (!userEvent.Deleted)
        {
            var email = userEvent.Email ?? user.Email;
            user.Email = member.Organization.PlanType is not PlanType.None
                && EmailMatchesOrganizationCanonicalEmailDomain(email, member.Organization)
                && !userEvent.IsGuest
                    ? email
                    : null;
        }
        else
        {
            // Deactivate the user if this is a delete event.
            member.ArchiveMemberInstance();
        }
    }

    internal static void ArchiveMemberInstance(this Member subject)
    {
        subject.User.Email = null;
        subject.MemberRoles.Clear();
        subject.Active = false;
        subject.RoomAssignments.Clear();
        subject.IsDefaultFirstResponder = false;
        subject.IsDefaultEscalationResponder = false;
    }

    static readonly Dictionary<string, HashSet<string>> EmailDomainMap = new()
    {
        [WebConstants.PulumiSlackId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pulumi.com" },
    };

    static bool EmailMatchesOrganizationCanonicalEmailDomain(string? email, Organization organization)
    {
        // Ok, this is hacky, but so far only one customer has complained and
        // we may want to do the ugly fast thing for now and do the right thing later.
        return !EmailDomainMap.TryGetValue(organization.PlatformId, out var emailDomains)
               || emailDomains.Contains(email?.RightAfter('@') ?? "");
    }

    /// <summary>
    /// Creates a new Skill instance that's a copy of the supplied skill, but with specified name and code.
    /// This is used by the Bot Console and CLI to run skill code that's in the process of being edited.
    /// </summary>
    /// <param name="skill">The skill</param>
    /// <param name="name">The new skill name.</param>
    /// <param name="code">The code to use</param>
    public static Skill CopyInstanceWithNewNameAndCode(this Skill skill, string name, string code)
    {
        return new Skill
        {
            Id = skill.Id,
            Name = name,
            Language = skill.Language,
            Code = code,
            CacheKey = SkillCompiler.ComputeCacheKey(code),
            Organization = skill.Organization
        };
    }

    /// <summary>
    /// Retrieve the usage text for the skill, replacing special replacement strings such as {bot} and
    /// {skill} with the relevant values.
    /// </summary>
    /// <param name="skill">The skill.</param>
    public static string GetUsageText(this Skill skill)
    {
        return skill.UsageText
            .Replace("{skill}", skill.Name, StringComparison.Ordinal)
            .Replace("{bot}", $"@{skill.Organization.GetBotName()}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Retrieve the usage text for the package, replacing special replacement strings such as {bot} and
    /// {skill} with the relevant values.
    /// </summary>
    /// <param name="package">The package.</param>
    /// <param name="botName">The Abbot bot name for the current user.</param>
    public static string GetUsageText(this Package package, string botName)
    {
        return package.UsageText
            .Replace("{skill}", package.Skill.Name, StringComparison.Ordinal)
            .Replace("{bot}", $"@{botName}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets a string suitable for use in audit logs.
    /// </summary>
    public static string ToAuditLogString(this Room room)
    {
        var name = room.Name is { Length: > 0 }
            ? $"{room.Name.EnsurePrefix('#').ToMarkdownInlineCode()}"
            : "_a channel with an unknown name_";
        return $"{name} ({room.PlatformRoomId.ToMarkdownInlineCode()})";
    }

    public static IEnumerable<Member> GetFirstResponders(this Room? room) =>
        room.GetRoleMembers(RoomRole.FirstResponder);

    public static IEnumerable<Member> GetEscalationResponders(this Room? room) =>
        room.GetRoleMembers(RoomRole.EscalationResponder);

    static IEnumerable<Member> GetRoleMembers(this Room? room, RoomRole roomRole)
    {
        if (room is null)
        {
            return Enumerable.Empty<Member>();
        }

        return room.Assignments.Require()
            .Where(a => IsRoomAssignmentRole(a, roomRole))
            .Where(a => a.Member.Active)
            .Select(a => a.Member);
    }

    static bool IsRoomAssignmentRole(RoomAssignment assignment, RoomRole role)
    {
        // TODO: Ensure that the member is an agent. However, we have to make sure we load the member's
        // room roles if we're going to do that.
        // assignment.Member.IsAgent()
        return assignment.Role == role;
    }

    /// <summary>
    /// Returns <c>true</c> if the specified entity is in the list, comparing only by Id.
    /// </summary>
    /// <param name="entities">The set of entities.</param>
    /// <param name="entity">The entity to look for.</param>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns></returns>
    public static bool ContainsEntity<TEntity>(this IEnumerable<TEntity> entities, TEntity entity) where TEntity : IEntity
    {
        return entities.Any(e => e.Id == entity.Id);
    }

    /// <summary>
    /// Returns a set of rooms as a textual list.
    /// </summary>
    /// <param name="rooms">A set of rooms.</param>
    public static string ToRoomList(this IEnumerable<Room> rooms)
    {
        return string.Join(", ", rooms.Select(r => $"#{r.Name}"));
    }

    /// <summary>
    /// Returns a set of rooms as a textual list.
    /// </summary>
    /// <param name="messages">A set of announcement messages.</param>
    public static string ToRoomList(this IEnumerable<AnnouncementMessage> messages)
    {
        return messages.Select(r => r.Room).ToRoomList();
    }

    /// <summary>
    /// Returns a set of rooms as a list of room mentions.
    /// </summary>
    /// <param name="messages">A set of announcement messages.</param>
    /// <param name="useLinkForPrivateRoom">If the room is private, use a link to the room instead of a room mention</param>
    public static string ToRoomMentionList(this IEnumerable<AnnouncementMessage> messages, bool useLinkForPrivateRoom = false)
    {
        return messages.Select(r => r.Room)
            .Select(r => r.ToMention(useLinkForPrivateRoom))
            .Humanize();
    }

    public static async Task<SlackMessage?> GetMessageAsync(
        this IConversationsApiClient conversationsApiClient,
        Announcement announcement)
    {
        if (!announcement.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            throw new InvalidOperationException("Organization does not have an api token");
        }

        var (channel, timestamp) = (announcement.SourceRoom.PlatformRoomId, announcement.SourceMessageId);

        var message = await conversationsApiClient.GetConversationAsync(
            apiToken,
            channel,
            timestamp);

        if (message is null)
        {
            Log.MessageIsNull(channel, timestamp);
        }

        if (message?.Text is null)
        {
            Log.MessageTextIsNull(channel, timestamp);
        }

        return message;
    }
}

public static partial class EntityExtensionsLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Message is null for Timestamp: {Timestamp} in Channel: {Channel}")]
    public static partial void MessageIsNull(this ILogger logger, string timestamp, string channel);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Message.Text is null for Timestamp: {Timestamp} in Channel: {Channel}")]
    public static partial void MessageTextIsNull(this ILogger logger, string timestamp, string channel);
}
