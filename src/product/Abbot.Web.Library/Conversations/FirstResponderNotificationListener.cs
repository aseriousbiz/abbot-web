using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Conversations;

/// <summary>
/// Listener responsible for sending any notifications triggered by new conversation messages to first responders.
/// </summary>
public class FirstResponderNotificationListener : IConversationListener
{
    internal const string SettingKey = $"{nameof(FirstResponderNotificationListener)}:Welcomed";
    static readonly ILogger<FirstResponderNotificationListener> Log =
        ApplicationLoggerFactory.CreateLogger<FirstResponderNotificationListener>();

    readonly ISettingsManager _settingsManager;
    readonly ISlackApiClient _slackApiClient;
    readonly IUrlGenerator _urlGenerator;
    readonly IUserRepository _userRepository;

    public FirstResponderNotificationListener(
        ISettingsManager settingsManager,
        ISlackApiClient slackApiClient,
        IUrlGenerator urlGenerator,
        IUserRepository userRepository)
    {
        _settingsManager = settingsManager;
        _slackApiClient = slackApiClient;
        _urlGenerator = urlGenerator;
        _userRepository = userRepository;
    }

    public Task OnNewConversationAsync(Conversation conversation, ConversationMessage message) =>
        WelcomeNewFirstResponderAsync(conversation, message);

    async Task WelcomeNewFirstResponderAsync(Conversation conversation, ConversationMessage message)
    {
        // If the message isn't live, we don't care.
        // Technically we could notify FRs about imported conversations, but it could create noise.
        // By skipping an imported conversation, we'll still notify an FR of the first live conversation they're responsible for.
        if (!message.IsLive || conversation.State is ConversationState.Hidden)
        {
            return;
        }

        if (!conversation.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            // Shouldn't be able to create a conversation without an API token.
            // But I don't think this is "Unreachable" since there can be a race between when a conversation is created
            // and an organization uninstalling the bot.
            // So we'll just log this.
            Log.OrganizationHasNoSlackApiToken();
            return;
        }

        var abbot = await _userRepository.EnsureAbbotMemberAsync(conversation.Organization);

        // Get FRs for this room
        var frs = message.Room.Assignments
            .Where(a => a.Role == RoomRole.FirstResponder)
            .ToList();

        foreach (var fr in frs)
        {
            var scope = SettingsScope.Member(fr.Member);
            // Has this FR been welcomed to the Joy and Wonder™️ of being an FR?
            var welcomed =
                await _settingsManager.GetAsync(scope, SettingKey);
            if (welcomed is { Value: "true" })
            {
                // Yep, move on to the next user.
                continue;
            }

            // Nope, so let's welcome them.
            await WelcomeFirstResponderAsync(apiToken, fr.Member, conversation);

            // And remember for later.
            await _settingsManager.SetAsync(scope, SettingKey, "true", abbot.User);
        }
    }

    async Task WelcomeFirstResponderAsync(string apiToken, Member member, Conversation conversation)
    {
        var messageText =
            $"I’m tracking conversations in {conversation.Room.ToMention()}!\n" +
            "I’ll ping you as any tracked conversation approaches the response time target and deadline.\n" +
            $"You can jump right into <{conversation.GetFirstMessageUrl()}|this conversation> or <{_urlGenerator.HomePage()}|view all conversations at ab.bot>.";

        var messageRequest = new MessageRequest(member.User.PlatformUserId, messageText);
        var resp = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);
        if (resp.Ok)
        {
            Log.WelcomedFirstResponder(member.User.PlatformUserId);
        }
        else
        {
            // We won't throw an exception here, which means we'll continue to mark the user as welcomed.
            // We really don't want to get in a loop trying to welcome a user, and missing out on the odd welcome message isn't the end of the world.
            // We do log though, so we can track failures.
            Log.ErrorWelcomingUser(member.User.PlatformUserId, resp.ToString());
        }
    }
}

static partial class FirstResponderNotificationListenerLoggingExtensions
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Sent FR welcome to {WelcomedPlatformUserId}")]
    public static partial void WelcomedFirstResponder(this ILogger<FirstResponderNotificationListener> logger, string welcomedPlatformUserId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Error welcoming {WelcomedPlatformUserId}: {SlackError}")]
    public static partial void ErrorWelcomingUser(this ILogger<FirstResponderNotificationListener> logger, string welcomedPlatformUserId, string slackError);
}
