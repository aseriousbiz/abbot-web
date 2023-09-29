using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class UsersPage : OrganizationDetailPage
{
    readonly IUserRepository _userRepository;
    readonly IBackgroundJobClient _backgroundJobClient;

    public UsersPage(IUserRepository userRepository, AbbotContext db, IBackgroundJobClient backgroundJobClient, IAuditLog auditLog)
        : base(db, auditLog)
    {
        _userRepository = userRepository;
        _backgroundJobClient = backgroundJobClient;
    }

    public int MemberCount { get; private set; }

    int _pageSize;
    int _pendingPage;
    int _adminsPage;
    int _membersPage;
    int _inactivePage;
    int _knownPage;
    int _guestsPage;
    int _botsPage;
    int _foreignPage;

    public IPaginatedList<Member> Admins { get; private set; } = null!;

    public IPaginatedList<Member> Members { get; private set; } = null!;

    public IPaginatedList<Member> Pending { get; private set; } = null!;

    public IPaginatedList<Member> Inactive { get; private set; } = null!;

    public IPaginatedList<Member> Known { get; private set; } = null!;

    public IPaginatedList<Member> Foreign { get; private set; } = null!;

    public IPaginatedList<Member> Guests { get; private set; } = null!;

    public IPaginatedList<Member> Bots { get; private set; } = null!;

    public async Task OnGetAsync(string id, int? size = 20, int? pp = 1, int? pa = 1, int? pm = 1, int? pi = 1, int? pg = 1, int? pn = 1, int? po = 1, int? pf = 1)
    {
        InitializePageNumbers(size, pp, pa, pm, pi, pg, pn, po, pf);

        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        await InitializeOrganizationAsync(id);

        // Enqueue a job to update the user counts for the organization.
        // TODO: Might be nice to track this job Id so we can report progress, etc. But for now, it's all good.
        var jobId = _backgroundJobClient.Enqueue<UpdateUsersFromSlackApiJob>(j => j.UpdateUsersAsync(Organization.Id));

        StatusMessage = $"Enqueued a job (Id: {jobId}) to update the user counts for the organization.";
        return RedirectToPage();
    }

    void InitializePageNumbers(int? size = 20, int? pp = 1, int? pa = 1, int? pm = 1, int? pi = 1, int? pg = 1, int? pn = 1, int? po = 1, int? pf = 1)
    {
        _pageSize = size ?? 20;
        _pendingPage = pp ?? 1;
        _adminsPage = pa ?? 1;
        _membersPage = pm ?? 1;
        _inactivePage = pi ?? 1;
        _knownPage = pn ?? 1;
        _guestsPage = pg ?? 1;
        _botsPage = po ?? 1;
        _foreignPage = pf ?? 1;
    }

    protected override async Task InitializeDataAsync(Entities.Organization organization)
    {
        var members = organization.Members.Where(m => !m.User.IsBot).ToList();

        MemberCount = members.Count;

        var queryable = Db.Members
            .Include(m => m.User)
            .Include(u => u.MemberRoles)
            .ThenInclude(mr => mr.Role)
            .Where(m => m.OrganizationId == organization.Id);

        var pendingQueryable = _userRepository.GetPendingMembersQueryable(organization);

        var adminsQueryable = _userRepository.GetActiveMembersQueryable(organization)
            .Where(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Administrator));

        var membersQueryable = _userRepository.GetActiveMembersQueryable(organization)
            .Where(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Agent))
            .Where(m => m.MemberRoles.All(mr => mr.Role.Name != Roles.Administrator));

        var archivedQueryable = _userRepository.GetArchivedMembersQueryable(organization);

        var foreignQueryable = queryable
            .Where(m => m.User.SlackTeamId != null && m.User.SlackTeamId != organization.PlatformId);

        var knownQueryable = queryable
            .Where(m => m.User.NameIdentifier == null && !m.MemberRoles.Any() && !m.IsGuest && !m.User.IsBot);

        var guestsQueryable = queryable
            .Where(m => m.IsGuest && !m.User.IsBot);

        var botsQueryable = queryable
            .Where(m => m.User.IsBot);

        Pending = await PaginatedList.CreateAsync(pendingQueryable, _pendingPage, _pageSize, "pp");

        Admins = await PaginatedList.CreateAsync(adminsQueryable, _adminsPage, _pageSize, "pa");

        Members = await PaginatedList.CreateAsync(membersQueryable, _membersPage, _pageSize, "pm");

        Inactive = await PaginatedList.CreateAsync(archivedQueryable, _inactivePage, _pageSize, "pi");

        Foreign = await PaginatedList.CreateAsync(foreignQueryable, _foreignPage, _pageSize, "pf");

        Known = await PaginatedList.CreateAsync(knownQueryable, _knownPage, _pageSize, "pn");

        Guests = await PaginatedList.CreateAsync(guestsQueryable, _guestsPage, _pageSize, "pg");

        Bots = await PaginatedList.CreateAsync(botsQueryable, _botsPage, _pageSize, "po");
    }
}
