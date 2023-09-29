using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.AspNetCore.TagHelpers;

[HtmlTargetElement("cronminute")]
public class CronMinuteTagHelper : SelectTagHelper
{
    public override int Order { get; } = -1000;

    public CronMinuteTagHelper(IHtmlGenerator generator) : base(generator)
    {
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "select";
        Items = GetItems();
        output.TagMode = TagMode.StartTagAndEndTag;
        base.Process(context, output);
    }

    static IEnumerable<SelectListItem> GetItems()
    {
        for (var minute = 0; minute < 60; minute += 10)
        {
            yield return new SelectListItem($"{minute:00}", $"{minute} * * * *");
        }
    }
}
