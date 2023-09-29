using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Models;
using Serious.Abbot.Security;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff;

public class IndexPage : StaffToolsPage
{
    readonly AbbotContext _db;
    readonly DataSeedRunner _dataSeedRunner;

    public static readonly IReadOnlyList<SelectListItem> IntegrationFilterOptions = GenerateIntegrationFilterOptions().ToList();
    public static readonly IReadOnlyList<SelectListItem> PlanFilterOptions = GeneratePlanFilterOptions().ToList();

    public IndexPage(AbbotContext abbotContext, DataSeedRunner dataSeedRunner)
    {
        _db = abbotContext;
        _dataSeedRunner = dataSeedRunner;
    }

    [BindProperty]
    public OrganizationSortBy OrderBy { get; set; }

    [BindProperty]
    public string? Filter { get; set; }

    [BindProperty]
    public string? IntegrationFilter { get; set; }

    public int InstalledOrganizationCount { get; set; }
    public int TotalOrganizationCount { get; set; }

    public IReadOnlyList<IDataSeeder> DataSeeders => _dataSeedRunner.DataSeeders;

    public IPaginatedList<Organization> Organizations { get; private set; } = null!;

    public bool InstalledFilter { get; private set; }

    [BindProperty]
    public PlanFilter PlanFilter { get; private set; } = PlanFilter.Any;

    public async Task<IActionResult> OnGetAsync(
        int? p = 1,
        OrganizationSortBy? orderBy = OrganizationSortBy.Created,
        string? filter = null,
        PlanFilter planFilter = PlanFilter.Any,
        bool installedFilter = false,
        string? integrationFilter = null,
        int? pd = 1)
    {
        TotalOrganizationCount = await _db.Organizations.CountAsync();
        InstalledOrganizationCount = await _db.Organizations.CountAsync(o => o.PlanType != PlanType.None);
        PlanFilter = planFilter;
        InstalledFilter = installedFilter;
        IntegrationFilter = integrationFilter;

        int pageNumber = p ?? 1;

        IQueryable<Organization> queryable = _db.Organizations
            .Where(o => o.Domain != null || o.PlatformType != PlatformType.Slack)
            .Include(o => o.Members.Where(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent)))
            .Include(o => o.Skills)
            .Include(o => o.Activity.Where(e =>
#pragma warning disable CA1307 // Specify StringComparison for clarity
                !e.Discriminator.Contains(nameof(TriggerRunEvent))
#pragma warning restore CA1307 // Specify StringComparison for clarity
                && ((SkillRunAuditEvent)e).Signal == null)
                .OrderByDescending(a => a.Created)
                .Take(1));

        if (installedFilter)
        {
            // Must match IsBotInstalled()
            queryable = queryable.Where(o => o.PlatformBotId != null);
        }

        queryable = ApplyIntegrationFilter(queryable, integrationFilter);
        queryable = ApplyPlanFilter(queryable, planFilter);

