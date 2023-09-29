using System.Collections.Generic;
using Humanizer.Localisation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Internal;
using Serious.Abbot.Pages;

namespace Serious.Abbot.Infrastructure.TagHelpers;

/// <summary>
/// <see cref="ITagHelper"/> implementation targeting &lt;a&gt; elements.
/// </summary>
[HtmlTargetElement("a", Attributes = ActionAttributeName)]
[HtmlTargetElement("a", Attributes = ControllerAttributeName)]
[HtmlTargetElement("a", Attributes = AreaAttributeName)]
[HtmlTargetElement("a", Attributes = PageAttributeName)]
[HtmlTargetElement("a", Attributes = PageHandlerAttributeName)]
[HtmlTargetElement("a", Attributes = FragmentAttributeName)]
[HtmlTargetElement("a", Attributes = HostAttributeName)]
[HtmlTargetElement("a", Attributes = ProtocolAttributeName)]
[HtmlTargetElement("a", Attributes = RouteAttributeName)]
[HtmlTargetElement("a", Attributes = RouteValuesDictionaryName)]
[HtmlTargetElement("a", Attributes = RouteValuesPrefix + "*")]
public class AnchorTagHelper : Microsoft.AspNetCore.Mvc.TagHelpers.AnchorTagHelper
{
    // Copied from the original AnchorTagHelper.cs because HtmlTargetElement isn't inherited.
    const string ActionAttributeName = "asp-action";
    const string ControllerAttributeName = "asp-controller";
    const string AreaAttributeName = "asp-area";
    const string PageAttributeName = "asp-page";
    const string PageHandlerAttributeName = "asp-page-handler";
    const string FragmentAttributeName = "asp-fragment";
    const string HostAttributeName = "asp-host";
    const string ProtocolAttributeName = "asp-protocol";
    const string RouteAttributeName = "asp-route";
    const string RouteValuesDictionaryName = "asp-all-route-data";
    const string RouteValuesPrefix = "asp-route-";

    static readonly HashSet<string> NonPreservedRouteValues = new HashSet<string>()
    {
        "page",
        "handler",
        "controller",
        "action",
        "area"
    };

    /// <summary>
    /// Creates a new <see cref="AnchorTagHelper"/>.
    /// </summary>
    /// <param name="generator">The <see cref="IHtmlGenerator"/>.</param>
    public AnchorTagHelper(IHtmlGenerator generator) : base(generator)
    {
    }

    /// <inheritdoc />
    // Run before the built-in ASP.NET Core one.
    public override int Order => -2000;

    /// <summary>
    /// Indicates if the 'staffOrganizationId' route value should be preserved when generating the href value.
    /// </summary>
    public bool PreserveStaff { get; set; }

    /// <summary>
    /// If set, initializes the route values EXCEPT 'page', 'handler', 'controller', 'action', and 'area' for the target link with the current route values.
    /// New values set by `asp-route-*` attributes will override the current route values.
    /// </summary>
    public bool PreserveRouteValues { get; set; }

    /// <inheritdoc />
    /// <remarks>Does nothing if user provides an <c>href</c> attribute.</remarks>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (PreserveRouteValues)
        {
            foreach (var (key, value) in ViewContext.RouteData.Values)
            {
                // Only add the route value if it's not already set by the tag and if it's not one of the "non-preserved" route values.
                if (!NonPreservedRouteValues.Contains(key) && !RouteValues.ContainsKey(key) && value is not null)
                {
                    RouteValues[key] = value.ToString() ?? string.Empty;
                }
            }
        }

        // We support optionally preserving _just_ the `staffOrganizationId` route value.
        if (PreserveStaff
            && ViewContext.RouteData.Values.TryGetValue("staffOrganizationId", out var v)
            && v is string staffOrganizationId)
        {
            RouteValues["staffOrganizationId"] = staffOrganizationId;
        }
        else if (!RouteValues.ContainsKey("staffOrganizationId"))
        {
            // Unless the tag explicitly sets `staffOrganizationId`, we want it to be explicitly empty.
            RouteValues["staffOrganizationId"] = null;
        }

        base.Process(context, output);
    }
}
