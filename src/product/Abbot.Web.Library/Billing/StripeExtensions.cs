using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Models;

namespace Stripe;

public static class StripeExtensions
{
    public static PlanType? GetPlanType(this Product product)
    {
        if (!product.Metadata.TryGetValue("PlanType", out var s)
            || !Enum.TryParse<PlanType>(s, true, out var planType))
        {
            return null;
        }

        return planType;
    }

    public static async Task<decimal> CalculateMonthlyRecurringRevenue()
    {
        string? lastId = null;
        var customerClient = new CustomerService();
        decimal revenue = 0M;
        bool hasMore = true;
        while (hasMore)
        {
            var customers = await customerClient.ListAsync(
                new CustomerListOptions
                {
                    Limit = 100, /* Max Limit is 100 */
                    Expand = new List<string> { "data.subscriptions" },
                    StartingAfter = lastId
                });

            revenue += customers.Sum(CalculateCustomerMonthlyRevenue);
            hasMore = customers.HasMore;
            if (hasMore)
            {
                lastId = customers.LastOrDefault()?.Id;
                if (lastId is null)
                {
                    throw new InvalidOperationException("API reports more customers but no last id was returned.");
                }
            }
        }

        return revenue;
    }

    public static decimal CalculateCustomerMonthlyRevenue(Customer customer)
    {
        var subscriptions = customer.Subscriptions;
        var revenue = 0M;
        foreach (var subscription in subscriptions)
        {
            revenue += CalculateSubscriptionMonthlyRevenue(subscription);
        }
        // Apply the coupon, if any. We only look at % off coupons.
        // We can ignore the amount off discount. That's a one time discount and doesn't affect ongoing MRR.
        if (customer.Discount is { Coupon.PercentOff: { } percentOff })
        {
            revenue *= 1 - percentOff / 100M;
        }

        return revenue;
    }

    public static decimal CalculateSubscriptionMonthlyRevenue(Subscription subscription)
    {
        decimal revenue = 0;
        foreach (var item in subscription.Items)
        {
            var multiplier = item.Plan.Interval switch
            {
                "day" => 30M,
                "week" => 4M,
                "month" => 1M,
                "year" => 1M / 12M,
                _ => throw new UnreachableException($"Unexpected plan interval: {item.Plan.Interval}.")
            };
            revenue += multiplier * item.Quantity * item.Price.UnitAmountDecimal.GetValueOrDefault();
        }
        return revenue / 100M; // The UnitAmount is in cents.
    }
}
