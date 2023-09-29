using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Models;

namespace Serious.Abbot.Entities;

public class BillingEvent : LegacyAuditEvent
{
    /// <summary>
    /// The new plan type if changed.
    /// </summary>
    public PlanType PlanType { get; set; }

    /// <summary>
    /// The Stripe subscription Id
    /// </summary>
    public string? SubscriptionId { get; set; }

    /// <summary>
    /// The Stripe Customer Id
    /// </summary>
    public string? CustomerId { get; set; }

    /// <summary>
    /// Email of the person who changed the subscription.
    /// </summary>
    public string? BillingEmail { get; set; }

    /// <summary>
    /// The name of the customer that made the purchase.
    /// </summary>
    [Column("Reason")] // Reusing existing column.
    public string? BillingName { get; set; }

    [NotMapped]
    public override bool HasDetails => true;
}
