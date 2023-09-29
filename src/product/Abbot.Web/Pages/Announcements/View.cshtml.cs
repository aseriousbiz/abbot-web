using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;
using Serious.Tasks;

namespace Serious.Abbot.Pages.Announcements;

public class ViewPage : UserPage
{
    readonly IAnnouncementsRepository _repository;
    readonly IAnnouncementScheduler _announcementScheduler;
    readonly IAnnouncementCache _announcementCache;
    readonly IConversationsApiClient _conversationsApiClient;
    readonly IReactionsApiClient _reactionsApiClient;
    readonly IEmojiLookup _emojiLookup;
    readonly ISlackResolver _resolver;
    readonly IMessageRenderer _messageRenderer;

    public Announcement Announcement { get; private set; } = null!;
    public Member? Author { get; private set; }

    public RenderedMessage? RenderedAnnouncement { get; set; }

    public IEnumerable<AnnouncementMessageModel> AnnouncementMessages { get; private set; } = null!;

    public Uri? MessageUrl { get; private set; }

    public ViewPage(
        IAnnouncementsRepository repository,
        IAnnouncementScheduler announcementScheduler,
        IAnnouncementCache announcementCache,
        IConversationsApiClient conversationsApiClient,
        IReactionsApiClient reactionsApiClient,
        IEmojiLookup emojiLookup,
        ISlackResolver resolver,
        IMessageRenderer messageRenderer)
    {
        _repository = repository;
        _announcementScheduler = announcementScheduler;
        _announcementCache = announcementCache;
        _conversationsApiClient = conversationsApiClient;
        _reactionsApiClient = reactionsApiClient;
        _emojiLookup = emojiLookup;
        _resolver = resolver;
        _messageRenderer = messageRenderer;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var announcement = await _repository.GetByIdAsync(id, Organization);
        if (announcement is null)
        {
            return NotFound();
        }

        AnnouncementMessages = await announcement
            .Messages
            .SelectFunc(GetAnnouncementMessageModel)
            .WhenAllOneAtATimeAsync();

        MessageUrl = announcement.GetMessageUrl();
        Announcement = announcement;
        Author = announcement.Creator.Members.SingleOrDefault(m => m.OrganizationId == announcement.OrganizationId);

        var announcementText = await GetTextAsync(announcement);
        RenderedAnnouncement = await _messageRenderer.RenderMessageAsync(announcementText, announcement.Organization);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var announcement = await _repository.GetByIdAsync(id, Organization);
        if (announcement is null)
        {
            return NotFound();
        }

        (StatusMessage, var page) = await _announcementScheduler.UnscheduleAnnouncementBroadcastAsync(announcement, Viewer.User)
            ? ("Announcement unscheduled successfully.", "Index")
            : ("Error: Sorry, it’s too late to unschedule this announcement.", null); // Displays with danger style.

        return RedirectToPage(page);
    }

    async Task<AnnouncementMessageModel?> GetAnnouncementMessageModel(AnnouncementMessage announcementMessage)
    {
        var replies = await GetRepliesAsync(announcementMessage);
        var reactions = await GetReactionsAsync(announcementMessage);
        return new AnnouncementMessageModel(
            announcementMessage,
            replies,
            reactions);
    }

    async Task<IReadOnlyList<Reaction>> GetReactionsAsync(AnnouncementMessage announcementMessage)
    {
        var apiToken = Organization.RequireAndRevealApiToken();

        if (announcementMessage.MessageId is not { } messageId)
        {
            // We haven't posted this one yet.
            return Array.Empty<Reaction>();
        }

        var response = await _reactionsApiClient.GetMessageReactionsAsync(
            apiToken,
            announcementMessage.Room.PlatformRoomId,
            messageId);
        // TODO: We may want to show an error if we have trouble retrieving replies.
        if (!response.Ok || response.Body.Reactions is not { Count: > 0 })
        {
            return Array.Empty<Reaction>();
        }

        async Task<Reaction?> GetReactionAsync(ReactionSummary reaction)
        {
            // TODO: Ideally we add an overload to ISlackResolver that takes in a bunch of user ids.
            var members = await reaction
                .Users
                .SelectFunc(userId => _resolver.ResolveMemberAsync(userId, Organization))
                .WhenAllOneAtATimeAsync();
            var users = members.Select(m => m.User);

            // At this point, we ignore custom emojis from other organizations.
            var emoji = await _emojiLookup.GetEmojiAsync(reaction.Name, apiToken);
            return new Reaction(emoji, reaction.Count, users);
        }

        var reactions = await response.Body.Reactions
            .SelectFunc(GetReactionAsync)
            .WhenAllOneAtATimeAsync();

        return reactions.ToList();
    }

