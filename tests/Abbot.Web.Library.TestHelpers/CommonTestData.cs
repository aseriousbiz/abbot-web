using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;

namespace Abbot.Common.TestHelpers;

public static class NonExistent
{
    /// <summary>
    /// A Platform User ID that is not in use by the test data seeding.
    /// We check to confirm that there are no test users with this ID.
    /// </summary>
    public static readonly string PlatformUserId = "U999";

    /// <summary>
    /// A Platform (Team) ID that is not in use by the test data seeding.
    /// We check to confirm that there are no test organizations with this ID.
    /// </summary>
    public static readonly string PlatformId = "T999";

    /// <summary>
    /// A Platform Room ID that is not in use by the test data seeding.
    /// We check to confirm that there are no test organizations with this ID.
    /// </summary>
    public static readonly string PlatformRoomId = "C999";

    public static readonly Id<Member> MemberId = new(-99);
    public static readonly Id<Conversation> ConversationId = new(-99);
    public static readonly Id<ConversationLink> ConversationLinkId = new(-99);
    public static readonly Id<Playbook> PlaybookId = new(-99);
}

public class CommonTestData
{
    public const string DefaultOrganizationScopes = "app_mentions:read,channels:history,channels:join,channels:manage,channels:read,chat:write,chat:write.customize,commands,conversations.connect:write,conversations.connect:manage,files:read,files:write,groups:history,groups:read,groups:write,im:history,im:read,im:write,mpim:history,mpim:read,mpim:write,reactions:read,reactions:write,team:read,users.profile:read,users:read,users:read.email";

    // We have faith that TestEnvironmentWithData will Seed us.

    /// <summary>
    /// An <see cref="Organization"/> to serve as the "home" organization in tests.
    /// </summary>
    public Organization Organization { get; private set; } = null!;

    /// <summary>
    /// Gets the System Abbot member in the test organization.
    /// </summary>
    public Member SystemAbbot { get; private set; } = null!;

    /// <summary>
    /// Gets the not-System Abbot member in the test organization.
    /// </summary>
    public Member Abbot { get; private set; } = null!;

    /// <summary>
    /// A test <see cref="Member"/> of the <see cref="Organization"/>.
    /// </summary>
    public Member Member { get; private set; } = null!;

    /// <summary>
    /// The <see cref="User"/> associated with the test <see cref="Member"/>.
    /// </summary>
    public User User => Member.User;

    /// <summary>
    /// A separate <see cref="Organization"/> to act as a "foreign" organization in shared channels.
    /// </summary>
    public Organization ForeignOrganization { get; private set; } = null!;

    /// <summary>
    /// The Abbot Member of the <see cref="ForeignOrganization"/>.
    /// </summary>
    public Member ForeignAbbot { get; private set; } = null!;

    /// <summary>
    /// A test <see cref="Member"/> of the <see cref="ForeignOrganization"/>.
    /// </summary>
    public Member ForeignMember { get; private set; } = null!;

    /// <summary>
    /// A test guest <see cref="Member"/> of the <see cref="Organization"/>.
    /// </summary>
    public Member Guest { get; private set; } = null!;

    /// <summary>
    /// The <see cref="User"/> associated with the <see cref="Guest"/>.
    /// </summary>
    public User GuestUser => Guest.User;

    /// <summary>
    /// </summary>
    public User ForeignUser => ForeignMember.User;

