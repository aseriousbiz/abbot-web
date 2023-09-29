using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("staff-menu", TagStructure = TagStructure.NormalOrSelfClosing)]
public class StaffMenuTagHelper : TagHelper
{
    static readonly object ItemsKey = typeof(StaffMenuTagHelper);

    [ViewContext]
    public required ViewContext ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        foreach (var zone in GetStaffMenuZones(ViewContext.HttpContext))
        {
            output.Content.AppendHtml(zone.GetContent());
        }
    }

    public static IReadOnlyList<TagHelperContent> GetStaffMenuZones(HttpContext httpContext)
    {
        if (httpContext.Items[ItemsKey] is not IReadOnlyList<TagHelperContent> items)
        {
            return Array.Empty<TagHelperContent>();
        }

        return items;
    }

    public static void StoreStaffMenuZone(HttpContext httpContext, TagHelperContent content)
    {
        if (httpContext.IsStaffMode())
        {
            if (httpContext.Items[ItemsKey] is not List<TagHelperContent> items)
            {
                items = new List<TagHelperContent>();
                httpContext.Items[ItemsKey] = items;
            }

            items.Add(content);
        }
    }
}

[HtmlTargetElement("staff-menu-zone", TagStructure = TagStructure.NormalOrSelfClosing)]
public class StaffMenuZoneTagHelper : TagHelper
{
    [ViewContext]
    public required ViewContext ViewContext { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Collect all the child content and suppress it
        output.SuppressOutput();
        var content = await output.GetChildContentAsync();
        StaffMenuTagHelper.StoreStaffMenuZone(ViewContext.HttpContext, content);
    }
}
