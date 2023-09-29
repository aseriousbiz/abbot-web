using System.Collections.Generic;
using System.Linq;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Set of extension methods to make replying easier.
/// </summary>
public static class ReplyExtensions
{
    /// <summary>
    /// Converts the list of items into a bulleted markdown list.
    /// </summary>
    /// <param name="items">The collection of strings to turn into a markdown list.</param>
    /// <returns>A string formatted as a markdown list.</returns>
    public static string ToMarkdownList(this IEnumerable<string> items)
    {
        return string.Join("\n", items.Select(item => $"• {item}"));
    }

    /// <summary>
    /// Converts the list of items into an ordered markdown list.
    /// </summary>
    /// <param name="items">The collection of strings to turn into a markdown list.</param>
    /// <returns>A string formatted as a markdown list.</returns>
    public static string ToOrderedList(this IEnumerable<string> items)
    {
        return string.Join("\n", items.Select((item, order) => $"{order + 1}. {item}"));
    }

    /// <summary>
    /// <para>
    /// DEPRECATED: Just call ToString.</para>
    /// <para>
    /// Formats the user for the target chat platform. This ensures platforms like
    /// Slack render the user in the proper format.
    /// </para>
    /// </summary>
    /// <param name="user">The user to format.</param>
    public static string? Format(this IChatUser user)
    {
        return user.ToString();
    }
}
