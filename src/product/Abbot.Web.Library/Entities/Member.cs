using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement.FeatureFilters;
using NetTopologySuite.Geometries;
using NodaTime;
using NodaTime.Extensions;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Security;
using Serious.Abbot.Serialization;
using Serious.EntityFrameworkCore;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a user in the system.
/// </summary>
[DebuggerDisplay("{DisplayName} (PlatformUserId: {User.PlatformUserId} PlatformId: {Organization.PlatformId})")]
#pragma warning disable CA1067
public class Member : OrganizationEntityBase<Member>, IEquatable<Member>, IWorker, IFeatureActor
#pragma warning restore CA1067
{
    public Member()
    {
        ApiKeys = new EntityList<ApiKey>();
        RoomAssignments = new EntityList<RoomAssignment>();
        AssignedConversations = new EntityList<Conversation>();
        LinkedIdentities = new EntityList<LinkedIdentity>();
    }

    // Special constructor called by EF Core.
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedMember.Local
    Member(DbContext db)
    {
        ApiKeys = new EntityList<ApiKey>(db, this, nameof(ApiKeys));
        RoomAssignments = new EntityList<RoomAssignment>(db, this, nameof(RoomAssignments));
        AssignedConversations = new EntityList<Conversation>(db, this, nameof(AssignedConversations));
        LinkedIdentities = new EntityList<LinkedIdentity>(db, this, nameof(LinkedIdentities));
    }

    public const string AbbotNameIdentifier = "system|abbot";

    /// <summary>
    /// The set of roles this user belongs to.
    /// </summary>
    public IList<MemberRole> MemberRoles { get; set; } = new List<MemberRole>();

    /// <summary>
    /// The set of rooms this member is assigned to. This is not loaded by default.
    /// </summary>
    public EntityList<RoomAssignment> RoomAssignments { get; set; }

    /// <summary>
    /// The set of <see cref="Conversation"/> instances this member is assigned to. This is not loaded by default.
    /// </summary>
    public EntityList<Conversation> AssignedConversations { get; set; }

    /// <summary>
    /// A set of facts stored for this member via the `who` skill.
    /// </summary>
    public IList<MemberFact> Facts { get; set; } = new List<MemberFact>();

    /// <summary>
    /// The set of API Keys for this member.
    /// </summary>
    public EntityList<ApiKey> ApiKeys { get; set; }

    /// <summary>
    /// The collection of <see cref="LinkedIdentity"/> for this user. Generally not included by default.
    /// </summary>
    public EntityList<LinkedIdentity> LinkedIdentities { get; set; }

    /// <summary>
    /// The date this member requested access to the Abbot website.
    /// </summary>
    public DateTimeOffset? AccessRequestDate { get; set; }

    /// <summary>
    /// Denotes whether a member is active in the system or is an archived member.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// The user this member is associated with.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// The Id of the user this member is associated with.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Real name for the user in this organization.
    /// </summary>
    public string RealName => User.RealName ?? User.DisplayName;

    /// <summary>
    /// Display name for the user in this organization.
    /// </summary>
    public string DisplayName => User.DisplayName;

    /// <summary>
    /// True if the user was welcomed by Abbot via the app_home_opened event. This event
    /// is raised when the member clicks on Abbot.
    /// </summary>
    public bool Welcomed { get; set; }

    /// <summary>
    /// Whether or not the user is an admin on the chat platform. This does not necessarily mean they are
    /// an Abbot admin.
    /// </summary>
    public bool PlatformAdmin { get; set; }

    /// <summary>
    /// If this member purchased a subscription, this is the email address they used with Stripe Checkout.
    /// </summary>
    public string? BillingEmail { get; set; }

    /// <summary>
    /// The location for the user that the user tells Abbot.
    /// </summary>
    [Column(TypeName = "geometry (point)")]
    public Point? Location { get; set; }

    /// <summary>
    /// The formatted address from the geo code service when the user tells Abbot their location.
    /// </summary>
    public string? FormattedAddress { get; set; }

    /// <summary>
    /// The IANA Time Zone identifier for the member as reported by the chat platform.
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// The working hours during the day for the user.
    /// </summary>
    public WorkingHours? WorkingHours { get; set; }

    /// <summary>
    /// If <c>true</c>, the user is a guest account to the organization.
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// If <c>true</c>, the member is a default first responder for the organization.
    /// </summary>
    public bool IsDefaultFirstResponder { get; set; }

    /// <summary>
    /// If <c>true</c>, the member is a default escalation responder for the organization.
    /// </summary>
    public bool IsDefaultEscalationResponder { get; set; }

    /// <summary>
    /// The date the user was invited into the Abbot. For organizations that do not auto-approve new agents,
    /// this being set will indicate that the user has been invited and auto-approve the user.
    /// </summary>
    public DateTime? InvitationDate { get; set; }

    /// <summary>
    /// Additional settings for the <see cref="Member"/>.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public MemberProperties Properties { get; set; } = new();

    /// <summary>
    /// Deconstructs the Member into its constituent <see cref="User"/> and <see cref="Organization" />
    /// </summary>
    /// <param name="user">The user that is a member of the organization.</param>
    /// <param name="organization">The organization.</param>
    public void Deconstruct(out User user, out Organization organization)
    {
        user = User;
        organization = Organization;
    }

    /// <summary>
    /// Returns a string with the Slack user mention syntax for the member.
    /// </summary>
    /// <remarks>
    /// For now, we just use Slack as that's where our priorities lie.
    /// </remarks>
    /// <returns>The Slack formatted user mention.</returns>
    public string ToMention()
    {
        return User.ToMention();
    }

    DateTimeZone? _dateTimeZone;

    /// <summary>
    /// The timezone for the member.
    /// </summary>
    [NotMapped]
    public DateTimeZone? TimeZone
    {
        get {
            return _dateTimeZone ??= GetTimeZoneById(TimeZoneId);
        }
    }

    static DateTimeZone? GetTimeZoneById(string? id) => id is null ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);

    /// <summary>
    /// Returns <c>true</c> if the other <see cref="Member"/> is the same instance as this one, or has the same
    /// non-zero Id as this one.
    /// </summary>
    /// <remarks>
    /// We should do this on all entities, but that's a bigger change than I want to take on right now.
    /// </remarks>
    /// <param name="other">The <see cref="Member"/> to compare to this one.</param>
    public bool Equals(Member? other)
    {
        return ReferenceEquals(this, other) || Id != 0 && Id == other?.Id;
    }

    public TargetingContext GetTargetingContext() =>
        Organization.CreateTargetingContext(User.PlatformUserId);
}