    public async Task InitializeAsync(TestEnvironmentWithData env)
    {
        var roleSeeder = env.Activate<RoleSeeder>();
        await roleSeeder.SeedDataAsync();

        Organization = await env.Organizations.CreateOrganizationAsync(
            "Thome",
            PlanType.Unlimited,
            "Test Organization",
            "testorg.example.com",
            "testorg",
            "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_68.png");

        env.CreateSlackAuthorization("Aabbot", "Abbot App").Apply(Organization);

        Organization.UserSkillsEnabled = true;
        await env.Db.SaveChangesAsync();

        SystemAbbot = await env.Users.EnsureAbbotMemberAsync(Organization);
        Assert.Equal(1, SystemAbbot.UserId);

        await env.Users.EnsureAndUpdateMemberAsync(
            new UserEventPayload(
                Organization.PlatformBotUserId!,
                Organization.PlatformId,
                "Bot Real Name",
                Organization.BotName!,
                IsBot: true),
            Organization);

        Abbot = await env.Users.EnsureAbbotMemberAsync(Organization);
        Assert.NotSame(SystemAbbot, Abbot);

        Member = await env.Users.EnsureAndUpdateMemberAsync(
            new UserEventPayload("Uhome",
                Organization.PlatformId,
                "Test User One",
                "Test User 1",
                Email: "member@example.com",
                Avatar: "https://example.com/avatar_member.png",
                TimeZoneId: "America/Vancouver"),
            Organization);

        Guest = await env.Users.EnsureAndUpdateMemberAsync(
            new UserEventPayload("Uguest",
                Organization.PlatformId,
                "Guest User One",
                "Guest User 1",
                TimeZoneId: "America/Vancouver",
                Avatar: "https://example.com/avatar_guest.png",
                IsGuest: true),
            Organization);

        ForeignOrganization = await env.CreateOrganizationAsync(
            platformId: "Tforeign",
            plan: PlanType.None);

        // Not creating explicit user will fall back on member linked to System Abbot user
        ForeignAbbot = await env.Users.EnsureAbbotMemberAsync(ForeignOrganization);
        Assert.Equal(1, ForeignAbbot.UserId);

        ForeignMember = await env.Users.EnsureAndUpdateMemberAsync(
            new UserEventPayload("Uforeign",
                ForeignOrganization.PlatformId,
                "Foreign User One",
                "Foreign User 1",
                Avatar: "https://example.com/avatar_foreign.png"),
            ForeignOrganization);

        var messageFactory = env.Get<ITurnContextTranslator>();
        if (messageFactory is UnitTestTurnContextTranslator translator)
        {
            translator.SetOrganization(Organization);
        }

        // We guarantee that all properties of the base class are initialized by this point.
        await SeedAsync(env);

        Assert.True(
            await env.Db.Users.Where(o => o.PlatformUserId == NonExistent.PlatformUserId).CountAsync() == 0,
            "A seeder added an organization using the 'NonExistent.PlatformUserId' as a platform user id.");
        Assert.True(
            await env.Db.Organizations.Where(o => o.PlatformId == NonExistent.PlatformId).CountAsync() == 0,
            "A seeder added an organization using the 'NonExistent.PlatformId' as a team id.");
        Assert.True(
            await env.Db.Rooms.Where(o => o.PlatformRoomId == NonExistent.PlatformRoomId).CountAsync() == 0,
            "A seeder added an organization using the 'NonExistent.RoomId' as a room id.");
    }

    protected virtual Task SeedAsync(TestEnvironmentWithData env) => Task.CompletedTask;

    public void Deconstruct(out Organization organization, out User user)
    {
        organization = Organization;
        user = User;
    }

    public void Deconstruct(out Organization organization, out User user, out Member member)
    {
        organization = Organization;
        user = User;
        member = Member;
    }

    [return: NotNullIfNotNull(nameof(type))]
    public Organization? GetOrganization(TestOrganizationType? type) => type switch
    {
        null => null,
        TestOrganizationType.Home => Organization,
        TestOrganizationType.Foreign => ForeignOrganization,
        _ => throw new InvalidOperationException($"Unknown type {type}."),
    };

    [return: NotNullIfNotNull(nameof(actorType))]
    public Member? GetMember(TestMemberType? actorType) => actorType switch
    {
        null => null,
        TestMemberType.ForeignMember => ForeignMember,
        TestMemberType.HomeGuest => Guest,
        TestMemberType.HomeMember => Member,
        _ => throw new InvalidOperationException($"Unknown actor type {actorType}."),
    };
}

public enum TestOrganizationType
{
    Home,
    Foreign,
}

public enum TestMemberType
{
    HomeMember,
    HomeGuest,
    ForeignMember,
}