        queryable = orderBy switch
        {
            OrganizationSortBy.Skills => queryable.OrderByDescending(o => o.Skills.Count),
            OrganizationSortBy.Agents => queryable.OrderByDescending(o => o.Members.Count(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent)))
                .ThenByDescending(o => o.PurchasedSeatCount),
            OrganizationSortBy.Seats => queryable.OrderByDescending(o => o.PurchasedSeatCount)
                .ThenByDescending(o => o.Members.Count(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent))),
            OrganizationSortBy.Underpaid => queryable
                .Where(o => o.PlanType == PlanType.Business)
                .OrderByDescending(o => o.Members.Count(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent)) - o.PurchasedSeatCount)
                .ThenByDescending(o => o.Members.Count(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent))),
            OrganizationSortBy.Enterprise => queryable
                .Where(o => EF.Functions.Like(o.PlatformId, "E%")
                    || o.EnterpriseGridId!.Length > 0)
                .OrderBy(o => o.Created),
            _ => queryable.OrderByDescending(o => o.Created),
        };

        if (SlackIdUtility.GetChatAddressTypeFromSlackId(filter ?? "") is ChatAddressType.Room)
        {
            var rooms = await _db.Rooms
                .Include(r => r.Organization)
                .Where(r => r.PlatformRoomId == filter)
                .ToListAsync();

            if (rooms is [var room])
            {
                return RedirectToPage(
                    "./Organizations/Rooms",
                    new { id = room.Organization.PlatformId, filter = room.PlatformRoomId });
            }

            if (rooms.Count > 0)
            {
                var roomOrgIds = rooms.Select(r => r.OrganizationId).ToList();
                queryable = queryable.Where(o => roomOrgIds.Contains(o.Id));

                // Skip other filters
                filter = null;
            }
        }

        if (filter is not null)
        {
            if (int.TryParse(filter, out var orgId))
            {
                queryable = queryable.Where(o => o.Id == orgId);
            }
            else
            {
                // ToLower is what Postgres uses for case-insensitive matching, so we should too.
                // https://www.postgresql.org/docs/current/citext.html
                filter = filter.ToLowerInvariant();

                // ToLowerInvariant isn't mapped by Npgsql.
#pragma warning disable CA1304
                queryable = queryable.Where(o =>
                    o.Name!.ToLower().Contains(filter) ||
                    o.Domain!.ToLower().Contains(filter) ||

                    // We do normal equality checks on PlatformId, and Stripe IDs, because we expect the user to have pasted them in.
                    // And we don't want arbitrary strings to match these criteria.
                    o.PlatformId.ToLower() == filter ||
                    o.StripeCustomerId!.ToLower() == filter ||
                    o.StripeSubscriptionId!.ToLower() == filter);
#pragma warning restore CA1304
            }
        }

        Organizations = await PaginatedList.CreateAsync(
            queryable,
            pageNumber,
            WebConstants.LongPageSize);

        return Page();
    }

    public Task<IActionResult> OnPostDisableAsync(string? returnUrl = null)
    {
        HttpContext.SetStaffModePreference(false);

        if (returnUrl is { Length: > 0 } && Url.IsLocalUrl(returnUrl))
        {
            return Task.FromResult<IActionResult>(Redirect(returnUrl));
        }
        else
        {
            return Task.FromResult<IActionResult>(RedirectToPage("/Index"));
        }
    }

    public Task<IActionResult> OnPostEnableAsync(string? returnUrl = null)
    {
        HttpContext.SetStaffModePreference(true);

        if (returnUrl is { Length: > 0 } && Url.IsLocalUrl(returnUrl))
        {
            return Task.FromResult<IActionResult>(Redirect(returnUrl));
        }
        else
        {
            return Task.FromResult<IActionResult>(RedirectToPage("/Index"));
        }
    }

    const char IntegrationFilterDelimiter = '-';

    static IQueryable<Organization> ApplyIntegrationFilter(IQueryable<Organization> query, string? integrationFilter)
    {
        if (integrationFilter is null or "")
        {
            return query;
        }

        // Invalid filter = none found
        if (integrationFilter.Split(IntegrationFilterDelimiter) is not [var t, var s]
            || !Enum.TryParse<IntegrationType>(t, out var type)
            || !Enum.TryParse<IntegrationState>(s, out var state))
        {
            return query.Where(_ => false);
        }

        return query
            .Where(o => o.Integrations
                .Any(i => i.Type == type
                    && (
                        state == IntegrationState.Exists
                        || (state == IntegrationState.Enabled && i.Enabled)
                        || (state == IntegrationState.Disabled && !i.Enabled)
                    )));
    }

    enum IntegrationState
    {
        Exists,
        Enabled,
        Disabled,
    }

    static IEnumerable<SelectListItem> GenerateIntegrationFilterOptions() =>
        from state in Enum.GetValues<IntegrationState>()
        let slg = new SelectListGroup { Name = state.Humanize() }
        from type in Enum.GetValues<IntegrationType>()
        where type is not IntegrationType.None
        select new SelectListItem
        {
            Group = slg,
            Text = type.Humanize(),
            Value = $"{type}{IntegrationFilterDelimiter}{state}",
        };

    static IQueryable<Organization> ApplyPlanFilter(IQueryable<Organization> query, PlanFilter filter)
    {
        if (filter is PlanFilter.All)
        {
            return query;
        }

        if (filter is PlanFilter.Disabled)
        {
            return query.Where(o => !o.Enabled);
        }

        query = query.Where(o => o.Enabled);

        IReadOnlyList<PlanType> allowedPlans = filter switch
        {
            PlanFilter.Any => Plan.AllTypes.Where(p => p is not PlanType.None).ToList(),
            PlanFilter.AnyPaid => new[] { PlanType.Team, PlanType.Business, PlanType.FoundingCustomer },
            PlanFilter.AnyPremium => Plan.AllTypes.Where(p => p is not (PlanType.None or PlanType.Free)).ToList(),
            PlanFilter.None => new[] { PlanType.None },
            PlanFilter.Free => new[] { PlanType.Free },
            PlanFilter.FreeTrial => new[] { PlanType.Free },
            PlanFilter.Team => new[] { PlanType.Team },
            PlanFilter.Business => new[] { PlanType.Business },
            PlanFilter.FoundingCustomer => new[] { PlanType.FoundingCustomer },
            PlanFilter.Beta => new[] { PlanType.Beta },
            PlanFilter.Unlimited => new[] { PlanType.Unlimited },
            _ => throw new UnreachableException(),
        };
        query = query.Where(o => allowedPlans.Contains(o.PlanType));
        if (filter is PlanFilter.FreeTrial)
        {
            query = query.Where(o => o.Trial != null && o.Trial.Expiry > DateTime.UtcNow);
        }
        return query;
    }

    static IEnumerable<SelectListItem> GeneratePlanFilterOptions()
    {
        yield return new SelectListItem("Any Plan (Home Orgs)", null);
        yield return new SelectListItem("Any Paid Plan", PlanFilter.AnyPaid.ToString());
        yield return new SelectListItem("Any Premium Plan", PlanFilter.AnyPremium.ToString());
        yield return new SelectListItem("No Plan (Foreign Orgs)", PlanFilter.None.ToString());
        yield return new SelectListItem("All Plans (+Foreign Orgs)", PlanFilter.All.ToString());
        yield return new SelectListItem("---", string.Empty, false, true);
        yield return new SelectListItem(PlanType.Free.GetFeatures().Name, PlanFilter.Free.ToString());
        yield return new SelectListItem("Free trial", PlanFilter.FreeTrial.ToString());
        yield return new SelectListItem(PlanType.Team.GetFeatures().Name, PlanFilter.Team.ToString());
        yield return new SelectListItem(PlanType.Business.GetFeatures().Name, PlanFilter.Business.ToString());
        yield return new SelectListItem(PlanType.FoundingCustomer.GetFeatures().Name, PlanFilter.FoundingCustomer.ToString());
        yield return new SelectListItem(PlanType.Beta.GetFeatures().Name, PlanFilter.Beta.ToString());
        yield return new SelectListItem(PlanType.Unlimited.GetFeatures().Name, PlanFilter.Unlimited.ToString());
        yield return new SelectListItem(PlanFilter.Disabled.Humanize(), PlanFilter.Disabled.ToString());
    }
}

// This enum is supposed to be a sort, but it's doing double duty as a filter too.
// TODO: Add a separate filter enum.
public enum OrganizationSortBy
{
    Created,
    Skills,
    Agents,
    Seats,
    Underpaid,
    Enterprise,
}

public enum PlanFilter
{
    None,
    Any,
    AnyPaid,
    AnyPremium,
    Free,
    FreeTrial,
    Team,
    Business,
    FoundingCustomer,
    Beta,
    Unlimited,
    All,
    Disabled,
}
