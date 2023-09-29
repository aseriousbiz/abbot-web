using System.Collections.Generic;
using System.Linq;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.FeatureManagement;

public static class FeatureHelper
{
    public static TargetingContext CreateTargetingContext(this Organization? organization, string? userId = null)
    {
        return new TargetingContext
        {
            // if the targeting is set for a specific user, this is the value it will check against
            UserId = userId,
            Groups = organization is null
                ? Enumerable.Empty<string>()
                : organization.GroupsForOrganization()
        };
    }

    static IEnumerable<string> GroupsForOrganization(this Organization organization)
    {
        yield return GroupForPlatformId(organization.PlatformId);
        yield return GroupForPlan(organization.GetPlan().Type);
        yield return GroupForOrganizationSlug(organization.Slug);
        if (organization.Domain != null)
            yield return GroupForDomain(organization.Domain);
    }

    public static string GroupForPlatformId(string platformId) => $"platformId:{platformId}";

    // We know plan types are ascii and lowercase looks better :).
    public static string GroupForPlan(PlanType planType) => $"plan:{planType.ToString().ToLowerInvariant()}";

    public static string GroupForOrganizationSlug(string slug) => $"slug:{slug}";
    public static string GroupForDomain(string domain) => $"domain:{domain}";
}
