using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Serious.AspNetCore.TagHelpers;

public static class TagHelperOutputExtensions
{
    public static void SetAttributeValueIfNotExist(this TagHelperOutput output, string name, string value, TagHelperContext context)
    {
        var existingAttribute = (context.AllAttributes[name]?.Value?.ToString() ?? output.Attributes[name]?.Value?.ToString());
        var newValue = string.IsNullOrEmpty(existingAttribute)
            ? value
            : existingAttribute;
        output.Attributes.SetAttribute(name, newValue);
    }

    public static void AppendAttributeValue(this TagHelperOutput output, string name, string value, TagHelperContext context)
    {
        var existingAttribute = (context.AllAttributes[name]?.Value?.ToString() ?? output.Attributes[name]?.Value?.ToString());
        var newAttributeValue = string.IsNullOrEmpty(existingAttribute)
            ? value
            : $"{value} {existingAttribute}";
        output.Attributes.SetAttribute(name, newAttributeValue);
    }
}
