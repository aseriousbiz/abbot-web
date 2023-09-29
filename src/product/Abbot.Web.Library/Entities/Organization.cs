using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Configuration;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Cryptography;
using Serious.EntityFrameworkCore;

namespace Serious.Abbot.Entities;

[DebuggerDisplay("{Name} ({Id})")]
public class Organization : EntityBase<Organization>, IOrganizationIdentifier, IFeatureActor
{
    public Organization()
    {
        Integrations = new EntityList<Integration>();
        Activity = new EntityList<AuditEventBase>();
        Packages = new EntityList<Package>();
    }

    // Special constructor called by EF Core.
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedMember.Local
    Organization(DbContext db)
    {
        Integrations = new EntityList<Integration>(db, this, nameof(Integrations));
        Activity = new EntityList<AuditEventBase>(db, this, nameof(Activity));
        Packages = new EntityList<Package>(db, this, nameof(Packages));
    }

    /// <summary>
    /// The default avatar for any unknown organizations.
    /// </summary>
    public const string DefaultAvatar = "/img/unknown-organization.png";

    /// <summary>
    /// The name of the organization.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The host name for the organization. Not to be confused with Slack's "Domain" which is the
    /// subdomain part of the host name.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// The customer Id in Stripe if the organization has set up a subscription.
    /// </summary>
    public string? StripeCustomerId { get; set; }

    /// <summary>
    /// The Id of the subscription in Stripe.
    /// </summary>
    public string? StripeSubscriptionId { get; set; }

