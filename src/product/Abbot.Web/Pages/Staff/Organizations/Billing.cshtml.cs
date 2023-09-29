using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Pages.Staff.Organizations;

public class BillingPage : OrganizationDetailPage
{
    readonly IOrganizationRepository _organizationRepository;
    readonly IOptions<StripeOptions> _stripeOptions;

    public BillingPage(AbbotContext db, IAuditLog auditLog, IOrganizationRepository organizationRepository, IOptions<StripeOptions> stripeOptions)
        : base(db, auditLog)
    {
        _organizationRepository = organizationRepository;
        _stripeOptions = stripeOptions;
    }

    public UpdateStripeConnectionModel UpdateStripeConnection { get; set; } = null!;
    public ChangePlanModel ChangePlan { get; set; } = null!;
    public IReadOnlyList<SelectListItem> AvailablePlans { get; set; } = null!;
    public string? StripeCustomerLink { get; set; }
    public string? StripeSubscriptionLink { get; set; }
    public int AgentCount { get; private set; }

    [BindProperty]
    public int? ExtensionDays { get; set; }

    protected override Task InitializeDataAsync(Organization organization)
    {
        AvailablePlans = Plan.AllTypes.Select(t => new SelectListItem(t.ToString(), t.ToString())).ToList();
        UpdateStripeConnection = new(organization.StripeCustomerId, organization.StripeSubscriptionId);
        ChangePlan = new(organization.PlanType);
        StripeCustomerLink = organization.StripeCustomerId is { Length: > 0 }
            ? $"{_stripeOptions.Value.StripeDashboardBaseUrl}/customers/{organization.StripeCustomerId}"
            : null;
        StripeSubscriptionLink = organization.StripeCustomerId is { Length: > 0 }
            ? $"{_stripeOptions.Value.StripeDashboardBaseUrl}/subscriptions/{organization.StripeSubscriptionId}"
            : null;

        AgentCount = organization.Members.Count(m => m.IsAgent());
        return Task.CompletedTask;
    }

    public async Task OnGetAsync(string id)
    {
        await InitializeDataAsync(id);
    }

    public async Task<IActionResult> OnPostChangePlanAsync(string id, ChangePlanModel changePlan)
    {
        await InitializeDataAsync(id);
        if (Organization.PlanType != changePlan.NewPlan)
        {
            await _organizationRepository.UpdatePlanAsync(
                Organization,
                changePlan.NewPlan,
                Organization.StripeCustomerId,
                Organization.StripeSubscriptionId,
                Organization.PurchasedSeatCount);

            await AuditLog.LogAuditEventAsync(
                new()
                {
                    Type = new("Organization.Plan", "Changed"),
                    Actor = Viewer,
                    Organization = Organization,
                    Description = $"Changed the organization to {changePlan.NewPlan} plan.",
                    StaffPerformed = true,
                    StaffOnly = true,
                });
            StatusMessage = $"Organization Plan set to `{changePlan.NewPlan}`.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLinkStripeAsync(string id, UpdateStripeConnectionModel updateStripeConnection)
    {
        await InitializeDataAsync(id);

        Organization.StripeCustomerId = updateStripeConnection.CustomerId;
        Organization.StripeSubscriptionId = updateStripeConnection.SubscriptionId;
        StatusMessage = "Stripe account linked.";
        await Db.SaveChangesAsync();
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization.StripeAccount", "Linked"),
                Actor = Viewer,
                Organization = Organization,
                Description = "Linked a subscription to the organization",
                StaffPerformed = true,
                StaffOnly = true,
            });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnlinkStripeAsync(string id)
    {
        await InitializeDataAsync(id);

        Organization.StripeCustomerId = null;
        Organization.StripeSubscriptionId = null;
        StatusMessage = "Stripe account unlinked.";
        await Db.SaveChangesAsync();
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Organization.StripeAccount", "Unlinked"),
                Actor = Viewer,
                Organization = Organization,
                Description = "Unlinked a subscription from the organization",
                StaffPerformed = true,
                StaffOnly = true,
            });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStartTrialAsync(string id)
    {
        await InitializeDataAsync(id);

        if (Organization.PlanType != PlanType.Free)
        {
            StatusMessage = "Cannot start a trial unless the organization is on the Free plan.";
            return RedirectToPage();
        }

        await _organizationRepository.StartTrialAsync(Organization,
            new TrialPlan(PlanType.Business, DateTime.UtcNow + TrialPlan.TrialLength));

        StatusMessage = "Started trial.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExtendTrialAsync(string id)
    {
        await InitializeDataAsync(id);

        if (Organization.Trial is null)
        {
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot extend a trial unless the organization is on a trial.";
            return RedirectToPage();
        }

        if (ExtensionDays is null)
        {
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot extend a trial without specifying the number of days.";
            return RedirectToPage();
        }

        var extension = TimeSpan.FromDays(ExtensionDays.Value);
        await _organizationRepository.ExtendTrialAsync(Organization, extension, "Extended by staff.", null);

        StatusMessage = $"Extended trial by {extension.Humanize()}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelTrialAsync(string id)
    {
        await InitializeDataAsync(id);

        if (Organization.Trial is null)
        {
            StatusMessage = "Cannot cancel a trial unless the organization is on a trial.";
            return RedirectToPage();
        }

        await _organizationRepository.EndTrialAsync(Organization, "Cancelled by staff.", null);

        StatusMessage = "Cancelled trial.";

        return RedirectToPage();
    }

    public record UpdateStripeConnectionModel(string? CustomerId, string? SubscriptionId);

    public record ChangePlanModel(PlanType NewPlan);
}
