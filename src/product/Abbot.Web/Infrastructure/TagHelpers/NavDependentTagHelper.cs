using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.AspNetCore.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

/// <summary>
/// Tag helper for setting classes and other attributes on elements depending on the current page.
/// </summary>
[HtmlTargetElement(HtmlTargetElementAttribute.ElementCatchAllTarget, Attributes = "active-for")]
[HtmlTargetElement(HtmlTargetElementAttribute.ElementCatchAllTarget, Attributes = "nav-active-class")]
public class NavDependentTagHelper : TagHelper
{
    // We need to run after the built-in "asp-page", etc. tag helpers run.
    // They run at order -1000, so this should put us well clear of them.
    public override int Order => 1000;

    /// <summary>
    /// The 'class' attribute values to add to the element if the current page matches the specified route.
    /// </summary>
    [HtmlAttributeName("nav-active-class")]
    public string? ClassIfActive { get; set; }

    /// <summary>
    /// If set, the current URL must exactly match the 'href' or 'active-for' value of this element.
    /// If not set (the default), a prefix match is sufficient.
    /// </summary>
    [HtmlAttributeName("nav-exact-match")]
    public bool RequireExactMatch { get; set; }

    /// <summary>
    /// The path that the current page URL must match to activate the 'active-' attributes.
    /// If not set, the 'href' attribute value is used.
    /// </summary>
    [HtmlAttributeName("active-for")]
    public string? ActiveFor { get; set; }

    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var allowedUrls = new List<string>();
        if (ActiveFor is not { Length: > 0 })
        {
            var href = output.Attributes.FirstOrDefault(a => a.Name == "href")?.Value?.ToString()
                       ?? context.AllAttributes.FirstOrDefault(a => a.Name == "href")?.Value?.ToString();

            if (href is not { Length: > 0 })
            {
                return;
            }

            allowedUrls.Add(href);
        }
        else
        {
            // If Active For is set, we support a comma-separated list of paths.
            allowedUrls.AddRange(ActiveFor.Split(',').Select(s => s.Trim()));
        }

        foreach (var allowedUrl in allowedUrls)
        {
            var thisMatch = RequireExactMatch
                ? ViewContext.HttpContext.Request.Path.Equals(allowedUrl, StringComparison.OrdinalIgnoreCase)
                : ViewContext.HttpContext.Request.Path.StartsWithSegments(allowedUrl,
                    StringComparison.OrdinalIgnoreCase);

            if (thisMatch)
            {
                ApplyAttributes(context, output);
                return;
            }
        }
    }

    void ApplyAttributes(TagHelperContext context, TagHelperOutput output)
    {
        if (ClassIfActive is { Length: > 0 })
        {
            output.AppendAttributeValue("class", ClassIfActive, context);
        }
    }
}
