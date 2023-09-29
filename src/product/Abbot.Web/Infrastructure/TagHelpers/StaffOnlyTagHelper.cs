using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement(Attributes = "staff-only")]
public class StaffOnlyAttributeTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext? ViewContext { get; set; }

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext is null)
        {
            // We can't tell if the user is staff without the ViewContext, so to be safe, we don't render
            output.SuppressOutput();
            return Task.CompletedTask;
        }

        if (ViewContext.IsStaffMode())
        {
            // The content should be visible! Render away.
            return Task.CompletedTask;
        }

        // The content should be hidden, suppress it.
        output.SuppressOutput();
        return Task.CompletedTask;
    }
}

[HtmlTargetElement("staff-only", TagStructure = TagStructure.NormalOrSelfClosing)]
public class StaffOnlyTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext? ViewContext { get; set; }

    /// <summary>
    /// Set this to a condition that must be true in addition to the user being a staff member for the content to render
    ///</summary>
    public bool And { get; set; } = true;

    /// <summary>
    /// Set this to a condition that must be true to allow the content to render even when the user is not staff AND the <see cref="And"/> condition is false.
    /// </summary>
    public bool Or { get; set; }

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Never render this content
        output.TagName = null;

        if (ViewContext is null)
        {
            // We can't tell if the user is staff without the ViewContext, so to be safe, we don't render
            output.SuppressOutput();
            return Task.CompletedTask;
        }

        if (Or || (ViewContext.IsStaffMode() && And))
        {
            // The content should be visible! Render away.
            return Task.CompletedTask;
        }

        // The content should be hidden, suppress it.
        output.SuppressOutput();
        return Task.CompletedTask;
    }
}

[HtmlTargetElement("staff-icon", TagStructure = TagStructure.NormalOrSelfClosing)]
public class StaffIconTagHelper : TagHelper
{
    public bool Disable { get; set; }
    public string? Tooltip { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        Tooltip ??= "This is only visible to staff";
        output.Attributes.Add("data-tooltip", Tooltip);

        var icon = Disable ? "fa-light fa-shield" : "fa fa-shield-quartered";
        var classes = icon;
        output.Content.SetHtmlContent($"""<i class="{classes}"></i>""");
        output.TagMode = TagMode.StartTagAndEndTag;
    }
}
