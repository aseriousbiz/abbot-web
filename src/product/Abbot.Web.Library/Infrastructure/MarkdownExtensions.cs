using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Metadata;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Infrastructure;

public static class MarkdownExtensions
{
    public static string ToMarkdownList(this IEnumerable<INamedEntity> items)
    {
        return items.Select(item => $"`{item.Name}`").ToMarkdownList();
    }

    public static string ToDetailedMarkdownList(this IEnumerable<MemberFact> items)
    {
        return items.Select(item => $"`{item.Content}`\t{item.ToMetadataMarkdown()}").ToMarkdownList();
    }

    public static string ToMarkdownList<TSkill>(this IEnumerable<TSkill> items) where TSkill : ISkillDescriptor
    {
        return items.Select(item => item.ToMarkdown()).ToMarkdownList();
    }

    public static string ToMetadataMarkdown(this ITrackedEntity entity)
    {
        return $"(added by {entity.Creator.ToMarkdown()}{entity.Created.ToMarkdown()})";
    }

    internal static string ToMarkdown(this DateTime timestamp)
    {
        return $@" on `{timestamp:F}`";
    }

    static string ToMarkdown(this User user)
    {
        return $"<@{user.PlatformUserId}>";
    }

    static string ToMarkdown(this ISkillDescriptor skill)
    {
        return $"`{skill.Name}`"
            .AppendIfNotEmpty(
                skill.Description.EnsureEndsWithPunctuation(),
                " - ");
    }

    public static string ToDebugCommaSeparatedList(this IEnumerable<Member> members)
    {
        return string.Join(", ", members.Select(m => $"User Id: {m.UserId}, Name: {m.DisplayName}"));
    }

    public static string ToCommaSeparatedList(this IEnumerable<User> users)
    {
        return string.Join(", ", users.Select(u => $"`{u.FormatMention()}`"));
    }

    public static string ToCommaSeparatedList(this IEnumerable<string> values)
    {
        return string.Join(", ", values);
    }
}
