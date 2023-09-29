using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Security;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Serious.TestHelpers;
using Xunit;

public class CreateZendeskTicketFormModalTests
{
    static InteractionCallbackInfo? Parse(string? callbackId) => CallbackInfo.TryParseAs<InteractionCallbackInfo>(callbackId, out var cb)
        ? cb
        : null;

    static async Task<TicketingIntegration> CreateIntegrationAsync(TestEnvironmentWithData env)
    {
        var integration = await env.Integrations.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);
        var settings = new ZendeskSettings
        {
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
            var modal = env.Activate<CreateZendeskTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);

            AssertModalCommon(ticketing, payload, convo.Id, null);
            var subjectInput = payload.Blocks.FindInputElementByBlockId<PlainTextInput>("subject_input");
            Assert.NotNull(subjectInput);
            Assert.Empty(subjectInput.InitialValue ?? string.Empty);
            Assert.NotNull(payload.Blocks.FindBlockById("info_section"));

            // Assume the default form.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:comment"));
            Assert.NotNull(payload.Blocks.FindBlockById("__field:priority"));
        }

        [Fact]
        public async Task RendersCustomFormIfOneIsEnabled()
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateIntegrationAsync(env);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization,
                SystemForms.CreateZendeskTicket,
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
            var modal = env.Activate<CreateZendeskTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo.Id, null);
            Assert.NotNull(payload.Blocks.FindBlockById("subject_input"));
            Assert.NotNull(payload.Blocks.FindBlockById("info_section"));

            // Custom fields are present.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:example_field"));

            // No default fields
            Assert.Null(payload.Blocks.FindBlockById("__field:comment"));
            Assert.Null(payload.Blocks.FindBlockById("__field:priority"));
        }

        [Fact]
        public async Task RendersDefaultFormIfCustomFormDisabled()
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateIntegrationAsync(env);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization,
            SystemForms.CreateZendeskTicket,
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
            var modal = env.Activate<CreateZendeskTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo.Id, null);
            Assert.NotNull(payload.Blocks.FindBlockById("subject_input"));
            Assert.NotNull(payload.Blocks.FindBlockById("info_section"));

            // Should have the default form fields.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:comment"));
            Assert.NotNull(payload.Blocks.FindBlockById("__field:priority"));

            // No custom fields.
            Assert.Null(payload.Blocks.FindBlockById("__field:example_field"));
        }

        [Theory]
        [InlineData(Roles.Administrator, "<https://d3v-test.zendesk.com/agent/organizations/123|The Derek Zoolander Center for Kids Who Can't Read Good>")]
        [InlineData(Roles.Agent, "<https://d3v-test.zendesk.com/agent/organizations/123|The Derek Zoolander Center for Kids Who Can't Read Good>")]
        [InlineData(null, "The Derek Zoolander Center for Kids Who Can't Read Good")]
        public async Task IncludesOrganizationIfOneIsLinkedToTheRoom(string? roleName, string expectedOrganizationValue)
        {
            var env = TestEnvironment.Create();
            await env.AddUserToRoleAsync(env.TestData.Member, roleName);
            var ticketing = await CreateIntegrationAsync(env);

            env.Clock.Freeze();
            var room = await env.CreateRoomAsync();
            var zdOrgLink = new ZendeskOrganizationLink("d3v-test", 123);
            await env.Rooms.CreateLinkAsync(
                room,
                RoomLinkType.ZendeskOrganization,
                zdOrgLink.ApiUrl.ToString(),
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);
            var convo = await env.CreateConversationAsync(room);
            var modal = env.Activate<CreateZendeskTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo.Id, zdOrgLink.ApiUrl.ToString());

            var infoSection = payload.Blocks.FindBlockById<Section>("info_section");
            Assert.NotNull(infoSection);
            Assert.Equal(expectedOrganizationValue, infoSection.Fields?.Last().Text);
        }
    }

    public class TheOnSubmissionAsyncMethod
    {
        [Fact]
        public async Task ShowsAlertIfConvoCannotBeFound()
        {
            var env = TestEnvironment.Create();
            var modal = env.Activate<CreateZendeskTicketFormModal>();
            var platformEvent = new FakePlatformEvent<ViewSubmissionPayload>(
                new()
                {
                    View = new ModalView()
                    {
                        CallbackId = InteractionCallbackInfo.For<CreateZendeskTicketFormModal>("246"),
                        PrivateMetadata = "1234|",
                        State = new(new Dictionary<string, Dictionary<string, IPayloadElement>>()
                        {
                            { "subject_input", new Dictionary<string, IPayloadElement> { { "aaaa", new PlainTextInput() { Value = "Subject" } } } },
                        }),
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

        [Theory]
        [InlineData(null)]
        [InlineData("https://d3v-test.zendesk.com/api/v2/organizations/1234.json")]
        public async Task EnqueuesTicketLinkIfConvoFound(string? organizationUrl)
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITicketIntegrationService>(out var ticketService)
                .Build();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization, SystemForms.CreateZendeskTicket, FormEngine.SerializeFormDefinition(new FormDefinition()
            {
                Fields = {
                    new FormFieldDefinition
                    {
                        Id = "comment",
                        Title = "Description",
                        Type = FormFieldType.MultiLineText,
                        Required = false,
                        InitialValue = "{{Conversation.Title}}",
                    },
                    new FormFieldDefinition
                    {
                        Id = "priority",
                        Title = "Priority",
                        Type = FormFieldType.DropdownList,
                        Required = true,
                        InitialValue = "normal",
                        Options =
                        {
                            new("Low", "low"),
                            new ("Normal", "normal"),
                            new ("High", "high"),
                            new ("Urgent", "urgent"),
                        }
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
            var modal = env.Activate<CreateZendeskTicketFormModal>();
            var platformEvent = new FakePlatformEvent<ViewSubmissionPayload>(
                new()
                {
                    View = new ModalView
                    {
                        CallbackId = InteractionCallbackInfo.For<CreateZendeskTicketFormModal>("246"),
                        PrivateMetadata = $"{convo.Id}|{organizationUrl}",
                        State = new(new Dictionary<string, Dictionary<string, IPayloadElement>>()
                        {
                            { "subject_input", new Dictionary<string, IPayloadElement> { { "aaaa", new PlainTextInput() { Value = "Subject" } } } },
                            { "__field:comment", new Dictionary<string, IPayloadElement> { { "aaaa", new PlainTextInput() { Value = "Description" } } } },
                            { "__field:priority", new Dictionary<string, IPayloadElement> { { "aaaa", new PlainTextInput() { Value = "urgent" } } } },
                            { $"__field:custom_field:{long.MaxValue}", new Dictionary<string, IPayloadElement> { { "aaaa", new PlainTextInput() { Value = "it's custom" } } } },
                        })
                    }
                },
                env.TestData.Member,
                env.TestData.Organization);

            var context = new ViewContext<ViewSubmissionPayload>(platformEvent, modal);

            await modal.OnSubmissionAsync(context);

            ticketService.Received()
                .EnqueueTicketLinkRequest<ZendeskSettings>(
                    new Id<Integration>(246),
                    convo,
                    context.FromMember,
                    Arg.Do<IReadOnlyDictionary<string, object?>>(properties => {
                        Assert.Equal("Subject", Assert.Contains("subject", properties));
                        Assert.Equal("Description", Assert.Contains("comment", properties));
                        Assert.Equal($"{convo.Id}", Assert.Contains("custom_field:1234", properties));
                        Assert.Equal("it's custom", Assert.Contains($"custom_field:{long.MaxValue}", properties));
                        Assert.Equal("urgent", Assert.Contains("priority", properties));

                        if (organizationUrl is null)
                        {
                            Assert.DoesNotContain("organizationLink", properties);
                        }
                        else
                        {
                            Assert.Equal(organizationUrl, Assert.Contains("organizationLink", properties));
                        }
                    }));

            var action = Assert.IsType<UpdateResponseAction>(platformEvent.Responder.ResponseAction);
            AssertAlert(action.View,
                ":white_check_mark: Got it! I’m going to work on creating that ticket now. I’ll send you a DM when I’m done.",
                "Request Accepted");
        }
    }

    static void AssertModalCommon(TicketingIntegration ticketing, [NotNull] ViewUpdatePayload? payload, int conversationId, string? zdOrganizationUrl)
    {
        Assert.NotNull(payload);
        Assert.Equal(InteractionCallbackInfo.For<CreateZendeskTicketFormModal>($"{ticketing.Integration.Id}"),
            Parse(payload.CallbackId));

        Assert.Equal("Create Zendesk Ticket", payload.Title);
        Assert.Equal("Cancel", payload.Close?.Text);
        Assert.Equal("Create", payload.Submit?.Text);
        Assert.Equal($"{conversationId}|{zdOrganizationUrl}", payload.PrivateMetadata);
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
