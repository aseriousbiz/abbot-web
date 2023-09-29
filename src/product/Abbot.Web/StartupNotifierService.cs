using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot;

/// <summary>
/// Runs _after_ data seeders to post a notification indicating Abbot is running.
/// </summary>
public class StartupNotifierService : IHostedService
{
    readonly IBus _bus;
    readonly IHostEnvironment _hostEnvironment;

    // "IPublishEndpoint is scoped, and can only be resolved in a scope. So if you have a scope, resolve IPublishEndpoint. Otherwise, use IBus."
    // - https://github.com/MassTransit/MassTransit/discussions/3106#discussioncomment-2009763
    public StartupNotifierService(IBus bus, IHostEnvironment hostEnvironment)
    {
        _bus = bus;
        _hostEnvironment = hostEnvironment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var message =
            $@"Abbot has started in the {_hostEnvironment.EnvironmentName} environment. " +
            $"<https://github.com/aseriousbiz/abbot/commit/{Program.BuildMetadata.CommitId}|Commit: `{Program.BuildMetadata.CommitId}`>.";

        await _bus.Publish(
            new PublishSystemNotification()
            {
                DeduplicationKey = "startup",
                Content = new MessageContent()
                {
                    Text = message
                }
            },
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
