using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using MassTransit;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing;
using Serious.Abbot.Eventing.Consumers;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Eventing.StateMachines;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Slack;
using Serious.TestHelpers;

namespace Abbot.Web.Library.Tests.Playbooks;

[UsesVerify]
public class PlaybookIntegrationTests
{
    [Theory]
    [InlineData("playbook.standup-reminder_v7")]
    [InlineData("playbook.standup-reminder_v8")]
    public async Task StandupReminder(string filename)
    {
        var env = TestEnvironmentBuilder.Create()
            .AddAbbotSagaConfig()
            .AddBusConsumer<StepRunnerConsumer>()
            .Build(snapshot: true);

        // Create our #general
        await env.CreateRoomAsync("C012ZJGPYTF");
        env.StopwatchFactory.Elapsed = TimeSpan.FromMilliseconds(123); // Step duration

        var definition = await EmbeddedResourceHelper.ReadPlaybookDefinitionResource(filename);
        var playbook = await env.CreatePlaybookAsync(webhookTriggerTokenSeed: "seedy");
        var version = await env.CreatePlaybookVersionAsync(playbook, definition);

        var dispatcher = env.Activate<PlaybookDispatcher>();

        var runGroup = await dispatcher.DispatchAsync(
            version,
            ScheduleTrigger.Id,
            new Dictionary<string, object?>());

        Assert.NotNull(runGroup);
        var run = Assert.Single(runGroup.Runs);

        await env.WaitForPlaybookRunAsync(run);

        await env.ReloadAsync(runGroup, run);

        await env.VerifyPlaybookRun(run, new {
            env.SlackApi.PostedMessages,
        }).UseParameters(filename);
    }
}

public record PlaybookVerifyPayload(
    PlaybookRunGroup? RunGroup,
    PlaybookRun Run,
    IReadOnlyList<AuditEventBase> AuditLog,
    IReadOnlyList<LogEvent> Logs,
    [property: Argon.JsonExtensionData]
    Argon.JObject? Additional = null);

public static class PlaybookIntegrationTestExtensions
{
    public static SettingsTask VerifyPlaybookRun(
        this TestEnvironmentWithData env,
        PlaybookRun run,
        object? additional = null)
    {
        return VerifierExt.Verify(async () =>
            new PlaybookVerifyPayload(
                run.Group,
                run,
                await env.GetAllActivityAsync(),
                env.GetAllLogs(),
                additional == null
                    ? null
                    : Argon.JObject.FromObject(additional)));
    }
}
