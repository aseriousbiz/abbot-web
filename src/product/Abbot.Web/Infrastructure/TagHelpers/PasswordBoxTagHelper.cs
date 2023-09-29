using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("input", Attributes = "[type=\"password\"]")]
public class PasswordBoxTagHelper : TagHelper
{
    readonly HtmlEncoder _htmlEncoder;

    /// <summary>
    /// Gets or sets a boolean indicating if the "show password" button should be displayed. Defaults to true.
    /// </summary>
    public bool ShowVisibilityButton { get; set; } = true;

    public override int Order => 0; // Run after the default input tag helper (all default tag helpers have negative order values)

    public PasswordBoxTagHelper(HtmlEncoder htmlEncoder)
    {
        _htmlEncoder = htmlEncoder;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PreElement.AppendHtml("<div class=\"flex form-input\" data-controller=\"password\">");
        output.AddClass("flex-grow", _htmlEncoder);
        output.AddClass("bg-transparent", _htmlEncoder);
        output.AddClass("pl-1", _htmlEncoder);
        output.Attributes.Add("data-password-target", "control");
        output.PostElement.AppendHtml("<button type=\"button\" data-action=\"password#toggleVisibility\">");
        output.PostElement.AppendHtml("<i data-password-target=\"showPasswordIcon\" class=\"icon fa fa-eye\"></i>");
        output.PostElement.AppendHtml("<i data-password-target=\"hidePasswordIcon\" class=\"hidden icon fa fa-eye-slash\"></i>");
        output.PostElement.AppendHtml("</button>");
        output.PostElement.AppendHtml("</div>");
    }
}
