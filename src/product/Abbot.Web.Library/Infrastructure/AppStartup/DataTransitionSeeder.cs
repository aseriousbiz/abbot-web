using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class FixSlackbots : IRunOnceDataSeeder
{
    readonly AbbotContext _db;

    public FixSlackbots(AbbotContext db)
    {
        _db = db;
    }

    public async Task SeedDataAsync()
    {
        // Fetch all the slack bots
        // Raw SQL is super easy here and avoids materializing all the users
        await _db.Database.ExecuteSqlRawAsync("""
            UPDATE "Users"
            SET "IsBot" = true
            WHERE "IsBot" = false AND "PlatformUserId" = 'USLACKBOT'
        """);
    }

    public bool Enabled => true;

    public int Version => 1;
}

public class SeedLastMessageActivityDate : IRunOnceDataSeeder
{
    static readonly ILogger<SeedLastMessageActivityDate> Log =
        ApplicationLoggerFactory.CreateLogger<SeedLastMessageActivityDate>();

    readonly AbbotContext _db;
    readonly IConversationsApiClient _conversationsApiClient;

    public SeedLastMessageActivityDate(AbbotContext db, IConversationsApiClient conversationsApiClient)
    {
        _db = db;
        _conversationsApiClient = conversationsApiClient;
    }

    public async Task SeedDataAsync()
    {
        var rooms = await _db.Rooms
            .Include(r => r.Organization)
            .Where(e => e.ManagedConversationsEnabled == true)
            .Where(e => e.LastMessageActivityUtc == null)
            .ToListAsync();

        var groups = rooms.GroupBy(r => r.OrganizationId);

        foreach (var group in groups)
        {
            foreach (var room in group)
            {
                using var orgScope = Log.BeginOrganizationScope(room.Organization);
                using var roomScope = Log.BeginRoomScope(room);

                if (!room.Organization.TryGetUnprotectedApiToken(out var apiToken))
                {
                    Log.OrganizationHasNoSlackApiToken();
                    return;
                }

                try
                {
                    var response = await _conversationsApiClient.GetConversationHistoryAsync(
                        apiToken,
                        limit: 1,
                        channel: room.PlatformRoomId);

                    if (!response.Ok)
                    {
                        Log.ApiError(room.Organization.Name, room.Organization.Id, response.Error);

                        if (response.Error is "channel_not_found" or "not_in_channel" or "no_permission")
                        {
                            // Issue is probably specific to this channel. Go to next channel.
                            continue;
                        }

                        // Issue is probably specific to the org. Go to next org.
                        break;
                    }

                    if (response is { Body: [{ Timestamp: { Length: > 0 } ts }] }
                        && SlackTimestamp.TryParse(ts, out var timestamp))
                    {
                        room.LastMessageActivityUtc = timestamp.UtcDateTime;
                        await _db.SaveChangesAsync();
                    }
                }
                catch (Exception e)
                {
                    // Log it and go to next org.
                    Log.ApiException(e, room.Organization.Name, room.OrganizationId);
                    break;
                }

                await Task.Delay(
                    1500); // Wait 1.5 seconds to avoid rate limits (50+ per minute). This will take a while. But we're patient.
            }
        }
    }

    public bool Enabled => true;

    public int Version => 1;
}

public static partial class SeedLastMessageActivityDateLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Api Error {OrganizationName} {OrganizationId} {Error}…")]
    public static partial void ApiError(this ILogger<SeedLastMessageActivityDate> logger, string? organizationName,
        int organizationId, string error);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Api Error {OrganizationName} {OrganizationId}…")]
    public static partial void ApiException(this ILogger<SeedLastMessageActivityDate> logger, Exception e,
        string? organizationName, int organizationId);
}

public class SeedConversationThreadIdsSeeder : IRunOnceDataSeeder
{
    readonly AbbotContext _db;

    public SeedConversationThreadIdsSeeder(AbbotContext db)
    {
        _db = db;
    }

    public async Task SeedDataAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(
            """
UPDATE "Conversations" SET "ThreadIds"[0] = "FirstMessageId";
""");
    }

    public bool Enabled => true;

    public int Version => 1;

    public bool BlockServerStartup => true;
}

public class UpdateConversationEventThreadIdsSeeder : IRunOnceDataSeeder
{
    readonly AbbotContext _db;

    public UpdateConversationEventThreadIdsSeeder(AbbotContext db)
    {
        _db = db;
    }

    public async Task SeedDataAsync()
    {
        var conversationEvents = await _db.ConversationEvents
            .Include(ce => ce.Conversation)
            .Where(e => e.ThreadId == null)
            .ToListAsync();

        foreach (var conversationEvent in conversationEvents)
        {
            conversationEvent.ThreadId = conversationEvent.Conversation.FirstMessageId;
        }

        await _db.SaveChangesAsync();
    }

    public bool Enabled => true;

    public int Version => 1;

    public bool BlockServerStartup => true;
}

public class RunForeignUserCleanupSeeder : IRunOnceDataSeeder
{
    readonly AbbotContext _db;

    public RunForeignUserCleanupSeeder(AbbotContext db)
    {
        _db = db;
    }

    public async Task SeedDataAsync()
    {
        var usersInNeedOfMigration = await _db.Users
            .Where(u => u.Members.All(m =>
                m.Organization.PlanType == PlanType.None && m.Organization.PlatformType == PlatformType.Slack))
            .ToListAsync();

        foreach (var user in usersInNeedOfMigration)
        {
            user.Email = null;
        }

        await _db.SaveChangesAsync();
    }

    public bool Enabled => true;

    public int Version => 1;
}
