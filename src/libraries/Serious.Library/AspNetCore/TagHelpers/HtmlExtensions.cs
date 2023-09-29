using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Serious.AspNetCore;

public static class HtmlExtensions
{
    /// <summary>
    /// Based on the built-in Html.GetEnumSelectList() method, but with the enum name as the rendered value instead
    /// of the integer value.
    /// </summary>
    /// <remarks>
    /// In a perfect world, I'd grab the selected value from the model data and use that to set the selected value.
    /// I don't have time to create the perfect world just yet - @haacked
    /// </remarks>
    /// <param name="htmlHelper">The <see cref="IHtmlHelper"/> to extend.</param>
    /// <param name="selectedValue">The currently selected value.</param>
    /// <param name="lowercaseValue">Whether or not to lowercase the value</param>
    /// <typeparam name="TEnum">The Enum type.</typeparam>
    /// <returns>A select list using the enum name as the value, rather than the int value.</returns>
    public static IEnumerable<SelectListItem> GetEnumValueSelectList<TEnum>(
        this IHtmlHelper htmlHelper,
        TEnum selectedValue = default,
        bool lowercaseValue = false)
        where TEnum : struct
    {
        (string?, bool) GetValueAndSelected(SelectListItem item)
        {
            var enumValue = Enum.Parse(typeof(TEnum), item.Value);
            var selected = selectedValue.Equals(enumValue);
            var value = lowercaseValue
                ? enumValue.ToString()?.ToLowerInvariant()
                : enumValue.ToString();

            return (value, selected);
        }

        return htmlHelper.GetEnumSelectList<TEnum>()
            .Select(item => (item.Text, GetValueAndSelected(item)))
            .Select(item => new SelectListItem(
                item.Item1,
                item.Item2.Item1,
                item.Item2.Item2));
    }
}
