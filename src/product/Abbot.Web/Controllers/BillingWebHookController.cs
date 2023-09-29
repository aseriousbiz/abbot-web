using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Billing;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Services;
using Serious.Logging;
using Stripe;
using Stripe.Checkout;

namespace Serious.Abbot.Controllers;

[AllowAnonymous]
[AbbotWebHost]
public class BillingWebHookController : Controller
{
    static readonly ILogger<BillingWebHookController> Log = ApplicationLoggerFactory.CreateLogger<BillingWebHookController>();

    readonly IBillingService _billingService;
    readonly string _webhookSecret;

    public BillingWebHookController(IOptions<StripeOptions> options, IBillingService billingService)
    {
        _billingService = billingService;
        _webhookSecret = options.Value.WebhookSecret;
        StripeConfiguration.ApiKey = options.Value.SecretKey;
    }

    [HttpPost("billing-webhook")]
    public async Task<IActionResult> BillingWebhook()
    {
        Log.MethodEntered(typeof(BillingWebHookController), nameof(BillingWebhook), null);

        using var postBody = new StreamReader(HttpContext.Request.Body);
        var json = await postBody.ReadToEndAsync();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json,
                Request.Headers["Stripe-Signature"],
                _webhookSecret);

            await (stripeEvent.Type switch
            {
                Stripe.Events.CheckoutSessionCompleted => CompleteCheckoutSessionAsync(stripeEvent),
                Stripe.Events.CustomerSubscriptionUpdated => UpdateSubscriptionAsync(stripeEvent),
                Stripe.Events.CustomerSubscriptionDeleted => CancelSubscriptionAsync(stripeEvent),
                _ => Task.CompletedTask
            });

            return Ok();
        }
        catch (StripeException ex)
        {
            Log.InvalidStripeWebhookPayload(ex, ex.Message);

            // Log the error
            return BadRequest($"I failed to process the webhook. Request ID: {Activity.Current?.Id ?? string.Empty}");
        }
    }

    async Task CancelSubscriptionAsync(Event stripeEvent)
    {
        var canceledSubscription = await GetLoadedSubscriptionAsync(stripeEvent);
        await _billingService.CancelSubscriptionAsync(canceledSubscription);
    }

    async Task UpdateSubscriptionAsync(Event stripeEvent)
    {
        var updatedSubscription = await GetLoadedSubscriptionAsync(stripeEvent);
        await _billingService.UpdateSubscriptionAsync(updatedSubscription);
    }

    async Task CompleteCheckoutSessionAsync(Event stripeEvent)
    {
        var session = (Session)stripeEvent.Data.Object;
        var subscription = await GetSubscriptionAsync(session.SubscriptionId);
        await _billingService.CompleteCheckoutAsync(session, subscription);
    }

    static async Task<Subscription> GetLoadedSubscriptionAsync(Event stripeEvent)
    {
        var subscription = (Subscription)stripeEvent.Data.Object;
        if (subscription.Customer is null)
        {
            return await GetSubscriptionAsync(subscription.Id);
        }

        return subscription;
    }

    static async Task<Subscription> GetSubscriptionAsync(string subscriptionId)
    {
        var options = new SubscriptionGetOptions();
        options.AddExpand("customer");
        options.AddExpand("items.data.price.product");
        var subscriptionService = new SubscriptionService();
        return await subscriptionService.GetAsync(subscriptionId, options);
    }
}

static partial class BillingControllerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Payload for Stripe Webhook request was invalid: {ExceptionMessage}")]
    public static partial void InvalidStripeWebhookPayload(this ILogger<BillingWebHookController> logger, Exception ex, string? exceptionMessage);
}
