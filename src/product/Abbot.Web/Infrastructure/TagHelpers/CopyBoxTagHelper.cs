using System.Net;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("copy-box", TagStructure = TagStructure.NormalOrSelfClosing)]
public class CopyBoxTagHelper : TagHelper
{
    readonly HtmlEncoder _htmlEncoder;

    /// <summary>
    /// The text to display in the copy box.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a boolean indicating if the 'size' field of the input should be set based on the value.
    /// </summary>
    public bool SizeToValue { get; set; }

    /// <summary>
    /// The CSS classes for the container DIV.
    /// </summary>
    public string? ContainerClasses { get; set; }

    /// <summary>
    /// Overrides the Css Classes for the input that displays the text to copy.
    /// </summary>
    public string? DisplayValueClasses { get; set; }

    /// <summary>
    /// Additional CSS classes for the clipboard button.
    /// </summary>
    public string? ClipboardClasses { get; set; }

    public CopyBoxTagHelper(HtmlEncoder htmlEncoder)
    {
        _htmlEncoder = htmlEncoder;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var containerCssClasses = (ContainerClasses ?? "flex").Split(" ");

        output.TagMode = TagMode.StartTagAndEndTag;
        output.TagName = "div";
        foreach (var cssClass in containerCssClasses)
        {
            output.AddClass(cssClass, _htmlEncoder);
        }
        output.Attributes.Add("data-controller", "clipboard");

        var sizeAttr = SizeToValue ? $" size=\"{Value.Length}\"" : string.Empty;

        var childContent = await output.GetChildContentAsync();
        if (childContent.IsEmptyOrWhiteSpace)
        {
            var cssClasses = DisplayValueClasses ?? "form-input-split flex-grow border-r border-gray-200 font-mono w-min";
            output.Content.AppendHtml(
                $@"<input readonly class=""{cssClasses}""{sizeAttr} value=""{WebUtility.HtmlEncode(Value)}"" data-clipboard-target=""content"" data-action=""click->clipboard#copy"" />");
        }
        else
        {
            var cssClasses = DisplayValueClasses ?? "flex-grow text-left px-2 py-1 bg-gray-50 rounded-l-md text-black shadow-inner border-gray-300 border clipboard has-tooltip-arrow is-expanded trigger-url";

            output.Content.AppendHtml($@"<button class=""{cssClasses}"" {sizeAttr} value=""{WebUtility.HtmlEncode(Value)}"" data-clipboard-target=""content"" data-action=""clipboard#copy"" data-tooltip=""Copy to clipboard"">");
            output.Content.AppendHtml(childContent);
            output.Content.AppendHtml(@"</button>");
        }

        output.Content.AppendHtml(
            $@"<button type=""button"" class=""form-input-button font-mono clipboard {ClipboardClasses}"" data-action=""clipboard#copy"" data-tooltip=""Copy to clipboard"">");
        output.Content.AppendHtml(@"<i class=""fa-regular fa-clipboard""></i>");
        output.Content.AppendHtml(@"</button>");
    }
}
