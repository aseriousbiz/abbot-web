using System.Collections.Concurrent;
using MassTransit;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Infrastructure;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Repositories;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Eventing;

public class SystemNotificationsConsumer : IConsumer<PublishSystemNotification>
{
    readonly ISlackApiClient _slackApiClient;
    readonly IOrganizationRepository _organizationRepository;
    readonly AbbotOptions _abbotOptions;

    static readonly ConcurrentDictionary<string, ConcurrencyTracker> ConcurrencyTrackers = new();

    public class Definition : AbbotConsumerDefinition<SystemNotificationsConsumer>
    {
        public Definition()
        {
            RequireSession("system-notifications");
        }
    }

    public SystemNotificationsConsumer(
        ISlackApiClient slackApiClient,
        IOrganizationRepository organizationRepository,
        IOptions<AbbotOptions> abbotOptions)
    {
        _slackApiClient = slackApiClient;
        _organizationRepository = organizationRepository;
        _abbotOptions = abbotOptions.Value;
    }


    public async Task Consume(ConsumeContext<PublishSystemNotification> context)
    {
        if (context.Message.DeduplicationKey is { Length: > 0 } key)
        {
            var tracker = ConcurrencyTrackers.GetOrAdd(key, _ => new ConcurrencyTracker());
            var sendCount = tracker.Increment();
            if (sendCount > 1)
            {
                return;
            }
        }

        if (_abbotOptions.StaffOrganizationId is not { Length: > 0 }
            || _abbotOptions.NotificationChannelId is not { Length: > 0 })
        {
            return;
        }

        var organization = await _organizationRepository.GetAsync(_abbotOptions.StaffOrganizationId);
        if (organization is null || !organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            return;
        }

        var request = new MessageRequest(_abbotOptions.NotificationChannelId, context.Message.Content.Text)
        {
            Blocks = context.Message.Content.Blocks ?? new List<ILayoutBlock>()
            {
                new Section(new MrkdwnText(context.Message.Content.Text)),
                new Context(":info-icon: This is a system notification."),
            }
        };
        await _slackApiClient.PostMessageWithRetryAsync(apiToken, request);
    }

    class ConcurrencyTracker
    {
        int _count;

        public int Increment()
        {
            return Interlocked.Increment(ref _count);
        }
    }
}
