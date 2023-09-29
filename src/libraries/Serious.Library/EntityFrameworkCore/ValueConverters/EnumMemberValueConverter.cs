using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Serious.EntityFrameworkCore.ValueConverters;

/// <summary>
/// A value converter for enums that use the <see cref="EnumMemberAttribute"/> value as the stored value.
/// </summary>
/// <remarks>
/// It turns out that EF Core doesn't respect the EnumMemberAttribute yet.
/// </remarks>
/// <typeparam name="TEnum">The enum type.</typeparam>
public class EnumMemberValueConverter<TEnum> : ValueConverter<TEnum, string> where TEnum : struct, Enum
{
    // Yeah, these dictionaries are never collected, but they're going to generally be small and
    // there's not going to be too many of them.
    static readonly Dictionary<string, TEnum> StringToEnumLookup = CreateStringToEnumLookup();
    static readonly Dictionary<TEnum, string> EnumToStringLookup = CreateEnumToStringLookup(StringToEnumLookup);

    public EnumMemberValueConverter()
        : base(
            value => GetStringFromEnum(value),
            storedValue => GetEnumFromString(storedValue))
    {
    }

    static TEnum GetEnumFromString(string value) => StringToEnumLookup.TryGetValue(value, out var enumValue)
        ? enumValue
        : default;

    static string GetStringFromEnum(TEnum value) => EnumToStringLookup.TryGetValue(value, out var enumValue)
        ? enumValue
        : string.Empty;


    static Dictionary<string, TEnum> CreateStringToEnumLookup() =>
        Enum.GetValues<TEnum>().ToDictionary(value => value.GetEnumMemberValueName(), value => value);
    static Dictionary<TEnum, string> CreateEnumToStringLookup(Dictionary<string, TEnum> stringToEnumLookup) =>
        stringToEnumLookup.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
}
