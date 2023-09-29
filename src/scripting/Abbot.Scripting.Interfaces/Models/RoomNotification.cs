using System.Collections.Generic;
using System.Linq;

namespace Serious.Abbot.Scripting;

/// <summary>
/// A notification to send to a room's responders.
/// </summary>
/// <param name="Icon">
/// Gets or sets the icon for this notification.
/// This should be a Unicode Emoji, but if absolutely necessary it can be a Slack emoji reference (":smile:")
/// </param>
/// <param name="Headline">
/// Gets or sets the headline for this notification.
/// </param>
/// <param name="Message">
/// Gets or sets the message for this notification.
/// </param>
/// <param name="Escalation">
/// If <c>true</c>, then escalation responders will be notified, instead of first responders.
/// </param>
public record RoomNotification(string Icon, string Headline, string Message, bool Escalation = false);

/// <summary>
/// A set of responders.
/// </summary>
/// <param name="RoomRole">The role of the responders</param>
/// <param name="Members">The responders.</param>
public record ResponderGroup(RoomRole RoomRole, IReadOnlyList<IChatUser> Members)
{
    /// <summary>
    /// The set of mention IDs for the responders.
    /// </summary>
    public IReadOnlyList<string> MentionIds => Members.Select(m => m.Id).ToList();
}
