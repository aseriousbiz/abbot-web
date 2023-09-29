using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Stripe;
using Stripe.Checkout;

namespace Serious.Abbot.Billing;

public interface IBillingService
{
    Task CompleteCheckoutAsync(Session session, Subscription subscription);
    Task UpdateSubscriptionAsync(Subscription subscription);
    Task CancelSubscriptionAsync(Subscription subscription);
}

public class BillingService : IBillingService
{
    static readonly ILogger<BillingService> Log =
        ApplicationLoggerFactory.CreateLogger<BillingService>();

    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly IAuditLog _auditLog;

    public BillingService(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IAuditLog auditLog)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _auditLog = auditLog;
    }

    public async Task CompleteCheckoutAsync(Session session, Subscription subscription)
    {
        if (!BillingContext.TryDecodeMetadata(session.Metadata, out var context))
        {
            throw new InvalidOperationException($"Unable to decode billing context.");
        }

        var organization = await _organizationRepository.GetAsync(context.OrganizationId);
        if (organization is null)
        {
            throw new InvalidOperationException($"Organization {context.OrganizationId} not found.");
        }
        using var orgScope = Log.BeginOrganizationScope(organization);

        var member = await _userRepository.GetMemberByIdAsync(context.MemberId, organization);
        if (member is null)
        {
            throw new InvalidOperationException(
                $"MemberId {context.MemberId} does not match a member for the organization {organization.Id}.");
        }
        using var memberScope = Log.BeginMemberScope(member);

        var purchaserEmail = subscription.Customer?.Email;

        Log.RetrievedSubscription(
            subscription.Id,
            purchaserEmail,
            session.Customer?.Email);

        var userEmail = member.User.Email;
        if (purchaserEmail is not null && !purchaserEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
        {
            // This lets us connect future changes to this member.
            member.BillingEmail = purchaserEmail;
        }

        await _userRepository.UpdateUserAsync();

        await UpdateSubscriptionAsync(organization, subscription);

        await _auditLog.LogPurchaseSubscriptionAsync(subscription, member, organization);
    }

    public async Task UpdateSubscriptionAsync(Subscription subscription)
    {
        string stripeCustomerId = subscription.CustomerId;
        var organization = await _organizationRepository.GetByStripeCustomerIdAsync(stripeCustomerId);
        if (organization is null)
        {
            Log.FailedToChangeSubscriptionBecauseNoOrganizationFound("Update", subscription.Id, stripeCustomerId);
            return;
        }
        using var _ = Log.BeginOrganizationScope(organization);

        var purchaserEmail = subscription.Customer?.Email;
        var previousPlan = organization.PlanType;

        var changed = await UpdateSubscriptionAsync(organization, subscription);

        if (changed)
        {
            var user = await _userRepository.GetBillingUserByEmailAsync(purchaserEmail, organization);
            await _auditLog.LogChangeSubscriptionAsync(previousPlan, subscription, user, organization);
        }
    }

    public async Task CancelSubscriptionAsync(Subscription subscription)
    {
        var organization = await _organizationRepository.GetByStripeCustomerIdAsync(subscription.CustomerId);

        if (organization is null)
        {
            Log.FailedToChangeSubscriptionBecauseNoOrganizationFound("Cancel",
                subscription.Id,
                subscription.CustomerId);

            return;
        }
        using var _ = Log.BeginOrganizationScope(organization);

        var purchaserEmail = subscription.Customer?.Email;

        Log.AttemptingToCancelSubscription(
            subscription.Id,
            organization.PlanType.ToString(),
            purchaserEmail);

        var cancelledPlan = organization.PlanType;
        await _organizationRepository.UpdatePlanAsync(organization, PlanType.Free, null, null, 0);
        var user = await _userRepository.GetBillingUserByEmailAsync(subscription.Customer?.Email, organization);
        await _auditLog.LogCancelSubscriptionAsync(cancelledPlan, subscription, user, organization);
    }

    async Task<bool> UpdateSubscriptionAsync(
        Organization organization,
        Subscription subscription)
    {
        // Figure out what plan they bought
        if (subscription.Items.Data.Count != 1)
        {
            throw new InvalidOperationException("Expected a single subscription item");
        }

        // Check the plan type metadata if it's there
        var item = subscription.Items.Single();
        var targetPlanType = item.Price.Product.GetPlanType();

        var quantity = item.Quantity;
        var existingQuantity = organization.PurchasedSeatCount;

        // If the target plan type is null, it means this subscription doesn't have an associated plan type
        // We just leave it alone and assume that staff are managing the plan.
        if (targetPlanType is null || targetPlanType == organization.PlanType)
        {
            if (quantity == existingQuantity)
            {
                // Even though we're not changing the plan or quantity, we still may need to update the subscription ID
                Log.OrganizationAlreadyOnPlan(organization.PlanType.ToString(),
                    subscription.Id,
                    subscription.CustomerId);
            }
            else
            {
                Log.OrganizationUpdatingPlanQuantity(organization.PlanType.ToString(),
                    existingQuantity,
                    quantity,
                    subscription.Id,
                    subscription.CustomerId);
            }
        }
        else
        {
            Log.OrganizationPurchasingPlan(targetPlanType.Value.ToString(),
                organization.PlanType.ToString(),
                subscription.Id,
                subscription.CustomerId);
        }

        await _organizationRepository.UpdatePlanAsync(organization, targetPlanType ?? organization.PlanType, subscription.CustomerId, subscription.Id, (int)quantity);
        return true;
    }
}

