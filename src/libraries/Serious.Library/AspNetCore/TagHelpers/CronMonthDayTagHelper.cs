using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.AspNetCore.TagHelpers;

[HtmlTargetElement("cronmonthday")]
public class CronMonthDayTagHelper : SelectTagHelper
{
    public override int Order { get; } = -1000;

    public CronMonthDayTagHelper(IHtmlGenerator generator) : base(generator)
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
        for (var day = 1; day <= 31; day++)
        {
            yield return new SelectListItem($"{day.ToOrdinal()}", $"0 0 {day} * *");
        }
        yield return new SelectListItem("Last Day", "0 0 L * *");
        yield return new SelectListItem("Last Week Day", "0 0 LW * *");
    }
}
