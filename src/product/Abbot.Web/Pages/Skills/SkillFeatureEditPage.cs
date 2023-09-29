using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Skills;

/// <summary>
/// Base class for pages where we're editing a feature of a skill such as secrets, triggers, etc.
/// </summary>
/// <remarks>
/// This contains logic for handling how navigate back to the appropriate page that brought us here. For example,
/// if a user clicks on a "patterns" tag in the skill list, we'll want to return there rather than the skill
/// editor.
/// </remarks>
public abstract class SkillFeatureEditPageModel : CustomSkillPageModel
{
    /// <summary>
    /// The name of the skill.
    /// </summary>
    public string SkillName => (RouteData.Values["skill"] as string ?? string.Empty).ToLowerInvariant();

    public Skill Skill { get; protected set; } = null!;

    [BindProperty(Name = "p", SupportsGet = true)]
    public int? PageNumber { get; set; } = 1;

    /// <summary>
    /// The filter on the skills list page that brought us here.
    /// </summary>
    [BindProperty(Name = "filter", SupportsGet = true)]
    public string? Filter { get; set; }

    protected IActionResult RedirectBack()
    {
        var pageNumber = PageNumber ?? 1;
        var source = Request.Query["source"].ToString();
        if (source is "all")
        {
            return RedirectToPage("All");
        }

        var indexRouteValues = new RouteValueDictionary(new {
            skill = SkillName,
            source,
            p = pageNumber,
            filter = Filter
        });

        return RedirectToPage("Index", indexRouteValues);
    }
}

/// <summary>
/// Base class for any page that that displays or modifies a custom skill.
/// </summary>
public abstract class CustomSkillPageModel : UserPage
{
    /// <summary>
    /// When <c>true</c> and custom skills are disabled, this page will return a <see cref="NotFoundResult"/>.
    /// </summary>
    /// <remarks>
    /// This should be <c>true</c> for any page related to custom skills except the skills index page,
    /// <see cref="IndexPage"/>, since that page shows a message about skills being disabled along with a link
    /// to enable them.
    /// </remarks>
    protected bool ReturnNotFoundIfCustomSkillsDisabled { get; set; } = true;

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        await base.OnPageHandlerExecutionAsync(context,
            async () => {
                if (ReturnNotFoundIfCustomSkillsDisabled && !Organization.UserSkillsEnabled)
                {
                    context.Result = NotFound();
                    return null!;
                }

                return await next();
            });
    }
}
