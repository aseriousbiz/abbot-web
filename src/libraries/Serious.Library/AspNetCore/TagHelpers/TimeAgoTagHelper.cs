using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.AspNetCore.TagHelpers;

/// <summary>
/// Tag helper for displaying dates as a time-ago. Usage:
/// <time-ago datetime="DateTimeOffset"></time-ago>
/// </summary>
[HtmlTargetElement("timeago")]
public class TimeAgoTagHelper : TagHelper
{
    static readonly List<string> OverwrittenAttributes = new() { "datetime", "title", "class" };

    // This is set via the datetime="" attribute the same way the <time> element works.
    // The casing of Datetime here is on purpose.
    /// <summary>
    /// The UTC Date and Time
    /// </summary>
    public DateTime? Datetime { get; set; }

    public TooltipPosition TooltipPosition { get; set; } = TooltipPosition.Top;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Datetime is null)
        {
            output.SuppressOutput();
            return;
        }

        DateTimeOffset timestamp = DateTime.SpecifyKind(Datetime.Value, DateTimeKind.Utc);
        string isoDate = timestamp.ToString("o", CultureInfo.InvariantCulture);
        string displayDate = timestamp.ToString("f", CultureInfo.InvariantCulture);
        output.TagName = "time";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("datetime", isoDate);
        output.SetAttributeValueIfNotExist("data-tooltip", isoDate, context);

        var tooltipPositionClass = TooltipPosition switch
        {
            TooltipPosition.Bottom => "has-tooltip-bottom",
            TooltipPosition.Left => "has-tooltip-left",
            TooltipPosition.Right => "has-tooltip-right",
            _ => "",
        };
        var classes = $"has-tooltip-arrow timeago {tooltipPositionClass}";

        output.AppendAttributeValue("class", classes, context);

        foreach (var attribute in context.AllAttributes.Where(a => !OverwrittenAttributes.Contains(a.Name, StringComparer.OrdinalIgnoreCase)))
        {
            output.Attributes.SetAttribute(attribute.Name, attribute.Value);
        }

        output.Content.SetContent(displayDate);
    }
}
