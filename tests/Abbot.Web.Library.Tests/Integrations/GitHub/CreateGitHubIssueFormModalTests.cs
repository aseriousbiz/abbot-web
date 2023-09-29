using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious;
using Serious.Abbot;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Serious.TestHelpers;
using Xunit;

public class CreateGitHubIssueFormModalTests
{
    static InteractionCallbackInfo? Parse(string? callbackId) => CallbackInfo.TryParseAs<InteractionCallbackInfo>(callbackId, out var cb)
        ? cb
        : null;

    static async Task<TicketingIntegration> CreateIntegrationAsync(TestEnvironmentWithData env)
    {
        var integration = await env.Integrations.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.GitHub);
        var settings = new GitHubSettings
        {
            InstallationId = 42,
        };
        return new(integration, settings);
    }

    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task RendersMainViewIfConversationExists()
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateIntegrationAsync(env);

            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            convo.Title = $@"Hello {member.ToMention()} in {room.ToMention()}.";
            await env.Db.SaveChangesAsync();
            var modal = env.Activate<CreateGitHubIssueFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo);

            // Assume the default form.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:title"));
            Assert.NotNull(payload.Blocks.FindBlockById("__field:body"));
        }

        [Fact]
        public async Task RendersCustomFormIfOneIsEnabled()
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateIntegrationAsync(env);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization,
                SystemForms.CreateGitHubIssue,
                FormEngine.SerializeFormDefinition(new FormDefinition()
                {
                    Fields = {
                        new ()
                        {
                            Id = "example_field",
                            Type = FormFieldType.SingleLineText,
                        },
                    },
                }),
                enabled: true,
                env.TestData.Member);
            var modal = env.Activate<CreateGitHubIssueFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo);

            // Custom fields are present.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:example_field"));

            // No default fields
            Assert.Null(payload.Blocks.FindBlockById("__field:priority"));
            Assert.Null(payload.Blocks.FindBlockById("__field:description"));
        }

        [Fact]
        public async Task RendersDefaultFormIfCustomFormDisabled()
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateIntegrationAsync(env);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization,
                SystemForms.CreateGitHubIssue,
                FormEngine.SerializeFormDefinition(new FormDefinition()
                {
                    Fields = {
                        new ()
                        {
                            Id = "example_field",
                            Type = FormFieldType.SingleLineText,
                        },
                    },
                }),
                enabled: false,
                env.TestData.Member);
            var modal = env.Activate<CreateGitHubIssueFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo);

            // Should have the default form fields.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:title"));
            Assert.NotNull(payload.Blocks.FindBlockById("__field:body"));

            // No custom fields.
            Assert.Null(payload.Blocks.FindBlockById("__field:example_field"));
        }
    }

    public class TheOnSubmissionAsyncMethod
    {
        [Fact]
        public async Task ShowsAlertIfConvoCannotBeFound()
        {
            var env = TestEnvironment.Create();
            var modal = env.Activate<CreateGitHubIssueFormModal>();
            var platformEvent = new FakePlatformEvent<ViewSubmissionPayload>(
                new()
                {
                    View = new ModalView()
                    {
                        CallbackId = InteractionCallbackInfo.For<CreateGitHubIssueFormModal>("246"),
                        PrivateMetadata = "1234",
                    }
                },
                env.TestData.Member,
                env.TestData.Organization);

            var context = new ViewContext<ViewSubmissionPayload>(platformEvent, modal);

            await modal.OnSubmissionAsync(context);

            var action = Assert.IsType<UpdateResponseAction>(platformEvent.Responder.ResponseAction);
            AssertAlert(action.View,
                $":warning: Conversation could not be found, contact '{WebConstants.SupportEmail}' for assistance.",
                "Internal Error");
        }

        [Fact]
        public async Task EnqueuesTicketLinkIfConvoFound()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITicketIntegrationService>(out var ticketService)
                .Build();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization, SystemForms.CreateGitHubIssue, FormEngine.SerializeFormDefinition(new FormDefinition()
            {
                Fields = {
                    new FormFieldDefinition
                    {
                        Id = "title",
                        Title = "Title",
                        Type = FormFieldType.SingleLineText,
                        Required = true,
                    },
                    new FormFieldDefinition
                    {
                        Id = "body",
                        Title = "Body",
                        Type = FormFieldType.MultiLineText,
                        Required = false,
                        InitialValue = "{{Conversation.Title}}",
                    },
                    new FormFieldDefinition()
                    {
                        Id = $"custom_field:{long.MaxValue}",
                        Title = "Custom Field",
                        Type = FormFieldType.SingleLineText,
                        Required = false,
                    },
                    new FormFieldDefinition()
                    {
                        Id = "custom_field:1234",
                        Title = "Fixed Field",
                        Type = FormFieldType.FixedValue,
                        Required = false,
                        InitialValue = "{{Conversation.Id}}",
                    },
                },
            }), enabled: true, env.TestData.Member);
            var modal = env.Activate<CreateGitHubIssueFormModal>();
            var platformEvent = new FakePlatformEvent<ViewSubmissionPayload>(
                new()
                {
                    View = new ModalView
                    {
                        CallbackId = InteractionCallbackInfo.For<CreateGitHubIssueFormModal>("246"),
                        PrivateMetadata = $"{convo.Id}",
                        State = new(new Dictionary<string, Dictionary<string, IPayloadElement>>()
                        {
                            { "__field:title", new() { { "aaaa", new PlainTextInput() { Value = "Title" } } } },
                            { "__field:body", new() { { "aaaa", new PlainTextInput() { Value = "Body" } } } },
                            { $"__field:custom_field:{long.MaxValue}", new() { { "aaaa", new PlainTextInput() { Value = "it's custom" } } } },
                        })
                    }
                },
                env.TestData.Member,
                env.TestData.Organization);

            var context = new ViewContext<ViewSubmissionPayload>(platformEvent, modal);

            await modal.OnSubmissionAsync(context);

            ticketService.Received()
                .EnqueueTicketLinkRequest<GitHubSettings>(
                    new Id<Integration>(246),
                    convo,
                    context.FromMember,
                    Arg.Do<IReadOnlyDictionary<string, object?>>(properties => {
                        Assert.Equal("Title", Assert.Contains("title", properties));
                        Assert.Equal("", Assert.Contains("body", properties));
                        Assert.Equal($"{convo.Id}", Assert.Contains("custom_field:1234", properties));
                        Assert.Equal("it's custom", Assert.Contains($"custom_field:{long.MaxValue}", properties));
                    }));

            var action = Assert.IsType<UpdateResponseAction>(platformEvent.Responder.ResponseAction);
            AssertAlert(action.View,
                ":white_check_mark: Got it! I’m going to work on creating that ticket now. I’ll send you a DM when I’m done.",
                "Request Accepted");
        }
    }

    static void AssertModalCommon(TicketingIntegration ticketing, [NotNull] ViewUpdatePayload? payload, Conversation conversation)
    {
        Assert.NotNull(payload);
        Assert.Equal(InteractionCallbackInfo.For<CreateGitHubIssueFormModal>($"{ticketing.Integration.Id}"),
            Parse(payload.CallbackId));

        Assert.Equal("Create GitHub Issue", payload.Title);
        Assert.Equal("Cancel", payload.Close?.Text);
        Assert.Equal("Create", payload.Submit?.Text);
        Assert.Equal($"{conversation.Id}", payload.PrivateMetadata);
    }

    static void AssertAlert([NotNull] ViewUpdatePayload? payload, string message, string title)
    {
        Assert.NotNull(payload);
        Assert.Equal(InteractionCallbackInfo.For<AlertModal>(),
            Parse(payload.CallbackId));

        Assert.Equal(title, payload.Title);
        Assert.Equal("Back", payload.Close?.Text);
        Assert.Null(payload.Submit);
        Assert.Collection(payload.Blocks,
            b => {
                var section = Assert.IsType<Section>(b);
                Assert.Equal(message, section.Text?.Text);
            });
    }
}
