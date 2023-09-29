using NodaTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Useful extensions to the <see cref="IChatUser"/>.
/// </summary>
public static class ChatUserExtensions
{
    /// <summary>
    /// Retrieve the current local time for the chat user.
    /// </summary>
    /// <param name="chatUser">The user.</param>
    /// <returns>The local time if the timezone is known, otherwise returns null.</returns>
    public static LocalTime? GetLocalTime(this IChatUser chatUser)
    {
        return chatUser.TimeZone is null
            ? null
            : GetCurrentInstant().InZone(chatUser.TimeZone).TimeOfDay;
    }

    /// <summary>
    /// Retrieve the current local date and time for the chat user.
    /// </summary>
    /// <param name="chatUser">The user.</param>
    /// <returns>The local time if the timezone is known, otherwise returns null.</returns>
    public static ZonedDateTime? GetLocalDateTime(this IChatUser chatUser)
    {
        return chatUser.TimeZone is null
            ? null
            : GetCurrentInstant().InZone(chatUser.TimeZone);
    }

    static Instant GetCurrentInstant()
    {
        return SystemClock.Instance.GetCurrentInstant();
    }
}
