namespace Abbot.Common.TestHelpers;

[Flags]
public enum RoomFlags
{
    /// <summary>
    /// Persistent room with bot as member.
    /// </summary>
    Default = 0,

    NotPersistent = 0x01,
    BotIsNotMember = 0x02,
    ManagedConversationsEnabled = 0x04,

    Shared = 0x10,
    IsCommunity = 0x20,

    Archived = 0x100,
    Deleted = 0x200,
}
