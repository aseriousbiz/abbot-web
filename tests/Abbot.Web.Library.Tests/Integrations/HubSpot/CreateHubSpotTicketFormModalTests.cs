using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Forms;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Security;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Serious.TestHelpers;
using Xunit;

public class CreateHubSpotTicketFormModalTests
{
    static InteractionCallbackInfo? Parse(string? callbackId) => CallbackInfo.TryParseAs<InteractionCallbackInfo>(callbackId, out var cb)
        ? cb
        : null;

    static async Task<TicketingIntegration> CreateIntegrationAsync(TestEnvironmentWithData env)
    {
        var integration = await env.Integrations.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.HubSpot);
        var settings = new HubSpotSettings
        {
        };
        return new(integration, settings);
    }

    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task RendersMainViewIfConversationExists()
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IMessageRenderer, MessageRenderer>()
                .Build();
            var ticketing = await CreateIntegrationAsync(env);

            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room, title: $"This title mentions {member.ToMention()}.");

            var modal = env.Activate<CreateHubSpotTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo.Id);

            // Assume the default form.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:subject"));
            var descriptionInput = Assert.IsType<Input>(payload.Blocks.FindBlockById("__field:content"));
            var descriptionTextInput = Assert.IsType<PlainTextInput>(descriptionInput.Element);
            Assert.Equal($"This title mentions {member.DisplayName}.", descriptionTextInput.InitialValue);
            Assert.NotNull(payload.Blocks.FindBlockById("__field:hs_ticket_priority"));
        }

        [Fact]
        public async Task RendersCustomFormIfOneIsEnabled()
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateIntegrationAsync(env);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization,
                SystemForms.CreateHubSpotTicket,
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
            var modal = env.Activate<CreateHubSpotTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo.Id);

            // Custom fields are present.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:example_field"));

            // No default fields
            Assert.Null(payload.Blocks.FindBlockById("__field:subject"));
            Assert.Null(payload.Blocks.FindBlockById("__field:content"));
            Assert.Null(payload.Blocks.FindBlockById("__field:hs_ticket_priority"));
        }

        [Fact]
        public async Task RendersDefaultFormIfCustomFormDisabled()
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateIntegrationAsync(env);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization,
                SystemForms.CreateHubSpotTicket,
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
            var modal = env.Activate<CreateHubSpotTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo.Id);

            // Custom fields are NOT present.
            Assert.Null(payload.Blocks.FindBlockById("__field:example_field"));

            // Default fields are present.
            Assert.NotNull(payload.Blocks.FindBlockById("__field:subject"));
            Assert.NotNull(payload.Blocks.FindBlockById("__field:content"));
            Assert.NotNull(payload.Blocks.FindBlockById("__field:hs_ticket_priority"));
        }

        [Theory]
        [InlineData(Roles.Administrator, "<https://app.hubspot.com/contacts/123/company/456|The Derek Zoolander Center for Kids Who Can't Read Good>")]
        [InlineData(Roles.Agent, "<https://app.hubspot.com/contacts/123/company/456|The Derek Zoolander Center for Kids Who Can't Read Good>")]
        [InlineData(null, "The Derek Zoolander Center for Kids Who Can't Read Good")]
        public async Task IncludesCompanyIfOneIsLinkedToTheRoom(string? roleName, string expectedCompanyDisplay)
        {
            var env = TestEnvironment.Create();
            await env.AddUserToRoleAsync(env.TestData.Member, roleName);
            var ticketing = await CreateIntegrationAsync(env);

            env.Clock.Freeze();
            var room = await env.CreateRoomAsync();
            var hsCompanyLink = new HubSpotCompanyLink(123, "456");
            await env.Rooms.CreateLinkAsync(
                room,
                RoomLinkType.HubSpotCompany,
                hsCompanyLink.ToString(),
                "The Derek Zoolander Center for Kids Who Can't Read Good",
                env.TestData.Member,
                env.Clock.UtcNow);
            var convo = await env.CreateConversationAsync(room);
            var modal = env.Activate<CreateHubSpotTicketFormModal>();

            var payload = await modal.CreateAsync(ticketing, convo, env.TestData.Member);
            AssertModalCommon(ticketing, payload, convo.Id, hsCompanyLink.ToString());

            var infoSection = payload.Blocks.FindBlockById<Section>("info_section");
            Assert.NotNull(infoSection);
            Assert.Equal(expectedCompanyDisplay, infoSection.Fields?.Last().Text);
        }
    }

    public class TheOnSubmissionAsyncMethod
    {
        [Fact]
        public async Task ShowsAlertIfConvoCannotBeFound()
        {
            var env = TestEnvironment.Create();
            var modal = env.Activate<CreateHubSpotTicketFormModal>();
            var platformEvent = new FakePlatformEvent<ViewSubmissionPayload>(
                new()
                {
                    View = new ModalView()
                    {
                        CallbackId = InteractionCallbackInfo.For<CreateHubSpotTicketFormModal>("246"),
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

        [Theory]
        [InlineData("", null)]
        [InlineData("|", null)]
        [InlineData("|https://app.hubspot.com/contacts/357/company/468", "https://app.hubspot.com/contacts/357/company/468")]
        public async Task EnqueuesTicketLinkIfConvoFound(string privateMetadataSuffix, string? expectedRoomIntegrationLink)
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITicketIntegrationService>(out var ticketService)
                .Build();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Forms.CreateFormAsync(env.TestData.Organization, SystemForms.CreateHubSpotTicket, FormEngine.SerializeFormDefinition(new FormDefinition()
            {
                Fields = {
                    new FormFieldDefinition
                    {
                        Id = "subject",
                        Title = "Subject",
                        Type = FormFieldType.SingleLineText,
                        Required = false,
                        InitialValue = "{{Conversation.Title}}",
                    },
                    new FormFieldDefinition()
                    {
                        Id = "hs_ticket_priority",
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
                    new FormFieldDefinition
                    {
                        Id = "a_custom_multi_select_field",
                        Title = "Areas",
                        Type = FormFieldType.MultiDropdownList,
                        Required = false,
                        Options =
                        {
                            new("Sales", "salse"),
                            new ("Billing", "billing"),
                            new ("Technical", "technical"),
                            new ("Other", "other"),
                        },
                        InitialValue = "sales,billing",
                    },
                    new FormFieldDefinition
                    {
                        Id = "a_custom_field",
                        Title = "Custom Field",
                        Type = FormFieldType.SingleLineText,
                        Required = false,
                    },
                    new FormFieldDefinition
                    {
                        Id = "a_fixed_field",
                        Title = "Fixed Field",
                        Type = FormFieldType.FixedValue,
                        Required = false,
                        InitialValue = "{{Conversation.Id}}",
                    }
                },
            }), enabled: true, env.TestData.Member);
            var modal = env.Activate<CreateHubSpotTicketFormModal>();
            var platformEvent = new FakePlatformEvent<ViewSubmissionPayload>(
                new()
                {
                    View = new ModalView()
                    {
                        CallbackId = InteractionCallbackInfo.For<CreateHubSpotTicketFormModal>("246"),
                        PrivateMetadata = $"{convo.Id}{privateMetadataSuffix}",
                        State = new(new Dictionary<string, Dictionary<string, IPayloadElement>>()
                        {
                            { "__field:subject", new Dictionary<string, IPayloadElement> { { "aaaa", new PlainTextInput { Value = "Subject" } } } },
                            { "__field:hs_ticket_priority", new Dictionary<string, IPayloadElement> { { "aaaa", new StaticSelectMenu { SelectedOption = new ("Normal", "normal") } }}},
                            { "__field:a_custom_multi_select_field", new Dictionary<string, IPayloadElement> { { "aaaa", new MultiStaticSelectMenu { SelectedOptions = new Option[] { new("Technical", "technical"), new("Billing", "billing") } } } } },
                            { "__field:a_custom_field", new Dictionary<string, IPayloadElement> { { "aaaa", new PlainTextInput { Value = "a_custom_value" } } } },
                        })
                    }
                },
                env.TestData.Member,
                env.TestData.Organization);

            var context = new ViewContext<ViewSubmissionPayload>(platformEvent, modal);

            await modal.OnSubmissionAsync(context);

            ticketService.Received()
                .EnqueueTicketLinkRequest<HubSpotSettings>(
                    new Id<Integration>(246),
                    convo,
                    context.FromMember,
                    Arg.Do<IReadOnlyDictionary<string, object?>>(properties => {
                        Assert.Equal("Subject", Assert.Contains("name", properties));
                        Assert.Equal("Description", Assert.Contains("description", properties));
                        Assert.Equal($"{convo.Id}", Assert.Contains("custom_field:1234", properties));
                        Assert.Equal("it's custom", Assert.Contains($"custom_field:{long.MaxValue}", properties));
                        Assert.Equal(expectedRoomIntegrationLink, Assert.Contains("roomIntegrationLink", properties));
                    }));

            var action = Assert.IsType<UpdateResponseAction>(platformEvent.Responder.ResponseAction);
            AssertAlert(action.View,
                ":white_check_mark: Got it! I’m going to work on creating that ticket now. I’ll send you a DM when I’m done.",
                "Request Accepted");
        }
    }

    static void AssertModalCommon(
        TicketingIntegration ticketing,
        [NotNull] ViewUpdatePayload? payload,
        int conversationId,
        string? companyUrl = null)
    {
        Assert.NotNull(payload);
        Assert.Equal(InteractionCallbackInfo.For<CreateHubSpotTicketFormModal>($"{ticketing.Integration.Id}"),
            Parse(payload.CallbackId));

        Assert.Equal("Create HubSpot Ticket", payload.Title);
        Assert.Equal("Cancel", payload.Close?.Text);
        Assert.Equal("Create", payload.Submit?.Text);
        Assert.Equal($"{conversationId}|{companyUrl}", payload.PrivateMetadata);
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
