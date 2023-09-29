using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.AspNetCore.TagHelpers;

[HtmlTargetElement("cronhour")]
public class CronHourTagHelper : SelectTagHelper
{
    public override int Order { get; } = -1000;

    public CronHourTagHelper(IHtmlGenerator generator) : base(generator)
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
        for (var i = 0; i < 12; i++)
        {
            int hour = i == 0 ? 12 : i;
            yield return new SelectListItem($"{hour:00}:00 AM", $"0 {i} * * *");
            yield return new SelectListItem($"{hour:00}:30 AM", $"30 {i} * * *");
        }
        yield return new SelectListItem("12:00 PM", "0 12 * * *");
        yield return new SelectListItem("12:30 PM", "30 12 * * *");
        for (var i = 1; i < 12; i++)
        {
            yield return new SelectListItem($"{i:00}:00 PM", $"0 {i + 12} * * *");
            yield return new SelectListItem($"{i:00}:30 PM", $"30 {i + 12} * * *");
        }
    }
}
