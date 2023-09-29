using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("input", Attributes = "[show-clear-button]")]
public class ShowClearButtonTagHelper : TagHelper
{
    public string? ClearButtonContainerClass { get; set; }

    public override int Order => 0; // Run after the default input tag helper (all default tag helpers have negative order values)

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PreElement.AppendHtml($"""<div data-controller="clear-input" class="relative {ClearButtonContainerClass}">""");
        output.Attributes.Add("data-clear-input-target", "input");
        output.PostElement.AppendHtml("<button type=\"button\" class=\"absolute inset-y-0 right-0 items-center mr-1 pr-2\" data-clear-input-target=\"button\">");
        output.PostElement.AppendHtml("<i class=\"fa-solid fa-circle-xmark\"></i>");
        output.PostElement.AppendHtml("</button>");
        output.PostElement.AppendHtml("</div>");
    }
}