/// <summary>
/// Additional properties for a member. We may want to move existing columns into here some day.
/// </summary>
/// <param name="Notifications">Notification settings for the <see cref="Member"/>.</param>
/// <param name="WorkingDays">The working days for the member.</param>
public record MemberProperties(NotificationSettings Notifications, WorkingDays WorkingDays) : JsonSettings
{
    public MemberProperties() : this(new NotificationSettings(true, false))
    {
    }

    public MemberProperties(NotificationSettings notifications) : this(notifications, WorkingDays.Default)
    {
    }
};

/// <summary>
/// The days of the week that the agent works.
/// </summary>
/// <param name="Monday">Lunes</param>
/// <param name="Tuesday">Martes</param>
/// <param name="Wednesday">Miércoles</param>
/// <param name="Thursday">Jueves</param>
/// <param name="Friday">Viernes</param>
/// <param name="Saturday">Sábado</param>
/// <param name="Sunday">Domingo</param>
public record WorkingDays(
    bool Monday,
    bool Tuesday,
    bool Wednesday,
    bool Thursday,
    bool Friday,
    bool Saturday,
    bool Sunday)
{
    public static readonly WorkingDays Default = new(true, true, true, true, true, false, false);
}

/// <summary>
/// Notification settings.
/// </summary>
/// <param name="OnExpiration">If <c>true</c>, overdue notifications come in as they happen.</param>
/// <param name="DailyDigest">If <c>true</c>, a message an hour before the working day is over is sent with open conversations. This is not exclusive of <see cref="OnExpiration"/>.</param>
public record NotificationSettings(bool OnExpiration, bool DailyDigest);

public static class MemberExtensions
{
    /// <summary>
    /// Returns syntax for a "silent mention", which is a mention that doesn't ping the user.
    /// In Slack, this is a link to the user's profile, with the text '@username'.
    /// </summary>
    public static string ToSilentMention(this Member member)
    {
        var url = SlackFormatter.UserUrl(member.Organization.Domain, member.User.PlatformUserId);
        return $"<{url}|@{member.DisplayName}>";
    }

    public static bool IsInRoleByRoleId(this Member member, int id)
    {
        return member.Active && member.MemberRoles.Any(ur => ur.RoleId == id);
    }

    public static bool IsInRole(this Member member, string roleName)
    {
        return member.Active && member.MemberRoles.Any(ur => ur.Role.Name == roleName);
    }

    public static bool IsAgent(this Member member)
    {
        return member.Active && member.IsInRole(Roles.Agent);
    }

    public static bool IsAdministrator(this Member member)
    {
        return member.Active && member.IsInRole(Roles.Administrator);
    }

    public static bool CanManageConversations(this Member member)
    {
        return member.IsAgent() || member.IsAdministrator();
    }

    public static bool IsStaff(this Member member)
    {
        return member.Active && member.IsInRole(Roles.Staff);
    }

    /// <summary>
    /// Returns true if the member is an Abbot (system, default, or custom).
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>Returns true if the member is Abbot.</returns>
    public static bool IsAbbot(this Member member) => member.User.IsAbbot;

