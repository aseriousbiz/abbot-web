using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff.Users;

public class IndexPage : StaffToolsPage
{
    readonly AbbotContext _db;

    public IndexPage(AbbotContext abbotContext)
    {
        _db = abbotContext;
    }

    public IPaginatedList<User> Users { get; private set; } = null!;

    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    [BindProperty(SupportsGet = true)]
    public UserTypeFilter? Type { get; set; }

    public async Task OnGetAsync(int? p = 1)
    {
        Type ??= UserTypeFilter.HomeOrganizations;
        int pageNumber = p ?? 1;

        var queryable = _db.Users
            .Include(u => u.Members)
            .ThenInclude(m => m.Organization)
            .AsQueryable();
        queryable = Type switch
        {
            UserTypeFilter.All => queryable,
            UserTypeFilter.LoggedIn => queryable.Where(u => u.NameIdentifier != null),
            UserTypeFilter.HomeOrganizations => queryable.Where(u => u.Members.Any(m => m.Organization.PlanType != PlanType.None)),
            UserTypeFilter.ForeignOrganizations => queryable.Where(u => u.Members.Any(m => m.Organization.PlanType == PlanType.None)),
            var x => throw new InvalidOperationException($"Unknown type filter {x}"),
        };

        // ToLower is what Postgres uses for case-insensitive matching, so we should too.
        // https://www.postgresql.org/docs/current/citext.html
        var filter = Filter?.ToLowerInvariant() ?? "";

        queryable = filter switch
        {
            "" => queryable,
            "integration:hubspot" => With(LinkedIdentityType.HubSpot),
            "integration:zendesk" => With(LinkedIdentityType.Zendesk),
            "-integration:hubspot" => Without(LinkedIdentityType.HubSpot),
            "-integration:zendesk" => Without(LinkedIdentityType.Zendesk),

            // ToLowerInvariant isn't mapped by Npgsql.
#pragma warning disable CA1304
            _ => queryable.Where(u =>
                u.DisplayName.ToLower().Contains(filter) ||
                u.Email!.ToLower().Contains(filter) ||

                // Do an exact match on Platform User ID or Slack Team ID
                u.SlackTeamId!.ToLower() == filter ||
                u.PlatformUserId.ToLower() == filter ||

                // Do a partial match on ExternalId, e.g. a Zendesk User ID
#pragma warning disable CA1307
                u.Members.SelectMany(m => m.LinkedIdentities).Any(li => li.ExternalId.ToLower().Contains(filter)))
#pragma warning restore CA1307
#pragma warning restore CA1304
        };

        queryable = queryable.OrderByDescending(o => o.Created);
        Users = await PaginatedList.CreateAsync(queryable, pageNumber, WebConstants.LongPageSize);

        IQueryable<User> With(LinkedIdentityType type) =>
            queryable.Where(u => u.Members.SelectMany(m => m.LinkedIdentities).Any(li => li.Type == type));

        IQueryable<User> Without(LinkedIdentityType type) =>
            queryable.Where(u => !u.Members.SelectMany(m => m.LinkedIdentities).Any(li => li.Type == type));
    }

    public enum UserTypeFilter
    {
        [Display(Name = "Users who have logged in")]
        LoggedIn,

        [Display(Name = "Users in installed orgs")]
        HomeOrganizations,

        [Display(Name = "Users in foreign orgs")]
        ForeignOrganizations,

        [Display(Name = "All Users, everywhere.")]
        All
    }
}
