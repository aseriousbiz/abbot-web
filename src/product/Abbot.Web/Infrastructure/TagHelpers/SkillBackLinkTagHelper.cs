using System.Globalization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.AspNetCore.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

/// <summary>
/// Tag helper for rendering back links from a skill feature page. This understands how to preserve
/// navigation elements in the query string so we can return to the skill list page with the current
/// filter and page applied, for example. Also understands which page to go back to based on the source.
/// </summary>
[HtmlTargetElement("skillbacklink")]
public class SkillBackLinkTagHelper : AnchorTagHelper
{
    readonly IActionContextAccessor _actionContextAccessor;

    public SkillBackLinkTagHelper(IHtmlGenerator generator, IActionContextAccessor actionContextAccessor) : base(generator)
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
        var currentPage = actionContext.RouteData.Values["page"].Require<string>();
        var skill = actionContext.RouteData.Values["skill"].Require<string>();
        var pageNumber = actionContext.RouteData.Values["p"] as string ?? query["p"];
        var filter = actionContext.RouteData.Values["filter"] as string ?? query["filter"];
        var source = actionContext.RouteData.Values["source"] as string ?? query["source"];

        var isEditCreateOrDeletePage = currentPage.EndsWithAny("/Edit", "/Delete", "/Create");

        var (page, content, addSkillRoute, addPageFilterRoute) = (source, isEditPage: isEditCreateOrDeletePage) switch
        {
            ("all", _) => ("/Skills/Patterns/All", "Back to Patterns", false, false),
            ("skills", false) => ("/Skills/Index", "Back to Skills", false, true),
            (_, false) => ("/Skills/Edit", "Back to Skill Editor", true, false),
            (_, true) => ("Index", "Back", true, true)
        };

        // When navigating to skill edit page or a skill feature index (signals, patterns, etc.)
        if (addSkillRoute && !RouteValues.ContainsKey("skill"))
        {
            RouteValues.Add("skill", skill);
        }

        // When navigating to skill feature index (signals, patterns, etc.)
        if (isEditCreateOrDeletePage && addPageFilterRoute)
        {
            if (source is { Length: > 0 } && !RouteValues.ContainsKey("source"))
            {
                RouteValues.Add("source", source);
            }
        }
        // When navigating back to skills list.
        if (addPageFilterRoute)
        {
            if (filter is { Length: > 0 } && !RouteValues.ContainsKey("filter"))
            {
                RouteValues.Add("filter", filter);
            }
            if (pageNumber is { Length: > 0 } && !RouteValues.ContainsKey("p"))
            {
                RouteValues.Add("p", pageNumber.ToString(CultureInfo.InvariantCulture));
            }
        }

        Page = page;
        output.Content.SetContent(content);

        output.TagName = "a";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (!context.AllAttributes.ContainsName("class"))
        {
            output.AppendAttributeValue("class", "button is-link is-light", context);
        }

        base.Process(context, output);
    }
}