    /// <summary>
    /// The type of plan the organization is on.
    /// </summary>
    public PlanType PlanType { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="TrialPlan"/> describing a temporary elevated plan for the organization.
    /// </summary>
    public TrialPlan? Trial { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating if the organization is eligible for a <see cref="PlanType.Business"/> plan trial.
    /// This is only set for brand new organizations.
    /// Also, organizations with <see cref="PlanType.None"/> are always eligible.
    /// </summary>
    public bool TrialEligible { get; set; }

    /// <summary>
    /// The Id of the organization according to the chat platform.
    /// </summary>
    public string PlatformId { get; set; } = null!;

    /// <summary>
    /// Bot Id on the chat platform for the bot. We use this to determine whether or not
    /// they've installed Abbot into their chat platform yet.
    /// </summary>
    public string? PlatformBotId { get; set; }

    /// <summary>
    /// The bot user id. This is the Id used to mention a bot user in chat. For most platforms, this is the
    /// same value as <see cref="PlatformBotId" />. For Slack, this is different.
    /// </summary>
    public string? PlatformBotUserId { get; set; }

    /// <summary>
    /// The type of chat platform such as Slack, Teams, etc.
    /// </summary>
    public PlatformType PlatformType { get; set; }

    /// <summary>
    /// The set of users that are members of this organization.
    /// </summary>
    public List<Member> Members { get; set; } = new();

    /// <summary>
    /// The set of skills for this organization.
    /// </summary>
    public List<Skill> Skills { get; set; } = new();

    /// <summary>
    /// The rooms contained in this organization.
    /// </summary>
    public IList<Room>? Rooms { get; set; }

    /// <summary>
    /// The API token for the chat platform. This enables accessing platform
    /// specific information not provided by the Bot Framework. We get this from a chat message's ChannelData.
    /// </summary>
    public SecretString? ApiToken { get; set; }

    /// <summary>
    /// The avatar for the organization according to the chat platform. Ideally 64x64 pixels.
    /// </summary>
    public string Avatar { get; set; } = DefaultAvatar;

    /// <summary>
    /// This is Abbot's Slack Bot App Id.
    /// </summary>
    /// <remarks>
    /// This is also necessary to allow us to create a link to the bot configuration page.
    /// </remarks>
    public string? BotAppId { get; set; }

    /// <summary>
    /// This is Abbot's bot App name. For production, this should be "Abbot". At some point we may produce
    /// private white-label versions of Abbot, so this would reflect the name of the bot there.
    /// </summary>
    /// <remarks>
    /// This is also necessary to allow us to create a link to the bot configuration page.
    /// </remarks>
    public string? BotAppName { get; set; }

    /// <summary>
    /// This is Abbot's name as configured by the chat platform. In the case of Slack, this defaults to "Abbot"
    /// but a Slack Admin can go into App Configuration for their installation of Abbot and change it. This is
    /// the name that users would mention and see in their side-bar.
    /// </summary>
    public string? BotName { get; set; }

    /// <summary>
    /// This is the avatar for Abbot as configured by the application. This cannot be changed.
    /// </summary>
    public string? BotAvatar { get; set; }

    /// <summary>
    /// Overrides the avatar shown for Abbot in Abbot's chat replies. It does not change the avatar for Abbot
    /// shown in the sidebar.
    /// </summary>
    public string? BotResponseAvatar { get; set; }

    /// <summary>
    /// Controls whether users who use their chat account to authenticate into the website should automatically
    /// gain access to the site or will be put in a waiting list to be approved.
    /// </summary>
    public bool AutoApproveUsers { get; set; } = true;

    /// <summary>
    /// The set of scopes the organization currently has granted to the chat platform.
    /// </summary>
    public string? Scopes { get; set; }

    /// <summary>
    /// The shortcut character used to invoke Abbot skills as an alternative to mentioning Abbot.
    /// </summary>
    public char ShortcutCharacter { get; set; } = '.';

    /// <summary>
    /// Whether or not the publicly accessible API (used by the Abbot Commandline tool) is enabled. The API still
    /// requires an authenticated API key, so it's not accessible to just anyone.
    /// </summary>
    public bool ApiEnabled { get; set; }

    /// <summary>
    /// Whether or not Abbot responds to mentions that do not match a skill.
    /// </summary>
    public bool FallbackResponderEnabled { get; set; }

    /// <summary>
    /// Our internal name or id for the organization. Used to name custom container images and to refer to the org
    /// in an independent way separate from the platform id or domain. Pre-filled with the first part of the
    /// domain (if it's slack), platformID in other platforms, but customizable for other platforms.
    /// </summary>
    public string Slug { get; set; } = null!;

    /// <summary>
    /// The Id of the Enterprise Grid that this organization belongs to, if any. If it's <c>null</c>, then
    /// we haven't yet determined this. If it's an empty string, then we've determined that this organization
    /// is NOT part of an Enterprise Grid.
    /// </summary>
    public string? EnterpriseGridId { get; set; }

    /// <summary>
    /// Gets or sets an <see cref="OrganizationSettings"/> that contains the actual settings.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public OrganizationSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets the default <see cref="RoomSettings"/> that contains the actual settings.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public RoomSettings? DefaultRoomSettings { get; set; }

    /// <summary>
    /// The default <see cref="Threshold{TimeSpan}"/> that defines the maximum time between when a non-organization member posts and when an organization member responds.
    /// </summary>
    public Threshold<TimeSpan> DefaultTimeToRespond { get; set; } = new(null, null);

    /// <summary>
    /// Whether or not running user skills is enabled.
    /// </summary>
    public bool UserSkillsEnabled { get; set; }

    /// <summary>
    /// Gets a list of <see cref="Integration"/>s registered for this organization.
    /// </summary>
    public EntityList<Integration> Integrations { get; set; }

    /// <summary>
    /// The set of <see cref="Package"/>s created by this organization.
    /// </summary>
    public EntityList<Package> Packages { get; set; }

    /// <summary>
    /// Gets the UTC timestamp of the last platform update for this entity.
    /// </summary>
    public DateTime? LastPlatformUpdate { get; set; }

    /// <summary>
    /// When this is <c>false</c>, All abbot functionality is disabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The number of seats this organization has purchased.
    /// </summary>
    public int PurchasedSeatCount { get; set; }

    public EntityList<AuditEventBase> Activity { get; set; }

    public TargetingContext GetTargetingContext() => this.CreateTargetingContext();

    public bool IsUnactivated()
    {
        return Settings.OnboardingState == OnboardingState.Unactivated || PlanType == PlanType.None;
    }

    public bool IsOnboarding()
    {
        return Settings.OnboardingState == OnboardingState.Onboarding;
    }
}

public record OrganizationSettings
{
    /// <summary>
    /// Id of the organization's <see cref="Customer"/> record in A Serious Business's tenant.
    /// </summary>
    public Id<Customer>? SeriousCustomerId { get; set; }

    /// <summary>
    /// Id of default <see cref="Hub"/> to use for conversations in rooms without <see cref="Room.Hub"/> set.
    /// </summary>
    public Id<Hub>? DefaultHubId { get; init; }

    /// <summary>
    /// Whether or not Abbot AI Enhancements such as message classification and conversation
    /// summarization are enabled.
    /// </summary>
    public bool? AIEnhancementsEnabled { get; init; }

    /// <summary>
    /// If <c>true</c> AND <see cref="AIEnhancementsEnabled"/> is <c>true</c>, Abbot does not create a
    /// <see cref="Conversation"/> in response to a message classified as social.
    /// </summary>
    public bool? IgnoreSocialMessages { get; init; }

    /// <summary>
    /// Describes custom endpoints for skills
    /// </summary>
    public IDictionary<CodeLanguage, SkillRunnerEndpoint> SkillEndpoints { get; init; } =
        new Dictionary<CodeLanguage, SkillRunnerEndpoint>();

    /// <summary>
    /// If <c>true</c> then only overdue conversations in the New state will be notified. By default, all overdue
    /// conversations are notified.
    /// </summary>
    public bool NotifyOnNewConversationsOnly { get; init; }

    /// <summary>
    /// The state of the organization's onboarding process.
    /// </summary>
    public OnboardingState OnboardingState { get; init; }

    /// <summary>
    /// The intended customer type this organization plans to use Abbot with.
    /// </summary>
    public IntendedCustomerType IntendedCustomerType { get; init; }

    /// <summary>
    /// The name of the default <see cref="Hub"/> to to create as specified in the onboarding. This is recorded
    /// for posterity.
    /// </summary>
    public string? OnboardingDefaultHubRoomName { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingState
{
    /// <summary>
    /// The organization predates the onboarding process.
    /// This is the default value for existing organizations prior to the onboarding process being introduced.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The organization has not been activated. It is a foreign org.
    /// </summary>
    Unactivated,

    /// <summary>
    /// The organization has been activated, but has not completed onboarding.
    /// </summary>
    Onboarding,

    /// <summary>
    /// The organization has completed onboarding.
    /// </summary>
    Completed,

    /// <summary>
    /// The organization has skipped onboarding.
    /// </summary>
    Skipped,
}

/// <summary>
/// Describes the type of customers that this organization is intended to serve.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntendedCustomerType
{
    /// <summary>
    /// Customer type is not specified.
    /// </summary>
    None,

    /// <summary>
    /// This organization plans to use Abbot for external customers.
    /// </summary>
    ExternalCustomers,

    /// <summary>
    /// This organization plans to use Abbot for internal customers.
    /// </summary>
    InternalUsers,
}
