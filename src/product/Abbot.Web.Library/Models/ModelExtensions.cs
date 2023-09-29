using System;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public static class ModelExtensions
{
    public static string FormatMention(this Member member)
    {
        return member.User.FormatMention();
    }

    /// <summary>
    /// Returns a <see cref="Uri"/> containing a URL to the member's profile on the chat platform.
    /// </summary>
    public static Uri FormatPlatformUrl(this Member member)
    {
        return SlackFormatter.UserUrl(member.Organization.Domain,
            member.User.PlatformUserId);
    }

    public static string FormatMention(this User user)
    {
        return SlackFormatter.UserMentionSyntax(user.PlatformUserId);
    }
}
