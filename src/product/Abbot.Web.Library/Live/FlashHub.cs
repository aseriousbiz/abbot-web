using Serious.Abbot.Entities;
using Hub = Microsoft.AspNetCore.SignalR.Hub;

namespace Serious.Abbot.Live;

public record struct FlashGroup(string Name)
{
    public static FlashGroup Organization(Id<Organization> organizationId) => new($"organization:{organizationId}");
    public static FlashGroup PlaybookRun(PlaybookRun run) => new($"playbook-run:{run.CorrelationId}");

    public override string ToString() => Name;
}

public record struct FlashName(string Name)
{
    public static readonly FlashName ConversationListUpdated = new("conversation-list-updated");
    public static readonly FlashName PlaybookRunUpdated = new("playbook-run-updated");

    public override string ToString() => Name;
}

/// <summary>
/// A SignalR Hub for notifying clients of changes to the conversations list.
/// DO NOT send actual data through this Hub, only send signals to trigger UI refresh.
/// We DO NOT secure our SignalR connections, so all users attached are anonymous.
/// </summary>
public class FlashHub : Hub
{
    public async Task<bool> JoinGroup(string group)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        return true;
    }

    public async Task<bool> LeaveGroup(string group)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        return true;
    }
}
