using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Billing;

/// <summary>
/// Represents metadata about a billing action.
/// The values in this type can be encoded into a Stripe Checkout session.
/// Any changes that occur during that session will be posted back to the Billing Webhook
/// and this context can be restored from the metadata provided by Stripe.
/// </summary>
/// <param name="OrganizationId">The organization this billing event occurred on.</param>
/// <param name="MemberId">The ID of the member who initiated the billing event.</param>
public record BillingContext(
    Id<Organization> OrganizationId,
    Id<Member> MemberId)
{
    public Dictionary<string, string> ToMetadata()
    {
        return new()
        {
            { "organizationId", OrganizationId.ToString() },
            { "memberId", MemberId.ToString() },
        };
    }

    public static bool TryDecodeMetadata(IDictionary<string, string> metadata,
        [NotNullWhen(true)] out BillingContext? context)
    {
        if (!metadata.TryGetValue("organizationId", out var orgIdStr)
            || !Id<Organization>.TryParse(orgIdStr, out var orgId))
        {
            context = null;
            return false;
        }

        if (!metadata.TryGetValue("memberId", out var memIdStr)
            || !Id<Member>.TryParse(memIdStr, out var memId))
        {
            context = null;
            return false;
        }

        context = new(orgId, memId);
        return true;
    }
}
