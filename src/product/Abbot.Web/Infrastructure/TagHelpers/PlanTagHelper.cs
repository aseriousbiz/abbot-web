using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Models;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("plan")]
public class PlanTagHelper : TagHelper
{
    /// <summary>
    /// A list of <see cref="PlanType"/>s that are allowed to see the content of this tag helper.
    /// If the organization's Plan matches any of the provided values, the content will be displayed.
    /// If both this and <see cref="Features"/> are set, the organization must match _either_ of the two requirements.
    /// </summary>
    public IReadOnlyList<PlanType>? Types { get; set; }

    /// <summary>
    /// A set of <see cref="PlanFeature"/>s (combined with bitwise-OR) that an organization must have in order to see the content of this tag helper.
    /// If both this and <see cref="Type"/> are set, the organization must match _either_ of the two.
    /// </summary>
    public PlanFeature Features { get; set; }

    /// <summary>
    /// Negates the evaluation for whether or not a plan tag should display content.
    /// </summary>
    public bool Negate { get; set; }

    // MVC will inject a ViewContext here.
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Don't render this tag itself.
        output.TagName = null;

        // Get the organization
        var organization = ViewContext.HttpContext.GetCurrentOrganization();

        var matched = IsMatch(organization);
        if (Negate)
        {
            matched = !matched;
        }

        if (!matched)
        {
            output.SuppressOutput();
        }

        return Task.CompletedTask;
    }

    bool IsMatch(Organization? organization)
    {
        if (organization is null)
        {
            return false;
        }

        if (Types is { Count: > 0 } && Types.Contains(organization.PlanType))
        {
            return true;
        }

        if (Features is not PlanFeature.None && (organization.HasPlanFeature(Features)))
        {
            return true;
        }

        return false;
    }
}
