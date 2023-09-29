using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Models;

namespace Serious.Abbot.Infrastructure.Filters;

/// <summary>
/// Apply this to a Page Model to limit it to only organizations with the specified Plan feature.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequirePlanFeatureAttribute : Attribute, IAsyncPageFilter
{
    public PlanFeature Features { get; }

    /// <summary>
    /// Construct a new <see cref="RequirePlanFeatureAttribute" />.
    /// </summary>
    /// <param name="features">The feature, or features (combined with bitwise-OR), that the organization's plan must permit for the page to render.</param>
    public RequirePlanFeatureAttribute(PlanFeature features)
    {
        Features = features;
    }

    public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var org = context.HttpContext.GetCurrentOrganization();
        if (org is not null && org.HasPlanFeature(Features))
        {
            return next();
        }

        context.Result = new NotFoundResult();
        return Task.CompletedTask;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
}
