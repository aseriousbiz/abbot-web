using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.AspNetCore.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("blankslate")]
public class BlankSlateTagHelper : TagHelper
{
    /*
     <article class="bg-gray-50 p-8">
        <div class="text-center">
            <p>{CONTENT}</p>
        </div>
    </article>
    */
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "article";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AppendAttributeValue("class", "p-8", context);
        var content = await output.GetChildContentAsync();
        output.Content.SetHtmlContent($@"<div class=""text-center"">
                    {new HtmlString(content.GetContent())}                 
                </div>");
    }
}
