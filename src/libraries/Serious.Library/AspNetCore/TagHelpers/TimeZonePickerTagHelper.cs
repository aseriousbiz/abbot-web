using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using TimeZoneNames;

namespace Serious.AspNetCore.TagHelpers;

[HtmlTargetElement("timezonepicker")]
public class TimeZonePickerTagHelper : SelectTagHelper
{
    public TimeZonePickerTagHelper(IHtmlGenerator generator) : base(generator)
    {
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "select";
        Items = GetItems();
        output.TagMode = TagMode.StartTagAndEndTag;
        base.Process(context, output);
    }

    /// <summary>
    /// The current selected value.
    /// </summary>
    public string? Value { get; set; }

    IEnumerable<SelectListItem> GetItems()
    {
        var languageCode = CultureInfo.CurrentUICulture.Name;
        var timezones = TZNames.GetDisplayNames(languageCode, useIanaZoneIds: true);

        var value = Value ?? "Etc/UTC";
        return timezones
            .Select(kvp => new SelectListItem(
                kvp.Value,
                kvp.Key,
                kvp.Key.Equals(value, StringComparison.Ordinal)));
    }
}
