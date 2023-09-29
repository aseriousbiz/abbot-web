using Abbot.Common.TestHelpers;
using NSubstitute.ReturnsExtensions;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;

public class PlaybookRepositoryTests
{
    public class TheGetCurrentVersionAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullForMissingPlaybook()
        {
            var env = TestEnvironment.Create();

            await env.CreatePlaybookAsync();

            using var _ = env.ActivateInNewScope<PlaybookRepository>(out var isolated);

            var result = await isolated.GetCurrentVersionAsync(NonExistent.PlaybookId, includeDraft: false, includeDisabled: false);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullForPlaybookWithNoVersions()
        {
            var env = TestEnvironment.Create();

            var playbook = await env.CreatePlaybookAsync();

            using var _ = env.ActivateInNewScope<PlaybookRepository>(out var isolated);

            var result = await isolated.GetCurrentVersionAsync(playbook, includeDraft: false, includeDisabled: false);

            Assert.Null(result);
        }

        [Theory]
        [InlineData(false, null)]
        [InlineData(true, 3)]
        public async Task ReturnsNullForPlaybookWithOnlyUnpublishedVersions(bool includeDraft, int? expectedVersion)
        {
            var env = TestEnvironment.Create();

            var playbook = await env.CreatePlaybookAsync();

            var actor = env.TestData.Member;
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);

            using var _ = env.ActivateInNewScope<PlaybookRepository>(out var isolated);

            var result = await isolated.GetCurrentVersionAsync(playbook, includeDraft, includeDisabled: false);

            Assert.Equal(expectedVersion, result?.Version);
        }

        [Fact]
        public async Task ReturnsNullForDisabledPlaybook()
        {
            var env = TestEnvironment.Create();

            var playbook = await env.CreatePlaybookAsync();

            var actor = await env.CreateMemberInAgentRoleAsync();
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);
            await env.PlaybookPublisher.PublishAsync(playbook, actor);
            await env.Playbooks.SetPlaybookEnabledAsync(playbook, false, actor);

            using var _ = env.ActivateInNewScope<PlaybookRepository>(out var isolated);

            var result = await isolated.GetCurrentVersionAsync(playbook, includeDraft: true, includeDisabled: false);

