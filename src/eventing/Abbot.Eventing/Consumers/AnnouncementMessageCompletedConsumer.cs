using MassTransit;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Infrastructure;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Eventing;

/// <summary>
/// Listens for <see cref="AnnouncementMessageCompleted"/> messages and sends a DM to the announcement creator
/// when the message is sent.
/// </summary>
public class AnnouncementMessageCompletedConsumer : IConsumer<AnnouncementMessageCompleted>
{
    readonly IAnnouncementsRepository _announcementsRepository;
    readonly ISlackApiClient _slackApiClient;
    readonly IUrlGenerator _urlGenerator;

    public AnnouncementMessageCompletedConsumer(
        IAnnouncementsRepository announcementsRepository,
        ISlackApiClient slackApiClient,
        IUrlGenerator urlGenerator)
    {
        _announcementsRepository = announcementsRepository;
        _slackApiClient = slackApiClient;
        _urlGenerator = urlGenerator;
    }

    // This ensures that `AnnouncementMessageCompleted` messages are processed serially per announcement id.
    // This means the consumer will only be processing a single message per announcement at a time.
    public class Definition : AbbotConsumerDefinition<AnnouncementMessageCompletedConsumer>
    {
        public Definition()
        {
            RequireSession("announcement-message-completed-v1");
        }
    }

    public async Task Consume(ConsumeContext<AnnouncementMessageCompleted> context)
    {
        var announcementId = context.Message.Id;

        // This only returns true if this call was the call to set it completed to avoid double sending.
        var completed = await _announcementsRepository.SetAnnouncementCompletedAsync(announcementId);

        if (!completed)
        {
            return;
        }

        var announcement = await _announcementsRepository.RequireForSchedulingAsync(announcementId);

#if DEBUG
        Expect.True(announcement.Messages.Count > 0);
#endif

        var successes = announcement.Messages.Where(m => m.ErrorMessage is null).ToList();
        var successfulChannelsList = successes
            .OrderBy(m => m.Id)
            .ToRoomMentionList(useLinkForPrivateRoom: true);
        var errorChannelList = announcement
            .Messages
            .Where(m => m.ErrorMessage is not null)
            .OrderBy(m => m.Id)
            .ToRoomMentionList(useLinkForPrivateRoom: true);

        var fallbackText = successes is { Count: 0 }
            ? $":warning: I’ve failed to post your announcement in any of the channels you specified: {errorChannelList}."
            : $":mega: :sparkles: I’ve posted your announcement in the following channels: {successfulChannelsList}.";

        var messageText = fallbackText;
        if (errorChannelList.Any() && successes.Any())
        {
            messageText += $"\n\n:warning: I’ve also failed to post in the following channels: {errorChannelList}.";
        }

        var announcementUrl = _urlGenerator.AnnouncementPage(announcement.Id);

        await _slackApiClient.SendDirectMessageAsync(
            announcement.Organization,
            announcement.Creator,
            fallbackText,
            new Section(new MrkdwnText(messageText)),
            new Actions(new ButtonElement("Track your announcement here")
            {
                Url = announcementUrl
            }));
    }
}
