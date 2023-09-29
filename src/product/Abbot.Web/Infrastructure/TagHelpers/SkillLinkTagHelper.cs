using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.AspNetCore.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

/// <summary>
/// Tag helper for rendering links to a skill feature page. This understands how to preserve
/// navigation elements in the query string so we can return to the skill list page with the current
/// filter and page applied, for example.
/// </summary>
[HtmlTargetElement("skilllink")]
public class SkillLinkTagHelper : AnchorTagHelper
{
    readonly IActionContextAccessor _actionContextAccessor;

    public SkillLinkTagHelper(IHtmlGenerator generator, IActionContextAccessor actionContextAccessor) : base(generator)
    {
        _actionContextAccessor = actionContextAccessor;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var actionContext = _actionContextAccessor.ActionContext;
        if (actionContext is null)
        {
            return;
        }
        var query = actionContext.HttpContext.Request.Query;
        var currentPage = actionContext.RouteData.Values["page"] as string;
        var skill = actionContext.RouteData.Values["skill"] as string;
        var pageNumber = actionContext.RouteData.Values["p"] as string ?? query["p"];
        var filter = actionContext.RouteData.Values["filter"] as string ?? query["filter"];
        var source = actionContext.RouteData.Values["source"] as string ?? query["source"];
        if (source is null or { Length: 0 } && currentPage is "/Skills/Index")
        {
            source = "skills";
        }

        if (skill is not null && !RouteValues.ContainsKey("skill"))
        {
            RouteValues.Add("skill", skill);
        }
        if (source is { Length: > 0 } && !RouteValues.ContainsKey("source"))
        {
            RouteValues.Add("source", source);
        }
        if (filter is { Length: > 0 } && !RouteValues.ContainsKey("filter"))
        {
            RouteValues.Add("filter", filter);
        }
        if (pageNumber is { Length: > 0 } && !RouteValues.ContainsKey("p"))
        {
            RouteValues.Add("p", pageNumber);
        }

        if (currentPage is "/Skills/Index")
        {
            RouteValues.Add("page", currentPage);
        }

        output.TagName = "a";
        if (Page is not "Edit" and not "Delete" && !context.AllAttributes.ContainsName("class"))
        {
            output.AppendAttributeValue("class", "button is-link", context);
        }

        base.Process(context, output);
    }
}
