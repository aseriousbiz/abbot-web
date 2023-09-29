using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Playbooks;
using Xunit.Abstractions;

public class PlaybookDispatcherTests
{
    public class TheDispatchAsyncMethod
    {
        [Fact]
        public async Task DispatchesToPublishedPlaybooks()
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();
            var playbook = await env.CreatePlaybookAsync();
            var definition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_1", "http.webhook.customer")
                },
                Sequences =
                {
                    ["seq_1"] = new ActionSequence(),
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(definition),
                comment: null,
                env.TestData.Member);
            await env.PlaybookPublisher.PublishAsync(playbook, actor);
            var laterDefinition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_2", "not.http.webhook")
                },
                Sequences =
                {
                    ["seq_1"] = new(),
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(laterDefinition),
                comment: null,
                env.TestData.Member);

            var outputs = new Dictionary<string, object?>
            {
                ["channel"] = new { id = "C0123412324", name = "Some channel" }
            };
            var dispatcher = env.Activate<PlaybookDispatcher>();

            await dispatcher.DispatchAsync(
                "http.webhook.customer",
                outputs,
                env.TestData.Organization);

            var published = await env.BusTestHarness.Published.SelectAsync<ExecutePlaybook>()
                .ToListAsync();
            var executedPlaybook = Assert.Single(published).Context.Message;
            Assert.NotNull(executedPlaybook);
            Assert.Equal("trigger_1", executedPlaybook.PlaybookTriggerId);
            var run = await env.Db.PlaybookRuns.SingleAsync();
            Assert.Equal(run.PlaybookId, playbook.Id);
        }
    }

    public class TheDispatchSignalAsyncMethod
    {
        [Fact]
        public async Task DispatchesToPublishedPlaybooks()
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();
            var playbook = await env.CreatePlaybookAsync();
            var definition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_1", "system.signal")
                    {
                        Inputs =
                        {
                            ["signal"] = "my-signal"
                        }
                    }
                },
                Sequences =
                {
                    ["seq_1"] = new(),
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(definition),
                comment: null,
                env.TestData.Member);

            await env.PlaybookPublisher.PublishAsync(playbook, actor);
            var anotherPlaybook = await env.CreatePlaybookAsync();
            var anotherDefinition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_3", "system.signal")
                    {
                        Inputs =
                        {
                            ["signal"] = "not-my-signal"
                        }
                    }
                }
            };
            await env.Playbooks.CreateVersionAsync(
                anotherPlaybook,
                PlaybookFormat.Serialize(anotherDefinition),
                comment: null,
                env.TestData.Member);
            await env.PlaybookPublisher.PublishAsync(anotherPlaybook, actor);

            var laterDefinition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_2", "system.signal")
                    {
                        Inputs =
                        {
                            ["signal"] = "my-signal"
                        }
                    }
                },
                Sequences =
                {
                    ["seq_1"] = new(),
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(laterDefinition),
                comment: null,
                env.TestData.Member);

            var outputs = new Dictionary<string, object?>
            {
                ["channel"] = new { id = "C0123412324", name = "Some channel" },
                ["arguments"] = "some arguments"
            };
            var dispatcher = env.Activate<PlaybookDispatcher>();

            await dispatcher.DispatchSignalAsync(
                new()
                {
                    Name = "my-signal",
                    Arguments = "my-signal-args",
                    Source = new()
                    {
                        SkillName = "my-skill",
                        Arguments = "my-skill-args",
                        SignalEvent = new()
                        {
                            Name = "our-signal",
                            Arguments = "our-signal-args",
                            Source = new()
                            {
                                SkillName = "our-skill",
                                Arguments = "our-skill-args",
                            },
                        },
                    },
                },
                outputs,
                env.TestData.Organization);

            var published = await env.BusTestHarness.Published.SelectAsync<ExecutePlaybook>()
                .ToListAsync();
            var executedPlaybook = Assert.Single(published).Context.Message;
            Assert.Equal("trigger_1", executedPlaybook.PlaybookTriggerId);
            var run = await env.Db.PlaybookRuns.SingleAsync();
            Assert.Equal(run.PlaybookId, playbook.Id);
            Assert.Equal("trigger_1", run.Properties.Trigger);
            Assert.Equal("some arguments", run.Properties.StepResults.First().Value.Outputs["arguments"]);

            var runSignal = run.Properties.SignalMessage;
            Assert.NotNull(runSignal);
            Assert.Equal("my-signal", runSignal.Name);
            Assert.Equal("my-signal-args", runSignal.Arguments);
            Assert.Equal("my-skill", runSignal.Source.SkillName);
            Assert.Equal("my-skill-args", runSignal.Source.Arguments);
            Assert.NotNull(runSignal.Source.SignalEvent);
            Assert.Equal("our-signal", runSignal.Source.SignalEvent.Name);
            Assert.Equal("our-signal-args", runSignal.Source.SignalEvent.Arguments);
            Assert.Equal("our-skill", runSignal.Source.SignalEvent.Source.SkillName);
            Assert.Equal("our-skill-args", runSignal.Source.SignalEvent.Source.Arguments);
            Assert.Null(runSignal.Source.SignalEvent.Source.SignalEvent);
        }
    }

    public class TheDispatchScheduledRunAsyncMethod
    {
        [Fact]
        public async Task DispatchesToPublishedPlaybooks()
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();
            var playbook = await env.CreatePlaybookAsync();
            var definition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_1", "system.schedule")
                },
                Sequences =
                {
                    ["seq_1"] = new (),
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(definition),
                comment: null,
                env.TestData.Member);
            var publishedVersion = await env.PlaybookPublisher.PublishAsync(playbook, actor);
            var laterDefinition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_2", "http.webhook.customer")
                },
                Sequences =
                {
                    ["seq_1"] = new (),
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(laterDefinition),
                comment: null,
                env.TestData.Member);
            var dispatcher = env.Activate<PlaybookDispatcher>();

            await dispatcher.DispatchScheduledRunAsync(
                playbook,
                publishedVersion.Version,
                definition.Triggers[0].Id);

            var published = await env.BusTestHarness.Published.SelectAsync<ExecutePlaybook>()
                .ToListAsync();
            var executedPlaybook = Assert.Single(published).Context.Message;
            Assert.NotNull(executedPlaybook);
            Assert.Equal("trigger_1", executedPlaybook.PlaybookTriggerId);
            var run = await env.Db.PlaybookRuns.SingleAsync();
            Assert.Equal(run.PlaybookId, playbook.Id);
        }

        [Fact]
        public async Task DoesNotDispatchWrongVersion()
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();
            var playbook = await env.CreatePlaybookAsync();
            var definition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_1", "system.schedule")
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(definition),
                comment: null,
                env.TestData.Member);
            var publishedVersion = await env.PlaybookPublisher.PublishAsync(playbook, actor);
            var laterDefinition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_2", "http.webhook.customer")
                }
            };
            await env.Playbooks.CreateVersionAsync(
                playbook,
                PlaybookFormat.Serialize(laterDefinition),
                comment: null,
                env.TestData.Member);
            var dispatcher = env.Activate<PlaybookDispatcher>();

            await dispatcher.DispatchScheduledRunAsync(
                playbook,
                publishedVersion.Version - 1,
                definition.Triggers[0].Id);

            var published = await env.BusTestHarness.Published.SelectAsync<ExecutePlaybook>()
                .ToListAsync();
            Assert.Empty(published);
        }
    }
}
