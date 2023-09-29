using System;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

/// CREDIT: https://github.com/aspnet/AspNetCore/blob/master/src/Mvc/Mvc.TagHelpers/src/LabelTagHelper.cs
/// <summary>
/// <see cref="ITagHelper"/> Implements a new tag &lt;description asp-for="Expression" /&gt; that
/// displays the metadata description.
/// </summary>
[HtmlTargetElement("description")]
public class DescriptionTagHelper : TagHelper
{
    const string ForAttributeName = "asp-for";

    /// <summary>
    /// An expression to be evaluated against the current model.
    /// </summary>
    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression? For { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        var description = For?.ModelExplorer?.Metadata?.Description;
        if (description is null)
            return;

        output.TagName = "small";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "form-text text-muted");
        output.Content.SetContent(description);
    }
}
