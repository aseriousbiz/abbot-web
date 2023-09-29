using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;

namespace Serious;

/// <summary>
/// Extensions for <c>enum</c>s.
/// </summary>
public static class EnumExtensions
{
    public static string GetDisplayName(this Enum? item)
        => item.GetEnumAttributeValue<DisplayAttribute>(attr => attr.GetName());

    public static string GetEnumMemberValueName(this Enum? item)
        => item.GetEnumAttributeValue<EnumMemberAttribute>(attr => attr.Value);

    static string GetEnumAttributeValue<TAttribute>(this Enum? item, Func<TAttribute, string?> getAttributeValue)
        where TAttribute : Attribute
    {
        var field = item?.GetType().GetField(item.ToString());
        if (field is null)
            return string.Empty;

        var memberAttribute = field.GetCustomAttribute<TAttribute>(inherit: false);
        if (memberAttribute is not null)
        {
            return getAttributeValue(memberAttribute) ?? field.Name;
        }

        return field.Name;
    }
}
