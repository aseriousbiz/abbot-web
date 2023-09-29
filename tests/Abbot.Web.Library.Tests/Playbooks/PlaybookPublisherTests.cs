using Abbot.Common.TestHelpers;
using Hangfire.Common;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;

public class PlaybookPublisherTests
{
    public class ThePublishAsyncMethod
    {
        [Fact]
        public async Task UpdatesExistingVersion()
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();
            var definition = PlaybookFormat.Serialize(new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("signal", "system.signal")
                },
                Sequences =
                {
                    ["seq_1"] = new ActionSequence
                    {
                        Actions =
                        {
                            new("unique_id_2", "test"),
                            new("shared_id_1", "test"),
                        }
                    }
                }
            });

            var playbook = await env.CreatePlaybookAsync();
            var version1 = await env.Playbooks.CreateVersionAsync(playbook, definition, comment: null, actor);
            Assert.Equal(1, version1.Version);
            var publisher = env.Activate<PlaybookPublisher>();

            var version = await publisher.PublishAsync(playbook, actor);

            Assert.NotNull(version.PublishedAt);
            Assert.Equal(version1.Id, version.Id);
            Assert.Equal(1, version.Version);
        }

        [Fact]
        public async Task CreatesRecurringJobForEachScheduleTrigger()
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();
            var definition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_1", "system.schedule")
                    {
                        Inputs =
                        {
                            new("schedule",
                                JObject.FromObject(new
                                {
                                    type = "advanced",
                                    cron = "*/10 * * * *"
                                })),
                            new("tz", "America/New_York"),
                        }
                    },
                    new TriggerStep("trigger_2", "system.schedule")
                    {
                        Inputs =
                        {
                            new("schedule",
                                JObject.FromObject(new
                                {
                                    type = "hourly",
                                    minute = 10,
                                })),
                            new("tz", "America/Los_Angeles"),
                        }
                    }
                },
                Sequences =
                {
                    ["seq_1"] = new ActionSequence
                    {
                        Actions =
                        {
                            new("unique_id_2", "test"),
                            new("shared_id_1", "test"),
                        }
                    }
                }
            };

            var serializedDefinition = PlaybookFormat.Serialize(definition);
            var playbook = await env.CreatePlaybookAsync();
            var version1 = await env.Playbooks.CreateVersionAsync(playbook, serializedDefinition, comment: null, actor);
            Assert.Equal(1, version1.Version);
            var publisher = env.Activate<PlaybookPublisher>();

            await publisher.PublishAsync(playbook, actor);

            var recurringJobs = env.RecurringJobManager.RecurringJobs;
            Assert.Equal(2, recurringJobs.Count);
            var (firstJob, firstSchedule) = recurringJobs[$"Playbook_{playbook.Id}_1_trigger_1"];
            var (secondJob, secondSchedule) = recurringJobs[$"Playbook_{playbook.Id}_1_trigger_2"];
            Assert.Equal(("*/10 * * * *", "10 * * * *"), (firstSchedule, secondSchedule));
            Assert.Equal(playbook.Id, (Id<Playbook>)firstJob.Args[0]);
            Assert.Equal(playbook.Id, (Id<Playbook>)secondJob.Args[0]);
            Assert.Equal(1, (int)firstJob.Args[1]);
            Assert.Equal(1, (int)secondJob.Args[1]);
            Assert.Equal(definition.Triggers[0].Id, (string)firstJob.Args[2]);
            Assert.Equal(definition.Triggers[1].Id, (string)secondJob.Args[2]);
        }

        [Fact]
        public async Task OverwritesAndRemovesPreviousScheduledTriggers()
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();
            var definition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_1", "system.schedule")
                    {
                        Inputs =
                        {
                            new("schedule",
                                JObject.FromObject(new
                                {
                                    type = "advanced",
                                    cron = "*/10 * * * *",
                                })),
                            new("tz", "America/New_York"),
                        }
                    },
                    new TriggerStep("trigger_2", "system.schedule")
                    {
                        Inputs =
                        {
                            new("schedule",
                                JObject.FromObject(new
                                {
                                    type = "hourly",
                                    minute = 10,
                                })),
                            new("tz", "America/Los_Angeles"),
                        }
                    }
                },
                Sequences =
                {
                    ["seq_1"] = new ActionSequence
                    {
                        Actions =
                        {
                            new("unique_id_2", "test"),
                            new("shared_id_1", "test"),
                        }
                    }
                }
            };

            var serializedDefinition = PlaybookFormat.Serialize(definition);
            var playbook = await env.CreatePlaybookAsync();
            var version1 = await env.Playbooks.CreateVersionAsync(playbook, serializedDefinition, comment: null, actor);
            Assert.Equal(1, version1.Version);
            var publisher = env.Activate<PlaybookPublisher>();
            await publisher.PublishAsync(playbook, actor);
            // New definition adds trigger_3, removes trigger_2, and changes trigger_1
            var newDefinition = new PlaybookDefinition
            {
                StartSequence = "seq_1",
                Triggers =
                {
                    new TriggerStep("trigger_1", "system.schedule")
                    {
                        Inputs =
                        {
                            new("schedule",
                                JObject.FromObject(new
                                {
                                    type = "monthly",
                                    minute = 0,
                                    hour = 15,
                                    dayOfMonth = 10,
                                })),
                            new("tz", "Europe/Amsterdam"),
                        }
                    },
                    new TriggerStep("trigger_3", "system.schedule")
                    {
                        Inputs =
                        {
                            new("schedule",
                                JObject.FromObject(new
                                {
                                    type = "advanced",
                                    cron = "0 11 11 11 11 ?",
                                })),
                            new("tz", "America/Los_Angeles"),
                        }
                    }
                },
                Sequences =
                {
                    ["seq_1"] = new ActionSequence
                    {
                        Actions =
                        {
                            new("unique_id_2", "test"),
                            new("shared_id_1", "test"),
                        }
                    }
                }
            };

            var newSerializedDefinition = PlaybookFormat.Serialize(newDefinition);
            await env.Playbooks.CreateVersionAsync(playbook, newSerializedDefinition, comment: null, actor);

            await publisher.PublishAsync(playbook, actor);

            var recurringJobs = env.RecurringJobManager.RecurringJobs;
            Assert.Equal(2, recurringJobs.Count);
            var (firstJob, firstSchedule) = recurringJobs[$"Playbook_{playbook.Id}_2_trigger_1"];
            var (secondJob, secondSchedule) = recurringJobs[$"Playbook_{playbook.Id}_2_trigger_3"];
            Assert.Equal(("0 15 10 * *", "0 11 11 11 11 ?"), (firstSchedule, secondSchedule));
            Assert.Equal(playbook.Id, (Id<Playbook>)firstJob.Args[0]);
            Assert.Equal(playbook.Id, (Id<Playbook>)secondJob.Args[0]);
            Assert.Equal(2, (int)firstJob.Args[1]); // Version
            Assert.Equal(2, (int)secondJob.Args[1]); // Version
            Assert.Equal(newDefinition.Triggers[0].Id, ((string)firstJob.Args[2]));
            Assert.Equal(newDefinition.Triggers[1].Id, ((string)secondJob.Args[2]));
        }
    }

    public class TheSetPlaybookEnabledAsyncMethod
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UpdatesPlaybookStateInDb(bool setEnabledTo)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync("p1");
            await env.UpdateAsync(playbook, p => p.Enabled = !setEnabledTo);

            var publisher = env.Activate<PlaybookPublisher>();
            await publisher.SetPlaybookEnabledAsync(playbook, setEnabledTo, env.TestData.Member);

            await env.ReloadAsync(playbook);
            Assert.Equal(setEnabledTo, playbook.Enabled);
        }

        [Fact]
        public async Task InstallsHangfireJobsWhenEnabled()
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();
            var version = await env.CreatePlaybookVersionAsync(playbook,
                published: true,
                definition: new()
                {
                    StartSequence = "seq_1",
                    Sequences =
                    {
                        ["seq_1"] = new ActionSequence()
                    },
                    Triggers =
                    {
                        new TriggerStep("trigger_1", "system.schedule")
                        {
                            Inputs =
                            {
                                new("schedule",
                                    JObject.FromObject(new
                                    {
                                        type = "advanced",
                                        cron = "*/10 * * * *",
                                    })),
                                new("tz", "America/New_York"),
                            }
                        },
                    },
                });

            // It doesn't actually matter what the _old_ state was, we'll reinstall triggers if they don't exist when you call the method.
            Assert.Empty(env.RecurringJobManager.RecurringJobs);

            var publisher = env.Activate<PlaybookPublisher>();
            await publisher.SetPlaybookEnabledAsync(playbook, true, env.TestData.Member);

            Assert.Collection(env.RecurringJobManager.RecurringJobs,
                j1 => {
                    Assert.Equal($"Playbook_{playbook.Id}_1_trigger_1", j1.Key);
                    Assert.Equal("*/10 * * * *", j1.Value.Schedule);
                    Assert.Equal(playbook.Id, (Id<Playbook>)j1.Value.Job.Args[0]);
                });
        }

        [Fact]
        public async Task UninstallsHangfireJobsWhenDisabled()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            var playbook = await env.CreatePlaybookAsync();
            var version = await env.CreatePlaybookVersionAsync(playbook,
                published: false,
                definition: new()
                {
                    StartSequence = "seq_1",
                    Sequences =
                    {
                        ["seq_1"] = new ActionSequence()
                    },
                    Triggers =
                    {
                        new TriggerStep("trigger_1", "system.schedule")
                        {
                            Inputs =
                            {
                                new("schedule",
                                    JObject.FromObject(new
                                    {
                                        type = "advanced",
                                        cron = "*/10 * * * *",
                                    })),
                                new("tz", "America/New_York"),
                            }
                        },
                    },
                });

            var publisher = env.Activate<PlaybookPublisher>();
            await publisher.PublishAsync(playbook, agent);

            // Installed on Publish, since Enabled
            Assert.Collection(env.RecurringJobManager.RecurringJobs,
                j1 => {
                    Assert.Equal($"Playbook_{playbook.Id}_1_trigger_1", j1.Key);
                    Assert.Equal("*/10 * * * *", j1.Value.Schedule);
                    Assert.Equal(playbook.Id, (Id<Playbook>)j1.Value.Job.Args[0]);
                });

            await publisher.SetPlaybookEnabledAsync(playbook, false, env.TestData.Member);

            Assert.Empty(env.RecurringJobManager.RecurringJobs);
        }
    }
}
