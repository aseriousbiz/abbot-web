using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;
using Stripe;
using Plan = Serious.Abbot.Models.Plan;

namespace Serious.Abbot.Pages.Settings.Organization.Billing;

public class IndexPage : AdminPage
{
    readonly IUserRepository _userRepository;
    readonly StripeOptions _stripeOptions;

    public IndexPage(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOptions<StripeOptions> stripeOptions,
        IAuditLog auditLog)
        : base(organizationRepository, auditLog)
    {
        _userRepository = userRepository;
        _stripeOptions = stripeOptions.Value;
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Billing", new { Id = Organization.PlatformId });

    public bool CurrentPlanIsPerSeat => CurrentPlan.Type is PlanType.Business;

    public int? PurchasedSeats { get; private set; }

    public IReadOnlyList<SubscriptionLineItem> LineItems { get; private set; } = null!;

    public CouponInfo? Coupon { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal Total { get; private set; }

    public decimal TotalDiscount { get; private set; }

    /// <summary>
    /// If <c>true</c>, the organization is in the middle of a free trial.
    /// </summary>
    [MemberNotNullWhen(true, "TrialPlan", "TrialPlanFeatures")]
    public bool IsUnexpiredTrial { get; private set; }

    public int AgentCount { get; private set; }

    /// <summary>
    /// The current plan the customer is on.
    /// </summary>
    public Plan CurrentPlan { get; private set; } = null!;

    public decimal CurrentPlanUnitPrice { get; private set; }

    /// <summary>
    /// If the customer is on a trial plan, this will not be null.
    /// </summary>
    public TrialPlan? TrialPlan { get; private set; }

    public Plan? TrialPlanFeatures { get; private set; }

    public decimal TrialPlanUnitPrice { get; private set; }

    public bool HasSubscription => Organization.StripeSubscriptionId is not null;

    public bool CanUpgrade => Organization is { StripeSubscriptionId: null, PlanType: PlanType.Free };

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentPlan = Organization.GetPlan();

        var priceService = new PriceService();
        var businessPlanPriceInfo = await priceService.GetAsync(_stripeOptions.BusinessPlanPriceId);
        decimal businessPlanUnitPrice = businessPlanPriceInfo.UnitAmountDecimal.GetValueOrDefault() / 100;

        CurrentPlanUnitPrice = CurrentPlan.Type is PlanType.Business
            ? businessPlanUnitPrice
            : CurrentPlan.MonthlyPrice;

        AgentCount = await _userRepository.GetActiveMembersQueryable(Organization)
            .Where(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .CountAsync();

        TrialPlan = Organization.Trial;

        IsUnexpiredTrial = TrialPlan is { Expiry: var expiry } && expiry > DateTimeOffset.UtcNow;

        if (IsUnexpiredTrial)
        {
            TrialPlanFeatures = TrialPlan.Plan.GetFeatures();
            TrialPlanUnitPrice = TrialPlan.Plan is PlanType.Business
                ? businessPlanUnitPrice
                : TrialPlanFeatures.MonthlyPrice;
        }

        var lineItems = new List<SubscriptionLineItem>();
        Discount? discount = null;

        if (Organization.StripeSubscriptionId is { Length: > 0 } stripeSubscriptionId)
        {
            var service = new SubscriptionService();
            var subscriptionInfo = await service.GetAsync(stripeSubscriptionId);
            var item = subscriptionInfo.Items.Single(); // We assume one item per subscription everywhere.

            Expect.True(item.Quantity <= int.MaxValue);

            PurchasedSeats = (int)item.Quantity;
            if (Organization.PurchasedSeatCount != PurchasedSeats.Value)
            {
                // In case we missed the webhook, we update the seat count only.
                await OrganizationRepository.UpdatePlanAsync(
                    Organization,
                    Organization.PlanType,
                    Organization.StripeCustomerId,
                    Organization.StripeSubscriptionId,
                    PurchasedSeats.Value);
            }

            lineItems.Add(new SubscriptionLineItem(
                    item.Subscription,
                    item.Plan.Id == _stripeOptions.BusinessPlanPriceId,
                    item.Price.UnitAmountDecimal.GetValueOrDefault() / 100,
                    item.Quantity,
                    (decimal)item.Price.UnitAmount.GetValueOrDefault() * item.Quantity / 100));

            discount = subscriptionInfo.Discount;
        }
        else
        {
            int agentCount = Math.Max(AgentCount, 1);
            lineItems.Add(new SubscriptionLineItem(
                "Business Plan",
                true,
                businessPlanUnitPrice,
                agentCount,
                agentCount * businessPlanUnitPrice));
        }

        Subtotal = lineItems.Sum(i => i.Total);
        var total = Subtotal;

        if (discount is not null)
        {
            var amount = discount.Coupon.AmountOff ?? discount.Coupon.PercentOff;
            var couponType = discount.Coupon.AmountOff is null
                ? CouponType.Percent
                : CouponType.Fixed;

            Coupon = new CouponInfo(
                discount.Coupon.Name,
                (amount ?? 0) / 100,
                couponType);

            TotalDiscount = couponType is CouponType.Fixed
                ? Coupon.Amount
                : Coupon.Amount * total;
        }

        Total = total - TotalDiscount;

        LineItems = lineItems;

        return Page();
    }

    public record SubscriptionLineItem(
        string Details,
        bool IsPerUnitPrice,
        decimal UnitPrice,
        long Quantity,
        decimal Total);

    public record CouponInfo(string Name, decimal Amount, CouponType Type);

    public enum CouponType
    {
        Fixed,
        Percent
    }
}