    /// <summary>
    /// Gets the <see cref="WorkingHours"/> for a <see cref="Member"/>, or returns <see cref="WorkingHours.Default"/>
    /// if they haven't set working hours.
    /// </summary>
    public static WorkingHours GetWorkingHoursOrDefault(this Member member)
    {
        return member.WorkingHours ?? WorkingHours.Default;
    }

    /// <summary>
    /// Gets the <see cref="WorkingHours"/> for a <see cref="Member"/> in the specified time zone, if any.
    /// </summary>
    /// <returns>
    /// A <see cref="WorkingHours"/> representing the member's working hours in the provided timezone.
    /// Or <c>null</c> if <paramref name="viewerTimeZone" /> or the <see cref="Member.TimeZoneId"/> of <paramref name="member" /> is <c>null</c>.</returns>
    public static WorkingHours? GetWorkingHoursInViewerTimeZone(this Member member, string? viewerTimeZone)
    {
        if (member.TimeZoneId is not { Length: > 0 } || viewerTimeZone is not { Length: > 0 })
        {
            return null;
        }

        var workingHours = member.GetWorkingHoursOrDefault();
        return workingHours.ChangeTimeZone(DateTime.UtcNow, DateTimeZoneProviders.Tzdb[member.TimeZoneId], DateTimeZoneProviders.Tzdb[viewerTimeZone]);
    }

    /// <summary>
    /// Returns true if the current time is within this user's Working Hours. If no Working Hours are set or if the
    /// member's timezone is not known, then returns true.
    /// </summary>
    /// <param name="member">The <see cref="Member"/>.</param>
    /// <param name="utcDate">The current UTC date.</param>
    /// <returns><c>true</c> if the utc date, converted to the Member's timezone, is in their working hours.</returns>
    public static bool IsInWorkingHours(this Member member, DateTime utcDate)
    {
        if (member.TimeZoneId is not { } tz)
        {
            return true;
        }
        return member.IsWorkDay(utcDate)
            && (
                member.WorkingHours is not { } workingHours // If no working hours are set, then we assume they're on call 24/7.
                || workingHours.Contains(utcDate, tz)
            );
    }

    /// <summary>
    /// Returns the next date within the member's working hours after the current working hours.
    /// </summary>
    /// <param name="member">The <see cref="Member"/>.</param>
    /// <param name="utcDate">The current UTC date.</param>
    public static DateTime GetNextWorkingHoursStartDateUtc(this Member member, DateTime utcDate)
    {
        var nextWorkingHours = member is { WorkingHours: { } workingHours, TimeZoneId: { } tz }
            ? workingHours.GetNextDateWithinWorkingHours(utcDate, tz)
            : utcDate;

        if (!member.HasAnyWorkDay())
        {
            return nextWorkingHours;
        }

        while (!member.IsWorkDay(nextWorkingHours))
        {
            nextWorkingHours = nextWorkingHours.AddDays(1);
        }

        return nextWorkingHours;
    }

    /// <summary>
    /// Returns <c>true</c> if the member has any work days set.
    /// </summary>
    /// <param name="member">The <see cref="Member"/>.</param>
    public static bool HasAnyWorkDay(this Member member)
    {
        var workingDays = member.Properties.WorkingDays;
        return workingDays.Monday
               || workingDays.Tuesday
               || workingDays.Wednesday
               || workingDays.Thursday
               || workingDays.Friday
               || workingDays.Saturday
               || workingDays.Sunday;
    }

    /// <summary>
    /// Returns <c>true</c> if the current day is a work day for the member or if the member's timezone is not known.
    /// </summary>
    /// <param name="member">The <see cref="Member"/>.</param>
    /// <param name="utcDate">The current UTC date.</param>
    public static bool IsWorkDay(this Member member, DateTime utcDate)
    {
        if (member.TimeZoneId is not { } tz)
        {
            return true;
        }
        // Convert the date to the member's timezone.
        var timezone = DateTimeZoneProviders.Tzdb[tz];
        var dayOfWeek = utcDate.ToUniversalTime().ToInstant().InZone(timezone).Date.DayOfWeek;
        var workingDays = member.Properties.WorkingDays;

        return dayOfWeek is IsoDayOfWeek.Monday && workingDays.Monday
            || dayOfWeek is IsoDayOfWeek.Tuesday && workingDays.Tuesday
            || dayOfWeek is IsoDayOfWeek.Wednesday && workingDays.Wednesday
            || dayOfWeek is IsoDayOfWeek.Thursday && workingDays.Thursday
            || dayOfWeek is IsoDayOfWeek.Friday && workingDays.Friday
            || dayOfWeek is IsoDayOfWeek.Saturday && workingDays.Saturday
            || dayOfWeek is IsoDayOfWeek.Sunday && workingDays.Sunday;
    }
}