static partial class BillingServiceLogExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message =
            "Retrieved subscription information (Id: {SubscriptionId}, Purchaser: {PurchaserEmail}, Customer: {CustomerEmail}")]
    public static partial void RetrievedSubscription(
        this ILogger logger,
        string subscriptionId,
        string? purchaserEmail,
        string? customerEmail);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message =
            "Organization is already on the {CurrentPlan} plan (Id: {SubscriptionId}, CustomerId: {CustomerId}")]
    public static partial void OrganizationAlreadyOnPlan(
        this ILogger logger,
        string currentPlan,
        string subscriptionId,
        string? customerId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message =
            "Organization purchasing plan! {CurrentPlan} plan (PreviousPlan: {PreviousPlan}, Id: {SubscriptionId}, CustomerId: {CustomerId}")]
    public static partial void OrganizationPurchasingPlan(
        this ILogger logger,
        string currentPlan,
        string previousPlan,
        string subscriptionId,
        string? customerId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message =
            "Organization updating plan quantity! {CurrentPlan} plan (PreviousQuantity: {PreviousQuantity}, NewQuantity: {NewQuantity}, Id: {SubscriptionId}, CustomerId: {CustomerId}")]
    public static partial void OrganizationUpdatingPlanQuantity(
        this ILogger logger,
        string currentPlan,
        long previousQuantity,
        long newQuantity,
        string subscriptionId,
        string? customerId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message =
            "Attempting to {ChangeType} subscription failed because no organization found (Id: {SubscriptionId}, Stripe CustomerId: {StripeCustomerId})")]
    public static partial void FailedToChangeSubscriptionBecauseNoOrganizationFound(
        this ILogger logger,
        string changeType,
        string subscriptionId,
        string stripeCustomerId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message =
            "Attempting to cancel subscription (Id: {SubscriptionId}, Current Plan: {CurrentPlan}, Purchaser: {PurchaserEmail}")]
    public static partial void AttemptingToCancelSubscription(
        this ILogger logger,
        string subscriptionId,
        string? currentPlan,
        string? purchaserEmail);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Customer Subscription {SubscriptionAction} (Id: {StripeEventId})")]
    public static partial void CustomerSubscriptionEvent(
        this ILogger logger,
        string subscriptionAction,
        string stripeEventId);
}
