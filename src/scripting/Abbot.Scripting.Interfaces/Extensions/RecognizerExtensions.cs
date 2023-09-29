using System;
using System.Collections.Generic;
using System.Dynamic;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Microsoft.Recognizers.Text.DateTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Useful extensions for Microsoft.Recognizers
/// </summary>
public static class RecognizerExtensions
{
#pragma warning disable CS3001 // Argument type is not CLS-compliant
    /// <summary>
    /// Returns the values property of a <see cref="Microsoft.Recognizers.Text.ModelResult"/> result, cast to the appropriate type.
    /// </summary>
    public static IEnumerable<dynamic> GetValues(this Microsoft.Recognizers.Text.ModelResult result)
    {
        var ret = (IList<Dictionary<string, string>>)result.Resolution["values"];
        foreach (var entry in ret)
        {
            var eo = new ExpandoObject();
            foreach ((string? key, string? value) in entry)
            {
                switch (key)
                {
                    case DateTimeResolutionKey.Timex:
                        if (entry.TryGetValue("type", out var type))
                        {
                            switch (type)
                            {
                                case TimexConstants.Set:
                                    eo.TryAdd(key.Capitalize(), new TimexSet(value));
                                    break;
                                case TimexConstants.Date:
                                case TimexConstants.DateTime:
                                    if (entry.TryGetValue("value", out var raw) && DateTime.TryParse(raw, out var dt))
                                    {
                                        eo.TryAdd("DateTime", dt);
                                        eo.TryAdd("AbsoluteTimex", TimexProperty.FromDateTime(dt));
                                    }
                                    break;
                            }
                        }
                        eo.TryAdd(key.Capitalize(), new TimexProperty(value));
                        break;
                    default:
                        eo.TryAdd(key.Capitalize(), value);
                        break;
                }
            }
            yield return eo;
        }
    }

    static string Capitalize(this string text)
    {
        return text.Length switch
        {
            0 => text,
            1 => text.ToUpperInvariant(),
            _ => (text[0].ToString()).ToUpperInvariant() + text[1..]
        };
    }
}