            Assert.Null(result);
        }

        [Theory]
        [InlineData(false, 3)]
        [InlineData(true, 4)]
        public async Task ReturnsPlaybookWithCurrentVersion(bool includeDraft, int expectedVersion)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync(slug: "right", webhookTriggerTokenSeed: "ToKeN");
            var actor = await env.CreateMemberInAgentRoleAsync();
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);
            await env.PlaybookPublisher.PublishAsync(playbook, actor);
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);
            await env.PlaybookPublisher.PublishAsync(playbook, actor);
            await env.Playbooks.CreateVersionAsync(playbook, "{}", comment: null, actor);

            using var _ = env.ActivateInNewScope<PlaybookRepository>(out var isolated);

            var result = await isolated.GetCurrentVersionAsync(playbook, includeDraft, includeDisabled: false);

            Assert.NotNull(result);
            Assert.Equal(playbook.Id, result.PlaybookId);
            Assert.Equal(expectedVersion, result.Version);

            Assert.Equal(playbook.Id, result.PlaybookId);
            Assert.Equal(playbook.Id, result.Playbook.Id);
            Assert.NotSame(playbook, result.Playbook); // Ensure isolation

            Assert.NotNull(result.Playbook.Organization);
        }
    }

    public class TheGetCurrentVersionsWithTriggerTypeAsyncMethod
    {
        [Theory]
        [InlineData("abbot-added", false, false, new[] { "p1", "p2", "p4" })]
        [InlineData("http", false, false, new[] { "p3", "p6" })]
        [InlineData("abbot-added", true, false, new[] { "p1", "p2", "p5" })]
        [InlineData("http", true, false, new[] { "p3", "p4", "p6" })]
        [InlineData("http", true, true, new[] { "p3", "p4", "p6", "p7" })]
        public async Task RetrievesCurrentVersions(string trigger, bool includeDraft, bool includeDisabled, string[] expectedVersions)
        {
            var env = TestEnvironment.Create();
            var actor = await env.CreateMemberInAgentRoleAsync();

            async Task CreatePlaybookVersion(Playbook playbook, string triggerType, bool published)
            {
                var definition = $"{{\"triggers\":[{{\"id\": \"1\", \"type\":\"{triggerType}\"}}]}}";

                await env.Playbooks.CreateVersionAsync(playbook, definition, comment: null, actor);
                if (published)
                {
                    await env.PlaybookPublisher.PublishAsync(playbook, actor);
                }
            }

            var playbook1 = await env.CreatePlaybookAsync(slug: "p1");
            var playbook2 = await env.CreatePlaybookAsync(slug: "p2");
            var playbook3 = await env.CreatePlaybookAsync(slug: "p3");
            var playbook4 = await env.CreatePlaybookAsync(slug: "p4");
            var playbook5 = await env.CreatePlaybookAsync(slug: "p5");
            var playbook6 = await env.CreatePlaybookAsync(slug: "p6");
            var playbook7 = await env.CreatePlaybookAsync(slug: "p7");
            await env.Playbooks.SetPlaybookEnabledAsync(playbook7, false, env.TestData.Member);

            await CreatePlaybookVersion(playbook1, "http", published: true);
            await CreatePlaybookVersion(playbook1, "abbot-added", published: true);

            await env.Playbooks.CreateVersionAsync(playbook2, """{}""", comment: null, actor);
            await CreatePlaybookVersion(playbook2, "abbot-added", published: true);

            await CreatePlaybookVersion(playbook3, "abbot-added", published: true);
            await CreatePlaybookVersion(playbook3, "http", published: true);

            await CreatePlaybookVersion(playbook4, "abbot-added", published: true);
            await CreatePlaybookVersion(playbook4, "http", published: false);

            await CreatePlaybookVersion(playbook5, "abbot-added", published: false);

            await CreatePlaybookVersion(playbook6, "http", published: true);

            await CreatePlaybookVersion(playbook7, "http", published: true);

            using var _ = env.ActivateInNewScope<PlaybookRepository>(out var isolated);

            var result = await isolated.GetLatestPlaybookVersionsWithTriggerTypeAsync(
                trigger,
                env.TestData.Organization,
                includeDraft,
                includeDisabled);

            var definitions = result.Select(pv => PlaybookFormat.Deserialize(pv.SerializedDefinition)).ToList();
            Assert.All(definitions,
                d => Assert.True(d.Triggers.Any(t => t.Type == trigger)));
            Assert.Equal(expectedVersions, result.Select(pv => pv.Playbook.Slug).ToArray());
        }
    }

    public class TheUpdateLatestVersionAsyncMethod
    {
        [Fact]
        public async Task UpdatesExistingUnpublishedVersion()
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

            var version = await env.Playbooks.UpdateLatestVersionAsync(playbook, definition, actor);

            Assert.Null(version.PublishedAt);
            Assert.Equal(1, version.Version);
        }

        [Fact]
        public async Task CreatesNewPublishedVersionIfLatestPublished()
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
            var publishedVersion = await env.PlaybookPublisher.PublishAsync(playbook, actor);
            Assert.Equal(1, version1.Version);
            Assert.Equal(1, publishedVersion.Version);

            var version = await env.Playbooks.UpdateLatestVersionAsync(playbook, definition, actor);

            Assert.Null(version.PublishedAt);
            Assert.Equal(2, version.Version);
        }
    }

    public class TheSetPlaybookEnabledAsyncMethod
    {
        [Theory]
        [InlineData(true, "Enabled", "Playbook `P1` enabled.")]
        [InlineData(false, "Disabled", "Playbook `P1` disabled.")]
        public async Task SetsEnabledCorrectlyAndProducesAuditEvent(bool setEnabledTo, string eventName, string eventDescription)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync("p1");
            playbook.Enabled = !setEnabledTo;
            await env.Db.SaveChangesAsync();

            await env.Playbooks.SetPlaybookEnabledAsync(playbook, setEnabledTo, env.TestData.Member);

            await env.ReloadAsync(playbook);
            Assert.Equal(setEnabledTo, playbook.Enabled);
            var auditEvent = await env.AuditLog.GetMostRecentLogEntryAs<AuditEvent>(env.TestData.Organization);
            Assert.NotNull(auditEvent);
            Assert.Equal(new("Playbook", eventName), auditEvent.Type);
            Assert.Equal(eventDescription, auditEvent.Description);
            Assert.Equal(playbook.Id, auditEvent.EntityId);
        }
    }
}