    async Task<IReadOnlyList<Reply>> GetRepliesAsync(AnnouncementMessage announcementMessage)
    {
        var apiToken = Organization.RequireAndRevealApiToken();

        if (announcementMessage.MessageId is not { } messageId)
        {
            // We haven't posted this one yet.
            return Array.Empty<Reply>();
        }

        var response = await _conversationsApiClient.GetConversationRepliesAsync(
            apiToken,
            announcementMessage.Room.PlatformRoomId,
            messageId);

        // TODO: We may want to show an error if we have trouble retrieving replies.
        if (!response.Ok || response.Body is { Count: 0 })
        {
            return Array.Empty<Reply>();
        }

        async Task<Reply?> GetReplyAsync(SlackMessage reply)
        {
            if (reply.User is not null && reply.Text is not null)
            {
                var member = await _resolver.ResolveMemberAsync(reply.User, Organization);
                if (member is not null)
                {
                    var postedAt = SlackFormatter.GetDateFromSlackTimestamp(reply.Timestamp.Require());
                    return new Reply(reply.Text, member.User, postedAt);
                }
            }

            return null;
        }

        var replies = await response
            .Body
            .Skip(1)  // The first message is the always the root, so we skip it.
            .SelectFunc(GetReplyAsync)
            .WhenAllOneAtATimeAsync();

        return replies.WhereNotNull().ToList();
    }

    public string TranslateSlackError(string error)
    {
        return error switch
        {
            "not_in_channel" => $"{Organization.BotName} is not a member of this room. Please add {Organization.BotName} to this channel and try again.",
            "channel_not_found" => "This room was not found. It may have been deleted after you created the announcement.",
            "is_archived" => "This room is archived.",
            "msg_too_long" => "The message is too long.",
            "no_text" => "The message is empty.",
            "restricted_action" or "missing_scope" => $"{Organization.BotName} does not have permission to post to this room.",
            "restricted_action_readonly_channel" => "This room is read-only.",
            "restricted_action_thread_channel_user" => "This room does not allow top-level messages. It is thread-only.",
            "ratelimited" or "rate_limited" => $"Slack rate-limited {Organization.BotName}.",
            "ekm_access_denied" => "Your Slack Administrators have suspended the ability to post messages.",
            "account_inactive" => $"The {Organization.BotName}  account is inactive.",
            "request_timeout" => "The attempt to post to this room timed out.",
            "service_unavailable" => "The Slack service was unavailable when trying to post to this room.",
            "fatal_error" => "The Slack service encountered a fatal error when trying to post to this room.",
            "internal_error" => "The Slack service encountered an internal error when trying to post to this room.",
            _ => error,
        };
    }

    async Task<string?> GetTextAsync(Announcement announcement)
    {
        return await _announcementCache.GetAndCacheAnnouncementTextAsync(announcement);
    }

    public async Task<RenderedMessage?> RenderReplyAsync(string text)
    {
        return await _messageRenderer.RenderMessageAsync(text, Organization);
    }
}

public record AnnouncementMessageModel(
    AnnouncementMessage AnnouncementMessage,
    IReadOnlyList<Reply> Replies,
    IReadOnlyList<Reaction> Reactions)
{
    public Room Room => AnnouncementMessage.Room;

    public string? MessageId => AnnouncementMessage.MessageId;

    public string? ErrorMessage => AnnouncementMessage.ErrorMessage;
}

public record Reply(string Text, User From, DateTime PostedAt);

public record Reaction(
    Emoji Emoji,
    int Count,
    IEnumerable<User> Users);
