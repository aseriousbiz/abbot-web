using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.AspNetCore.TagHelpers;
using Serious.Slack;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("emoji", TagStructure = TagStructure.NormalOrSelfClosing)]
public class EmojiTagHelper : TagHelper
{
    public Emoji? Emoji { get; set; }
    public bool IncludeTooltip { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        Emoji.Require();

        // Make sure we're emitting a start and end tag in the final output.
        output.TagMode = TagMode.StartTagAndEndTag;

        if (Emoji is UnicodeEmoji unicodeEmoji)
        {
            output.TagName = "span";
            output.Content.SetHtmlContent(unicodeEmoji.HtmlEncoded);
        }
        else if (Emoji is CustomEmoji customEmoji)
        {
            output.TagName = "img";
            output.Attributes.SetAttribute("src", customEmoji.ImageUrl);
            output.Attributes.SetAttribute("alt", $":{customEmoji.Name}:");
            output.Attributes.SetAttribute("class", "h-5 w-5");
        }
        else
        {
            output.TagName = "i";
            output.Attributes.SetAttribute("class", "fa-light fa-circle-question text-gray-500");
        }

        if (IncludeTooltip)
        {
            output.Attributes.SetAttribute("data-tooltip", $":{Emoji.Name}:");
        }
    }
}
