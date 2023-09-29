using System;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("page-header")]
public class PageHeaderTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "header";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.PreContent.AppendHtml("<h1 class=\"font-semibold text-2xl\">");
        output.PostContent.AppendHtml("</h1>");
    }
}

[HtmlTargetElement("page-body")]
public class PageBodyTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass("flex", HtmlEncoder.Default);
        output.AddClass("flex-col", HtmlEncoder.Default);
        output.AddClass("gap-4", HtmlEncoder.Default);
    }
}

[HtmlTargetElement("round-box")]
public class RoundBoxTagHelper : TagHelper
{
    public bool Padding { get; set; } = true;
    public BorderSize RoundedSize { get; set; } = BorderSize.XLarge;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass(RoundedSize.ApplySize("rounded"), HtmlEncoder.Default);
        output.AddClass("bg-white", HtmlEncoder.Default);
        output.AddClass("border", HtmlEncoder.Default);
        output.AddClass("border-gray-300", HtmlEncoder.Default);

        if (Padding)
        {
            output.AddClass("p-6", HtmlEncoder.Default);
        }
    }
}

public enum BorderSize
{
    Normal,
    Medium,
    Large,
    XLarge,
    XXLarge,
    XXXLarge,
    Full,
}

public static class BorderSizeExtensions
{
    public static string ApplySize(this BorderSize size, string baseClass) => size switch
    {
        BorderSize.Normal => baseClass,
        BorderSize.Medium => $"{baseClass}-md",
        BorderSize.Large => $"{baseClass}-lg",
        BorderSize.XLarge => $"{baseClass}-xl",
        BorderSize.XXLarge => $"{baseClass}-2xl",
        BorderSize.XXXLarge => $"{baseClass}-3xl",
        BorderSize.Full => $"{baseClass}-full",
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
    };
}

// Kinda ugly, but the easiest way to get this to behave exactly like the built-in 'a' tag helper
[HtmlTargetElement("back-link")]
[HtmlTargetElement("back-link", Attributes = "asp-action")]
[HtmlTargetElement("back-link", Attributes = "asp-controller")]
[HtmlTargetElement("back-link", Attributes = "asp-area")]
[HtmlTargetElement("back-link", Attributes = "asp-page")]
[HtmlTargetElement("back-link", Attributes = "asp-page-handler")]
[HtmlTargetElement("back-link", Attributes = "asp-fragment")]
[HtmlTargetElement("back-link", Attributes = "asp-host")]
[HtmlTargetElement("back-link", Attributes = "asp-protocol")]
[HtmlTargetElement("back-link", Attributes = "asp-route")]
[HtmlTargetElement("back-link", Attributes = "asp-all-route-data")]
[HtmlTargetElement("back-link", Attributes = "asp-route-*")]
public class BackLinkTagHelper : AnchorTagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        base.Process(context, output);
        output.TagName = "a";
        output.AddClass("mt-4", HtmlEncoder.Default);
        output.AddClass("font-medium", HtmlEncoder.Default);
        output.AddClass("text-sm", HtmlEncoder.Default);
        output.AddClass("text-blue-500", HtmlEncoder.Default);
        output.AddClass("hover:text-gray-700", HtmlEncoder.Default);
        output.PreContent.Append("← ");
    }

    public BackLinkTagHelper(IHtmlGenerator generator) : base(generator)
    {
    }
}
