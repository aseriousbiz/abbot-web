using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serious.Abbot.Billing;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Serious.AspNetCore;
using Stripe.Checkout;
using SessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using SessionService = Stripe.BillingPortal.SessionService;

namespace Serious.Abbot.Controllers;

[AbbotWebHost]
[Authorize(AuthorizationPolicies.RequireAdministratorRole)]
[Route("/subscription")]
public class SubscriptionController : Controller
{
    readonly IOptions<StripeOptions> _stripeOptions;

    public SubscriptionController(IOptions<StripeOptions> stripeOptions)
    {
        _stripeOptions = stripeOptions;
    }

    [HttpPost("manage")]
    public async Task<IActionResult> Manage(string? returnUrl = null)
    {
        var organization = HttpContext.RequireCurrentOrganization();
        if (returnUrl is not { Length: > 0 } || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/Index";
        }
        var returnUri = Request.GetFullyQualifiedUrl(returnUrl);

        var options = new SessionCreateOptions
        {
            Customer = organization.StripeCustomerId,
            ReturnUrl = returnUri.ToString(),
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        // Redirect the user to the Stripe Billing Portal
        return Redirect(session.Url);
    }

    [HttpPost("upgrade")]
    public async Task<IActionResult> Upgrade(int? qty = 1, string? returnUrl = null)
    {
        var member = HttpContext.RequireCurrentMember();
        var organization = member.Organization;
        if (organization.PlanType != PlanType.Free)
        {
            TempData["StatusMessage"] = $"You cannot upgrade this plan, contact {WebConstants.SupportEmail} for assistance.";
            return RedirectToPage("/Settings/Organization/Billing/Index");
        }

        // If the user is already on a business plan trial, pass that along to Stripe so they get the remainder of the trial free.
        long? trialPeriodDays = null;
        if (organization.Trial is { } trial && trial.Plan == PlanType.Business)
        {
            var totalDays = (long)Math.Ceiling((trial.Expiry - DateTime.UtcNow).TotalDays);

            // Stripe requires the trial period be at least 1 day
            trialPeriodDays = Math.Max(totalDays, 1);
        }

        var context = new BillingContext(organization, member);

        int quantity = Math.Max(qty ?? 1, 1);
        var options = new Stripe.Checkout.SessionCreateOptions
        {
            Customer = organization.StripeCustomerId,
            ClientReferenceId = $"{organization.Id}",
            SuccessUrl = Request.GetFullyQualifiedUrl(Url.Action("CheckoutSuccess", "Subscription", new {
                returnUrl,
                sessionId = "{CHECKOUT_SESSION_ID}"
            })!).ToString(),
            CancelUrl = Request.GetFullyQualifiedUrl(Url.Action("CheckoutCanceled", "Subscription", new {
                returnUrl,
                sessionId = "{CHECKOUT_SESSION_ID}"
            })!).ToString(),
            Mode = "subscription", // Use Stripe Billing to set up fixed-price subscriptions.
            AllowPromotionCodes = true,
            SubscriptionData = new()
            {
                TrialPeriodDays = trialPeriodDays
            },
            LineItems = new()
            {
                new()
                {
                    Price = _stripeOptions.Value.BusinessPlanPriceId,
                    Quantity = quantity,
                    AdjustableQuantity = new SessionLineItemAdjustableQuantityOptions
                    {
                        Enabled = true,
                        Minimum = quantity,
                    }
                },
            },
            Metadata = context.ToMetadata(),
        };

        var service = new Stripe.Checkout.SessionService();
        var session = await service.CreateAsync(options);

        // Redirect the user to the Stripe Billing Portal
        return Redirect(session.Url);
    }

    [HttpGet("upgrade/success")]
    public IActionResult CheckoutSuccess([FromQuery] string? returnUrl = null, [FromQuery] string? sessionId = null)
    {
        if (returnUrl is not { Length: > 0 } || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/Index";
        }
        var returnUri = Request.GetFullyQualifiedUrl(returnUrl);

        TempData["StatusMessage"] = "Your subscription has been updated.";
        return Redirect(returnUri.ToString());
    }

    [HttpGet("upgrade/canceled")]
    public IActionResult CheckoutCanceled([FromQuery] string? returnUrl = null)
    {
        if (returnUrl is not { Length: > 0 } || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/Index";
        }
        var returnUri = Request.GetFullyQualifiedUrl(returnUrl);

        return Redirect(returnUri.ToString());
    }
}
