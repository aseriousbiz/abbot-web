#pragma warning disable CS8602, CA1813, CA1018
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.FeatureManagement;
using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Web;

/// <summary>
/// FeatureManagement "PageFeatureGate" attribute that can be put on PageModel classes to keep them from
/// being loaded unless a feature is enabled. It redirects to our custom 404 page if the user doesn't have
/// the feature enabled.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PageFeatureGateAttribute : Attribute, IAsyncPageFilter
{
    public string[] Features { get; }

    /// <summary>
    /// Controls whether 'All' or 'Any' feature in a list of features should be enabled to view the page.
    /// </summary>
    public RequirementType Requirement { get; set; } = RequirementType.All;

    public PageFeatureGateAttribute(params string[] features) => Features = features;

    public virtual async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var fm = context.HttpContext.RequestServices.GetRequiredService<FeatureService>();
        var actor = context.HttpContext.GetFeatureActor();
        var isEnabled = await fm.IsEnabledAsync(Features, actor, Requirement).ConfigureAwait(false);
        if (isEnabled)
        {
            await next.Invoke().ConfigureAwait(false);
        }
        else
        {
            context.Result = new StatusCodeResult(StatusCodes.Status404NotFound);
        }
    }

    public virtual Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
}

public class FeatureGateAttribute : Attribute, IAsyncActionFilter
{
    public string[] Features { get; }

    /// <summary>
    /// Controls whether 'All' or 'Any' feature in a list of features should be enabled to view the page.
    /// </summary>
    public RequirementType Requirement { get; set; } = RequirementType.All;

    public FeatureGateAttribute(params string[] features) => Features = features;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var fm = context.HttpContext.RequestServices.GetRequiredService<FeatureService>();
        var actor = context.HttpContext.GetFeatureActor();
        var isEnabled = await fm.IsEnabledAsync(Features, actor, Requirement).ConfigureAwait(false);
        if (isEnabled)
        {
            await next.Invoke().ConfigureAwait(false);
        }
        else
        {
            context.Result = new StatusCodeResult(StatusCodes.Status404NotFound);
        }
    }
}
