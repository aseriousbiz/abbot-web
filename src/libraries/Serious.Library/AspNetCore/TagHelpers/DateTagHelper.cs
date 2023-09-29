using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Humanizer;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.AspNetCore.TagHelpers;

[HtmlTargetElement("date")]
public class DateTagHelper : TagHelper
{
    static readonly List<string> OverwrittenAttributes = new() { "utc", "title", "class" };

    /// <summary>
    /// The UTC Date and Time
    /// </summary>
    public DateTime? Utc { get; set; }

    /// <summary>
    /// Use this to set the user's timezone.
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// Provides options on how to format the date.
    /// </summary>
    public DisplayDateFormat? Format { get; set; }

    /// <summary>
    /// If true, renders a tooltip.
    /// </summary>
    public bool IncludeUtcInTooltip { get; set; }

    /// <summary>
    /// If <see cref="Format"/> is set to <see cref="DisplayDateFormat.Custom"/>, this is the .NET DateTime format
    /// string used to format the date.
    /// </summary>
    public string? CustomFormat { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var dateTime = Utc ?? DateTime.UtcNow;

        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("DateTime must be UTC");
        }

        string isoDate = dateTime.ToString("o", CultureInfo.InvariantCulture);
        // Convert the date to the timezone.
        var tz = Timezone is { Length: > 0 } timezone ? timezone : "America/Los_Angeles";
        var localDateTime = dateTime.ToZonedDateTime(tz).ToDateTimeUnspecified();

        var dateFormat = CustomFormat is { Length: > 0 }
            ? CustomFormat
            : Format switch
            {
                DisplayDateFormat.FullDateTime => "MMMM d, yyyy h:mm tt",
                DisplayDateFormat.FullDate => "MMMM d, yyyy",
                DisplayDateFormat.ShortDateTime => "MMM d, yyyy h:mm tt",
                DisplayDateFormat.ShortDate => "MMM d, yyyy",
                DisplayDateFormat.TimeOnly => "h:mm tt",
                DisplayDateFormat.Humanize => "HUMANIZE",
                DisplayDateFormat.Custom => throw new InvalidOperationException("CustomFormat must be set if Format is set to Custom"),
                _ => "MMMM d, yyyy h:mm tt",
            };
        var displayDate = dateFormat is "HUMANIZE"
            ? localDateTime.Humanize()
            : localDateTime.ToString(dateFormat, CultureInfo.InvariantCulture);
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (IncludeUtcInTooltip)
        {
            output.SetAttributeValueIfNotExist("data-tooltip", isoDate, context);
        }

        output.AppendAttributeValue("class", "has-tooltip-arrow", context);

        foreach (var attribute in context.AllAttributes.Where(a => !OverwrittenAttributes.Contains(a.Name, StringComparer.OrdinalIgnoreCase)))
        {
            output.Attributes.SetAttribute(attribute.Name, attribute.Value);
        }

        output.Content.SetContent(displayDate);
    }
}

public enum DisplayDateFormat
{
    FullDate,
    FullDateTime,
    TimeOnly,
    ShortDate,
    ShortDateTime,
    Humanize,
    Custom
}
