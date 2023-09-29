using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using Humanizer;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Segment;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Events;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Models;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The repository for organizations.
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    static readonly ILogger<OrganizationRepository> Log =
        ApplicationLoggerFactory.CreateLogger<OrganizationRepository>();

    readonly AbbotContext _db;
    readonly IUserRepository _userRepository;
    readonly IClock _clock;
    readonly IAuditLog _auditLog;
    readonly IPublishEndpoint _publishEndpoint;
    readonly IAnalyticsClient _analyticsClient;
    readonly IOptions<AbbotOptions> _options;

    public OrganizationRepository(
        AbbotContext db,
        IUserRepository userRepository,
        IClock clock,
        IAuditLog auditLog,
        IPublishEndpoint publishEndpoint,
        IAnalyticsClient analyticsClient,
        IOptions<AbbotOptions> options)
    {
        _db = db;
        _userRepository = userRepository;
        _clock = clock;
        _auditLog = auditLog;
        _publishEndpoint = publishEndpoint;
        _analyticsClient = analyticsClient;
        _options = options;
    }

    public async Task<Organization?> GetAsync(int id) => await GetAsync(new Id<Organization>(id));

    public async Task<Organization?> GetAsync(Id<Organization> primaryKey)
    {
        return await _db.Organizations.FindByIdAsync(primaryKey);
    }

    public async Task<EnsureResult<Organization>> EnsureAsync(ClaimsPrincipal principal)
    {
        var platformId = principal.GetPlatformTeamId().Require();
        var enterpriseGridId = principal.GetEnterpriseId();

        var organization = await GetAsync(platformId);

        bool isNew = false;

        if (organization is null)
        {
            var domain = principal.GetPlatformDomain();
            var pos = domain?.LastIndexOf(".slack.com", StringComparison.InvariantCultureIgnoreCase) ?? -1;
            var slug = pos > 0
                ? domain?[..pos] ?? platformId
                : platformId;

            organization = await CreateOrganizationAsync(
                platformId,
                PlanType.None,
                principal.GetPlatformTeamName(),
                principal.GetPlatformDomain(),
                slug,
                principal.GetPlatformAvatar(),
                enterpriseGridId);


            isNew = true;
        }
        else if (organization.EnterpriseGridId != enterpriseGridId)
        {
            // This EnterpriseGridId is retrieved from the `https://slack.com/api/openid.connect.userInfo` endpoint
            // as part of the Auth0 "Fetch User Profile Script" in the custom OAuth connector we use for Slack auth.
            // This uses the authenticated user's access token to get this info, so it should be up-to-date.
            organization.EnterpriseGridId = enterpriseGridId;
            await _db.SaveChangesAsync();
        }

        return new EnsureResult<Organization>(organization, isNew);
    }

    public async Task<Organization?> GetAsync(string platformId)
    {
        return await _db.Organizations
            .SingleOrDefaultAsync(o => o.PlatformId == platformId);
    }

    public async Task<IReadOnlyList<Organization?>> GetAllForGarbageCollectionAsync()
    {
        return await _db.Organizations
            // Our query filter ignores deleted skills, so don't do that.
            .IgnoreQueryFilters()
            // Skills aren't deleted, they are marked as deleted. So this _should_ mean we still include organizations that had skills and then later deleted them.
            // We'll want to garbage collect those too.
            .Where(o => o.Skills.Count > 0)
            .Include(o => o.Skills)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Organization>> GetOrganizationsToUpdateFromApiAsync(int daysSinceLastUpdate)
    {
        return await _db.Organizations
            .Where(o => o.PlatformType == PlatformType.Slack)
            .Where(o => o.ApiToken != null)
            .Where(o => o.LastPlatformUpdate == null
                        || o.LastPlatformUpdate < _clock.UtcNow.AddDays(-1 * daysSinceLastUpdate))
            .OrderBy(o => o.LastPlatformUpdate) // Order by oldest first.
            .Take(100) // Limit to 100 organizations at a time.
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Room>> EnsureRoomsLoadedAsync(Organization organization)
    {
        await _db.Entry(organization).Collection(o => o.Rooms!).LoadAsync();
        return organization.Rooms.Require().ToReadOnlyList();
    }

    public Task<Organization> CreateOrganizationAsync(
        string platformId,
        PlanType plan,
        string? name,
        string? domain,
        string slug,
        string? avatar,
        string? enterpriseGridId = null,
        OnboardingState onboardingState = OnboardingState.Unactivated)
    {
        var organization = new Organization
        {
            Name = name,
            Domain = domain,
            PlatformId = platformId,
            Slug = slug,
            PlatformType = PlatformType.Slack,
            PlanType = plan,
            TrialEligible = plan == PlanType.Free, // Orgs that start on the free plan get trials.
            Avatar = avatar ?? Organization.DefaultAvatar,
            EnterpriseGridId = enterpriseGridId,
            Settings = new()
            {
                OnboardingState = onboardingState
            },
        };

        return CreateOrganizationAsync(organization, includeOptionalResources: true);
    }

    public async Task<bool> EnsureActivatedAsync(Organization organization, Member actor)
    {
        // Foreign orgs created after OnboardingState was added will be marked Unactivated.
        // Foreign orgs created before OnboardingState was added will be indicated by the PlanType being None.
        if (organization.Settings.OnboardingState is OnboardingState.Unactivated || organization.PlanType == PlanType.None)
        {
            organization.Settings = organization.Settings with
            {
                OnboardingState = OnboardingState.Onboarding,
            };
            organization.TrialEligible = true;

            if (organization.PlanType is PlanType.None)
            {
                organization.PlanType = _options.Value.DefaultPlan;
            }

            _db.Organizations.Update(organization);
            await _db.SaveChangesAsync();

            _analyticsClient.Track(
                "Organization activated",
                AnalyticsFeature.Activations,
                actor,
                organization);

            // Publish a message for background processing of new organization activities
            await _publishEndpoint.Publish(new OrganizationActivated
            {
                OrganizationId = organization,
            });

            return true;
        }

        if (organization.Settings.OnboardingState is OnboardingState.Unspecified)
        {
            // Normalize old organizations
            organization.Settings = organization.Settings with
            {
                OnboardingState = OnboardingState.Completed,
            };

            _db.Organizations.Update(organization);
            await _db.SaveChangesAsync();
        }

        return false;
    }

    public async Task UpdateOrganizationAsync(Organization organization, TeamInfo teamInfo)
    {
        organization.Name = teamInfo.Name;
        organization.Domain = teamInfo.GetHostName();
        organization.Slug = teamInfo.Domain;
        organization.Avatar = teamInfo.GetAvatar() ?? organization.Avatar;
        organization.EnterpriseGridId = teamInfo.GetEnterpriseId();

        await _db.SaveChangesAsync();

        await _publishEndpoint.Publish(new OrganizationUpdated()
        {
            OrganizationId = organization,
        });
    }

    public async Task SetOverrideRunnerEndpointAsync(Organization organization, CodeLanguage codeLanguage,
        SkillRunnerEndpoint endpoint, Member actor)
    {
        var oldEndpoint = organization.Settings.SkillEndpoints.TryGetValue(codeLanguage, out var e)
            ? e
            : null;

        organization.Settings.SkillEndpoints[codeLanguage] = endpoint;

        _db.Update(organization); // Mark as modified because sometimes EF can't see inside Settings.
        await _db.SaveChangesAsync();

        var auditEvent = new AuditEventBuilder
        {
            EntityId = organization.Id,
            Actor = actor,
            Description = $"Changed Custom Endpoint for {codeLanguage} Runner",
            Type = new AuditEventType(AuditEventType.RunnerEndpointsSubject, AuditOperation.Changed),
            Organization = organization,
            Properties = new {
                Language = codeLanguage,
                OldEndpoint = oldEndpoint,
                NewEndpoint = endpoint,
            }
        };

        await _auditLog.LogAuditEventAsync(auditEvent);
    }

    public async Task ClearOverrideRunnerEndpointAsync(Organization organization, CodeLanguage codeLanguage,
        Member actor)
    {
        var oldEndpoint = organization.Settings.SkillEndpoints.TryGetValue(codeLanguage, out var e)
            ? e
            : null;

        organization.Settings.SkillEndpoints.Remove(codeLanguage);

        _db.Update(organization); // Mark as modified because sometimes EF can't see inside Settings.
        await _db.SaveChangesAsync();

        var auditEvent = new AuditEventBuilder
        {
            EntityId = organization.Id,
            Actor = actor,
            Description = $"Removed Custom Endpoint for {codeLanguage} Runner",
            Type = new AuditEventType(AuditEventType.RunnerEndpointsSubject, AuditOperation.Changed),
            Organization = organization,
            Properties = new {
                Language = codeLanguage,
                OldEndpoint = oldEndpoint,
            }
        };

        await _auditLog.LogAuditEventAsync(auditEvent);
    }

    public async Task<Organization> InstallBotAsync(InstallEvent installEvent)
    {
        Log.MethodEntered(typeof(OrganizationRepository), nameof(InstallBotAsync), "Installing Abbot...");

        if (installEvent.ApiToken is null)
        {
            Log.AccessTokenNotFound(installEvent.PlatformId, installEvent.PlatformType);
        }

        var organization = await GetAsync(installEvent.PlatformId);
        if (organization is null)
        {
            organization = await CreateOrganizationAsync(
                installEvent.PlatformId,
                _options.Value.DefaultPlan, // When we're directly installed, they get the default plan.
                installEvent.Name,
                installEvent.Domain,
                installEvent.Slug,
                installEvent.Avatar,
                enterpriseGridId: installEvent.EnterpriseId ?? string.Empty,
                OnboardingState.Onboarding); // When we're directly installed, the org is activated.
        }
        else
        {
            if (organization.PlanType is PlanType.None)
            {
                // This was a foreign org before, so we need to update the plan type.
                // Foreign orgs become trial-eligible when they install the bot.
                organization.TrialEligible = true;
                organization.PlanType = PlanType.Free;
            }

            if (organization.Settings.OnboardingState is OnboardingState.Unactivated)
            {
                organization.Settings = organization.Settings with
                {
                    OnboardingState = OnboardingState.Onboarding
                };
            }

            organization.Name = installEvent.Name;
            organization.Domain ??= installEvent.Domain;
            organization.EnterpriseGridId = installEvent.EnterpriseId ?? string.Empty;
            organization.Slug = installEvent.Slug;
            organization.Avatar = installEvent.Avatar ?? Organization.DefaultAvatar;
        }

        var auth = new SlackAuthorization(
            installEvent.AppId,
            installEvent.BotAppName,
            installEvent.BotId,
            installEvent.BotUserId,
            installEvent.BotName,
            installEvent.BotAvatar ?? Organization.DefaultAvatar,
            BotResponseAvatar: organization.BotResponseAvatar,
            installEvent.ApiToken,
            installEvent.OAuthScopes);

        auth.Apply(organization);
        await SaveChangesAsync();

        Log.AbbotInstalled(organization.Name,
            organization.Id,
            organization.PlatformId,
            organization.BotAppName,
            organization.BotAppId);

        var installInfo = InstallationInfo.Create(InstallationEventAction.Install, organization);
        var actor = await TryCreateMember(installEvent.Installer) ?? await EnsureAbbotMember(organization);
        await _auditLog.LogInstalledAbbotAsync(installInfo, organization, actor);

        return organization;

        async Task<Member?> TryCreateMember(ClaimsPrincipal? principal) =>
            principal is null
                ? null
                : await _userRepository.EnsureCurrentMemberWithRolesAsync(principal, organization);
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }

    public async Task<bool> ContainsAtLeastOneUserInRoleAsync(Organization organization, string roleName)
    {
        return await _db.Members
            .Include(u => u.MemberRoles)
            .ThenInclude(ur => ur.Role)
            .Where(m => m.OrganizationId == organization.Id)
            .AnyAsync(m => m.MemberRoles.Any(mr => mr.Role.Name == roleName));
    }

    public async Task<Organization?> GetByStripeCustomerIdAsync(string stripeCustomerId)
    {
        return await _db.Organizations
            .SingleOrDefaultAsync(o => o.StripeCustomerId == stripeCustomerId);
    }

    /// <summary>
    /// Creates the provided organization, and provisions mandatory organization resources like the Abbot member.
    /// Optionally, provisions optional organization resources like skills, lists and aliases.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to create.</param>
    /// <param name="includeOptionalResources">If <c>true</c>, optional default resources like skills, lists and aliases will be provisioned.</param>
    /// <returns>The created <see cref="Organization"/></returns>
    async Task<Organization> CreateOrganizationAsync(Organization organization, bool includeOptionalResources)
    {
        var abbotUser = await _userRepository.EnsureAbbotUserAsync();
        var abbotMember = CreateAbbotBotMemberInstance(abbotUser, organization);
        await _db.Organizations.AddAsync(organization);
        await _db.Members.AddAsync(abbotMember);

        if (includeOptionalResources)
        {
            var jokesList = CreateJokesListInstance(abbotUser, organization);
            var rememberAlias = CreateRememberAliasInstance(abbotUser, organization);
            await _db.Aliases.AddAsync(rememberAlias);
            await _db.UserLists.AddAsync(jokesList);
        }

        try
        {
            await _db.SaveChangesAsync();
            Log.OrganizationCreated(organization.Id, organization.PlatformId, organization.PlatformType);
        }
        catch (DbUpdateException ex) when (ex.GetDatabaseError() is UniqueConstraintError
        {
            ColumnNames: ["PlatformId"]
        })
        {
            Log.DuplicateOrganizationByPlatformId(organization.Name, organization.PlatformId);
            var existing = await GetAsync(organization.PlatformId).Require();
            _db.Entry(organization).State = EntityState.Detached;
            return existing;
        }

        return organization;
    }

    public async Task<Member> EnsureAbbotMember(Organization organization) =>
        await _userRepository.EnsureAbbotMemberAsync(organization);

    public async Task UpdatePlanAsync(
        Organization organization,
        PlanType newPlan,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        int seatCount)
    {
        organization.PlanType = newPlan;
        organization.Trial = null; // Clear out any trial.
        organization.TrialEligible = false; // No longer trial eligible if they change plans.
        organization.StripeCustomerId = stripeCustomerId;
        organization.StripeSubscriptionId = stripeSubscriptionId;
        organization.PurchasedSeatCount = seatCount;

        await SaveChangesAsync();

        await _publishEndpoint.Publish(new OrganizationUpdated
        {
            OrganizationId = organization,
        });
    }

    public async Task<IReadOnlyList<Organization>> GetExpiredTrialsAsync(DateTime nowUtc)
    {
        return await _db.Organizations.Where(o => o.Trial != null && o.Trial.Expiry <= nowUtc).ToListAsync();
    }

    public async Task<IReadOnlyList<Organization>> GetExpiringTrialsAsync(DateTime nowUtc, int daysTillExpiration)
    {
        return await _db.Organizations
            .Where(o => o.Trial != null && o.Trial.Expiry.Date == nowUtc.AddDays(daysTillExpiration).Date)
            .ToListAsync();
    }

    public async Task<Member> StartTrialAsync(Organization organization, TrialPlan trial)
    {
        if (organization.PlanType != PlanType.Free)
        {
            throw new InvalidOperationException(
                $"Cannot start a trial for the {trial.Plan} plan because the organization is on the {organization.PlanType} plan, only organizations on the {PlanType.Free} plan can start trials.");
        }

        var oldTrial = organization.Trial;
        organization.Trial = trial;
        organization.TrialEligible = false;
        await SaveChangesAsync();

        var actor = await EnsureAbbotMember(organization);
        if (oldTrial is not null)
        {
            await _auditLog.LogAuditEventAsync(new()
            {
                Type = new("Trial", "Ended"),
                Actor = actor,
                Organization = organization,
                Description = $"Trial of {oldTrial.Plan} plan ended: New trial started.",
            });
        }

        await _auditLog.LogTrialStartedAsync(actor, organization, trial);
        await _publishEndpoint.Publish(new OrganizationUpdated
        {
            OrganizationId = organization,
        });

        return actor;
    }

    public async Task ExtendTrialAsync(Organization organization, TimeSpan extension, string reason, Member? actor)
    {
        if (organization is not { Trial.Expiry: var expiry })
        {
            return;
        }

        var currentTrial = organization.Trial;
        organization.Trial = organization.Trial with
        {
            Expiry = expiry + extension
        };

        await SaveChangesAsync();

        actor ??= await EnsureAbbotMember(organization);
        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Trial", "Extended"),
                Actor = actor,
                Organization = organization,
                Description = $"Trial of {currentTrial.Plan} plan extended by {extension.Humanize()}: {reason}",
            });

        await _publishEndpoint.Publish(new OrganizationUpdated
        {
            OrganizationId = organization,
        });
    }

    public async Task EndTrialAsync(Organization organization, string reason, Member? actor)
    {
        if (organization.Trial is null)
        {
            return;
        }

        var oldTrial = organization.Trial;
        organization.Trial = null;
        await SaveChangesAsync();

        actor ??= await EnsureAbbotMember(organization);
        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Trial", "Ended"),
                Actor = actor,
                Organization = organization,
                Description = $"Trial of {oldTrial.Plan} plan ended: {reason}",
            });

        await _publishEndpoint.Publish(new OrganizationUpdated
        {
            OrganizationId = organization,
        });
    }

    internal static Member CreateAbbotBotMemberInstance(User abbotUser, Organization organization)
    {
        // Every org has a system user named Abbot that represents the website.
        var member = new Member
        {
            User = abbotUser,
            Organization = organization,
            Active = false
        };

        abbotUser.Members.Add(member);
        return member;
    }

    static UserList CreateJokesListInstance(User owner, Organization organization)
    {
        // Let's create some jokes!
        var jokes = new[]
        {
            "What sort of robot turns into a tractor? A transfarmer.",
            "Why was the robot tired when it got home? Because it had a hard drive.",
            "What is R2D2 short for? Because he has little legs.",
            "What did the baby robot call its creator? Da-ta.", "What do robots do at lunchtime? Have a megabyte.",
            "Why was the robot bankrupt? it had used all its cache.",
            "How do robots eat guacamole? With microchips?",
            "Why was the robot angry? People kept pushing its buttons.",
            "How do robots drive? They put their metal to the pedal.",
            "What is a robot’s favorite music? Heavy metal.", "What do you call a pirate robot? Arrrrr2D2.",
            "Why do robots have summer holidays? To recharge their batteries.",
            "What did the ocean say to the shore? Nothing. It just waved.",
            "Don’t you hate it when someone answers their own questions? I do.",
            "What do you call a robot with no body and no nose? Nobody knows."
        }.Select(joke => new UserListEntry
        {
            Content = joke,
            Creator = owner,
            ModifiedBy = owner
        });

        return new UserList
        {
            Name = "joke",
            Description = "Abbot's favorite jokes",
            Entries = jokes.ToList(),
            Creator = owner,
            ModifiedBy = owner,
            Organization = organization
        };
    }

    static Alias CreateRememberAliasInstance(User owner, Organization organization)
    {
        return new()
        {
            Name = "remember",
            TargetSkill = "rem",
            Description = "A shortcut to `rem`",
            Creator = owner,
            ModifiedBy = owner,
            Organization = organization
        };
    }

    // This is called when a user is authenticating if the org is not complete.
    public async Task UpdateOrganizationAsync(Organization organization, ClaimsPrincipal principal)
    {
        if (organization.IsComplete())
        {
            return;
        }

        if (organization.PlanType == PlanType.None)
        {
            // They were a foreign org before. Make them a free org.
            organization.PlanType = PlanType.Free;
        }

        organization.Name = principal.GetPlatformTeamName() ?? organization.Name;
        organization.Domain = principal.GetPlatformDomain() ?? organization.Domain;

        await SaveChangesAsync();
    }

    public async Task<bool> AssignDefaultFirstResponderAsync(Organization organization, Member subject, Member actor)
    {
        if (subject.IsDefaultFirstResponder)
        {
            return false;
        }

        if (subject.User.IsBot || !subject.IsAgent())
        {
            return false;
        }

        subject.IsDefaultFirstResponder = true;
        await _db.SaveChangesAsync();
        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization.DefaultFirstResponder", "Added"),
                Actor = actor,
                Organization = organization,
                Description = $"Added {subject.DisplayName} as a default first responder for the organization.",
            });

        return true;
    }

    public async Task<bool> UnassignDefaultFirstResponderAsync(Organization organization, Member subject, Member actor)
    {
        if (!subject.IsDefaultFirstResponder)
        {
            return false;
        }

        subject.IsDefaultFirstResponder = false;
        await _db.SaveChangesAsync();
        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization.DefaultFirstResponder", "Removed"),
                Actor = actor,
                Organization = organization,
                Description = $"Removed {subject.DisplayName} as a default first responder for the organization.",
            });

        return true;
    }

    public async Task<bool> AssignDefaultEscalationResponderAsync(Organization organization, Member subject,
        Member actor)
    {
        if (subject.IsDefaultEscalationResponder)
        {
            return false;
        }

        if (subject.User.IsBot || !subject.IsAgent())
        {
            return false;
        }

        subject.IsDefaultEscalationResponder = true;
        await _db.SaveChangesAsync();
        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization.DefaultEscalationResponder", "Added"),
                Actor = actor,
                Organization = organization,
                Description = $"Added {subject.DisplayName} as a default escalation responder for the organization.",
            });

        return true;
    }

    public async Task<bool> UnassignDefaultEscalationResponderAsync(Organization organization, Member subject,
        Member actor)
    {
        if (!subject.IsDefaultEscalationResponder)
        {
            return false;
        }

        subject.IsDefaultEscalationResponder = false;
        await _db.SaveChangesAsync();
        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization.DefaultEscalationResponder", "Removed"),
                Actor = actor,
                Organization = organization,
                Description = $"Removed {subject.DisplayName} as a default escalation responder for the organization.",
            });

        return true;
    }

    public async Task<int> GetAgentCountAsync(Organization organization)
    {
        return await _db.Members
            .Where(m => m.OrganizationId == organization.Id)
            .Where(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .CountAsync();
    }

    public async Task DeleteOrganizationAsync(string platformId, string reason, Member actor)
    {
        if (!actor.IsStaff())
        {
            throw new InvalidOperationException("Only Staff members can delete organizations.");
        }

        var organization = await _db.Organizations
            .IgnoreQueryFilters() // Include soft-deleted skills.
            .Include(o => o.Skills)
            .Where(o => o.PlatformId == platformId)
            .SingleOrDefaultAsync();

        if (organization is null)
        {
            return;
        }

        var conversationLinks = await _db.ConversationLinks
            .Where(l => l.OrganizationId == organization.Id
                        || (l.CreatedBy != null && l.CreatedBy.OrganizationId == organization.Id))
            .ToListAsync();

        _db.ConversationLinks.RemoveRange(conversationLinks);
        await _db.SaveChangesAsync();

        var skillsWithSourcePackageFromThisOrganization = organization
            .Packages
            .SelectMany(p => p.Versions)
            .SelectMany(p => p.InstalledSkills)
            .ToList();

        foreach (var skill in skillsWithSourcePackageFromThisOrganization)
        {
            // If any skills from other organizations were installed from a package published by this organization,
            // we need to disconnect the "source package" relationship so that the package can be deleted.
            // The skill will still exist, but it will not be "attached" to a package.
            skill.SourcePackageVersionId = null;
            skill.SourcePackageVersion = null;
        }

        await _db.SaveChangesAsync();

        var auditEvents = await _db.AuditEvents
            .Where(e => e.OrganizationId == organization.Id
                        || (e.ActorMember != null && e.ActorMember.OrganizationId == organization.Id))
            .ToListAsync();

        _db.AuditEvents.RemoveRange(auditEvents);
        await _db.SaveChangesAsync();
        _db.Remove(organization);
        await _db.SaveChangesAsync();

        await _auditLog.LogOrganizationDeleted(organization, reason, actor);
    }

    public async Task SetAISettingsWithAuditing(
        bool aiEnhancementsEnabled,
        bool ignoreSocialConversations,
        Organization organization,
        Member actor)
    {
        // Get existing settings.
        if (aiEnhancementsEnabled == organization.Settings.AIEnhancementsEnabled
            && ignoreSocialConversations == organization.Settings.IgnoreSocialMessages)
        {
            // No changes. We're good.
            return;
        }

        var oldSettings = organization.Settings;
        organization.Settings = oldSettings with
        {
            AIEnhancementsEnabled = aiEnhancementsEnabled,
            IgnoreSocialMessages = ignoreSocialConversations,
        };

        var auditEvent = new AuditEventBuilder
        {
            EntityId = organization.Id,
            Actor = actor,
            Description = "Changed AI Enhancement Settings.",
            Type = new AuditEventType(AuditEventType.AIEnhancementSubject, AuditOperation.Changed),
            Organization = organization,
            Properties = new {
                OldSettings = oldSettings,
                NewSettings = organization.Settings,
            }
        };

        await _auditLog.LogAuditEventAsync(auditEvent);
    }

    public async Task AssociateSeriousCustomerAsync(Organization organization, Customer customer, Member actor)
    {
        if (customer.Organization.PlatformId != WebConstants.ASeriousBizSlackId)
        {
            throw new UnreachableException(
                "Organizations can only be associated with a Customer in the ASeriousBiz Slack workspace.");
        }

        organization.Settings.SeriousCustomerId = customer;
        _db.Organizations.Update(organization);

        await _db.SaveChangesAsync();

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization", "AssociatedWithCustomer"),
                Organization = customer.Organization,
                Description = $"Associated {organization.Name} with customer {customer.Name}",
                Details =
                    $"Organization {organization.Name} with platform ID {organization.PlatformId} associated with customer {customer.Name}",
                Actor = actor,
                EntityId = customer.Id,
                Properties = new {
                    CustomerId = customer.Id,
                    OrganizationId = organization.Id,
                }
            });
    }

    public async Task SetOnboardingStateAsync(Organization organization, OnboardingState state, Member actor)
    {
        var oldState = organization.Settings.OnboardingState;
        organization.Settings = organization.Settings with
        {
            OnboardingState = state,
        };

        _db.Organizations.Update(organization);
        await _db.SaveChangesAsync();

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization", "SetOnboardingState"),
                Organization = organization,
                Description = state switch
                {
                    OnboardingState.Completed => "Completed onboarding for the organization",
                    OnboardingState.Skipped => "Skipped onboarding for the organization",
                    _ => "Set onboarding state for the organization",
                },
                Details = $"Onboarding state for organization {organization.Name} set to {state}",
                Actor = actor,
                Properties = new {
                    OldState = oldState,
                    NewState = state,
                }
            },
            new(AnalyticsFeature.Subscriptions, "Organization onboarding changed")
            {
                ["old_state"] = oldState,
                ["new_state"] = state,
            });
    }
}

static partial class OrganizationRepositoryLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message =
            "Abbot installed to organization ((Name: {OrganizationName}, Id: {OrganizationId}, PlatformId: {PlatformId}, BotApp: {BotAppName} ({BotAppId}))")]
    public static partial void AbbotInstalled(
        this ILogger<OrganizationRepository> logger,
        string? organizationName,
        int organizationId,
        string platformId,
        string? botAppName,
        string? botAppId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message =
            "Abbot uninstalled from organization (Name: {OrganizationName}, Id: {OrganizationId}, PlatformId: {PlatformId}, BotApp: {BotAppName} ({BotAppId}))")]
    public static partial void AbbotUninstalled(
        this ILogger<OrganizationRepository> logger,
        string? organizationName,
        int organizationId,
        string platformId,
        string? botAppName,
        string? botAppId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message =
            "Duplicate Organization {OrganizationName} by PlatformId {PlatformId} error while trying to create oranization.")]
    public static partial void DuplicateOrganizationByPlatformId(
        this ILogger<OrganizationRepository> logger,
        string? organizationName,
        string platformId);
}
