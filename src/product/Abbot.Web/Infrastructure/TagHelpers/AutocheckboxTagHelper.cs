using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.AspNetCore.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("autocheckbox")]
public class AutocheckboxTagHelper : TagHelper
{
    readonly IUrlHelperFactory _urlHelperFactory;
    readonly IAntiforgery _antiforgery;

    [HtmlAttributeName("asp-page")]
    public string? Page { get; set; }

    [HtmlAttributeName("asp-page-handler")]
    public string? Handler { get; set; }

    public bool InitialValue { get; set; }

    public bool SuppressIndicator { get; set; }

    [HtmlAttributeName("readonly")]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets the name of the route value to use for the checkbox value.
    /// Defaults to 'value'
    /// </summary>
    public string Name { get; set; } = "value";

    [ViewContext]
    public required ViewContext ViewContext { get; set; }

    public AutocheckboxTagHelper(IUrlHelperFactory urlHelperFactory, IAntiforgery antiforgery)
    {
        _urlHelperFactory = urlHelperFactory;
        _antiforgery = antiforgery;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);
        var url = urlHelper.Page(Page, Handler);

        output.TagName = "form";
        output.Attributes.SetAttribute("class", "flex items-center gap-x-1");
        if (!ReadOnly)
        {
            output.Attributes.SetAttribute("action", url);
            output.Attributes.SetAttribute("method", "POST");
            output.Attributes.SetAttribute("data-controller", "autocheckbox");
            output.Attributes.SetAttribute("data-autocheckbox-suppress-indicator-value", SuppressIndicator);
        }

        var checkedAttribute = InitialValue
            ? " checked"
            : "";

        output.PreContent.AppendHtml(
            $"""<label class="relative btn inline-flex items-center cursor-pointer" for="{Name}">""");

        output.PreContent.AppendHtml(
            ReadOnly
                ? $"""<input type="checkbox" class="" id="{Name}" name="{Name}" disabled{checkedAttribute}>"""
                : $"""<input type="checkbox" class="" id="{Name}" name="{Name}" value="true" data-action="autocheckbox#toggle"{checkedAttribute}>""");

        output.PreContent.AppendHtml("""<span class="ml-1">""");
        output.PostContent.AppendHtml("</span></label>");
        output.PostContent.AppendHtml(_antiforgery.GetHtml(ViewContext.HttpContext));
        base.Process(context, output);
    }
}
