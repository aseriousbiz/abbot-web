namespace Serious.Abbot.Entities;

/// <summary>
/// Represents all the relevant settings for a room.
/// This value can be set at various scope levels and values are merged together.
/// </summary>
/// <remarks>
/// There are three "layers" of room settings.
/// Settings in a higher layer override settings in a lower layer.
/// Any settings in a layer that are <c>null</c> inherit the value from the lower layer.
/// The layers are, from highest (and thus most specific) to lowest (and thus most general):
/// 1. Room-specific settings.
/// 2. Organization defaults.
/// 3. Application-global defaults.
/// </remarks>
public record RoomSettings
{
    public const string DefaultUserWelcomeMessage =
        ":wave: Hey, I’m Abbot. I’ll be monitoring this channel to make sure someone responds to you promptly. Don’t worry, I won’t remember anything except your greatest fears.";
    public const string DefaultConversationWelcomeMessage =
        ":wave: Hey, I’m Abbot. I’ll be monitoring this conversation to make sure someone responds to you promptly. Please use this thread to reply in this conversation so that I see what’s going on.";

    /// <summary>
    /// The global application defaults used as the "final" fallback for any settings not set at the room or organization level.
    /// </summary>
    public static readonly RoomSettings Default = new()
    {
        WelcomeNewUsers = false,
        WelcomeNewConversations = false,

        // In practice, these messages won't be used.
        // If a user enables welcome messages, submitting the form will take this value and copy it to the organization/room-level settings.
        // But having them set here makes it easy to fill in the text box when we're rendering the settings page.
        UserWelcomeMessage = DefaultUserWelcomeMessage,
        ConversationWelcomeMessage = DefaultConversationWelcomeMessage,
    };

    /// <summary>
    /// A room settings instance with all settings set to <c>null</c>.
    /// </summary>
    public static readonly RoomSettings Empty = new();

    /// <summary>
    /// A boolean indicating if new users should be welcomed to the room.
    /// </summary>
    public bool? WelcomeNewUsers { get; init; }

    /// <summary>
    /// The content of the message that will be sent to users if <see cref="WelcomeNewUsers"/> is <c>true</c>.
    /// </summary>
    public string? UserWelcomeMessage { get; init; }

    /// <summary>
    /// A boolean indicating if Abbot should send a reply to new conversations.
    /// </summary>
    public bool? WelcomeNewConversations { get; init; }

    /// <summary>
    /// The content of the message that will be posted as a reply if <see cref="WelcomeNewConversations"/> is <c>true</c>.
    /// </summary>
    public string? ConversationWelcomeMessage { get; init; }

    /// <summary>
    /// If <c>true</c>, then the room treats all non-agents as supported people (aka people who can create conversations).
    /// </summary>
    public bool? IsCommunityRoom { get; init; }

    /// <summary>
    /// Merges two <see cref="RoomSettings"/> objects, taking all the non-<c>null</c> values from the child object
    /// and using the values from the parent object for any <c>null</c> values.
    /// </summary>
    /// <param name="parent">The "parent" object that should be used as the base that can be overridden.</param>
    /// <param name="child">The "child" object that has the most specific settings.</param>
    /// <returns>A merged <see cref="RoomSettings"/> object.</returns>
    public static RoomSettings Merge(RoomSettings? parent, RoomSettings? child)
    {
        return new()
        {
            WelcomeNewUsers = child?.WelcomeNewUsers ?? parent?.WelcomeNewUsers,
            UserWelcomeMessage = child?.UserWelcomeMessage ?? parent?.UserWelcomeMessage,
            WelcomeNewConversations = child?.WelcomeNewConversations ?? parent?.WelcomeNewConversations,
            ConversationWelcomeMessage = child?.ConversationWelcomeMessage ?? parent?.ConversationWelcomeMessage,
            IsCommunityRoom = child?.IsCommunityRoom ?? parent?.IsCommunityRoom,
        };
    }
}
