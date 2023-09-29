using Abbot.Common.TestHelpers;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Services;
using Serious.BlockKit.LayoutBlocks;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Payloads;
using Serious.TestHelpers.CultureAware;

public class AnnouncementHandlerTests
{
    public class TheOnMessageInteractionAsyncMethod
    {
        [Fact]
        public async Task OnlyShowsAnnouncementModalWhenUserIsAgent()
        {
            var env = TestEnvironment.Create();
            var admin = await env.CreateAdminMemberAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var room = await env.CreateRoomAsync(botIsMember: true);
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = room.PlatformRoomId,
                IsMember = true
            });
            var interactionInfo =
                new MessageInteractionInfo(new MessageActionPayload
                {
                    TriggerId = "TriggerId",
                },
                    $"{room.PlatformRoomId}|MessageId|",
                    new InteractionCallbackInfo("AnnouncementHandler"));
            var message = env.CreatePlatformMessage(room, interactionInfo: interactionInfo, from: admin);
            var handler = env.Activate<AnnouncementHandler>();

            await handler.OnMessageInteractionAsync(message);

            Assert.Empty(env.Responder.OpenModals);
        }

        [Fact]
        public async Task DisplaysAnnouncementModalWhenBotIsMember()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var room = await env.CreateRoomAsync(botIsMember: true);
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = room.PlatformRoomId,
                IsMember = true
            });
            var interactionInfo =
                new MessageInteractionInfo(new MessageActionPayload
                {
                    TriggerId = "TriggerId",
                },
                $"{room.PlatformRoomId}|MessageId|",
                new InteractionCallbackInfo("AnnouncementHandler"));
            var message = env.CreatePlatformMessage(room, interactionInfo: interactionInfo, from: agent);
            var handler = env.Activate<AnnouncementHandler>();

            await handler.OnMessageInteractionAsync(message);

            var modal = env.Responder.OpenModals["TriggerId"];
            var blocks = modal.Blocks;
            Assert.Null(blocks.FindBlockById("date-picker-input"));
            Assert.Null(blocks.FindBlockById("time-picker-input"));
            Assert.Equal("Create Announcement", modal.Title);
            Assert.Equal("Send", modal.Submit);
        }

        [Fact]
        public async Task AllowsEditingAnnouncementWhenMessageAlreadyHasAnnouncement()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            env.Clock.Freeze();
            var scheduledDateUtc = env.Clock.UtcNow.AddDays(1);
            var scheduledDate = env.TestData.Member.ToTimeZone(scheduledDateUtc);
            var sourceRoom = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom1 = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom2 = await env.CreateRoomAsync(botIsMember: true);
            await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId",
                scheduledDateUtc,
                targetRoom1,
                targetRoom2);
            var interactionInfo =
                new MessageInteractionInfo(new MessageActionPayload
                {
                    TriggerId = "TriggerId",
                },
                    $"{sourceRoom.PlatformRoomId}|MessageId|",
                    new InteractionCallbackInfo("AnnouncementHandler"));
            var message = env.CreatePlatformMessage(sourceRoom, interactionInfo: interactionInfo, from: agent);
            var handler = env.Activate<AnnouncementHandler>();

            await handler.OnMessageInteractionAsync(message);

            var modal = env.Responder.OpenModals["TriggerId"];
            var blocks = modal.Blocks;
            var datePicker = blocks.FindInputElementByBlockId<DatePicker>("date-picker-input");
            Assert.NotNull(datePicker);
            Assert.Equal(scheduledDate.ToString("yyyy-MM-dd"), datePicker.InitialDate);
            var timePicker = blocks.FindInputElementByBlockId<TimePicker>("time-picker-input");
            Assert.NotNull(timePicker);
            Assert.Equal(scheduledDate.ToString("hh:mm"), timePicker.InitialTime);
            var channelSelect = blocks.FindInputElementByBlockId<SelectMenu>("channels-input");
            var mesm = Assert.IsType<MultiExternalSelectMenu>(channelSelect);
            Assert.Equal("channels", mesm.ActionId);
            Assert.Equal(1, mesm.MinQueryLength);
            Assert.Equal(
                new Option[]
                {
                        new(targetRoom1.Name!, targetRoom1.PlatformRoomId),
                        new(targetRoom2.Name!, targetRoom2.PlatformRoomId),
                },
                mesm.InitialOptions);
            Assert.Equal("Edit Announcement", modal.Title);
            Assert.Equal("Schedule", modal.Submit);
        }

        [Fact]
        public async Task ShowsErrorMessageWhenEditingAnnouncementButAnnouncementScheduledToStartWithinFiveMinutes()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            env.Clock.Freeze();
            var scheduledDateUtc = env.Clock.UtcNow.AddMinutes(4).AddSeconds(59);
            var sourceRoom = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom1 = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom2 = await env.CreateRoomAsync(botIsMember: true);
            await env.CreateAnnouncementAsync(
                sourceRoom,
                "MessageId",
                scheduledDateUtc,
                targetRoom1,
                targetRoom2);
            var interactionInfo =
                new MessageInteractionInfo(new MessageActionPayload
                {
                    TriggerId = "TriggerId",
                },
                    $"{sourceRoom.PlatformRoomId}|MessageId|",
                    new InteractionCallbackInfo("AnnouncementHandler"));
            var message = env.CreatePlatformMessage(sourceRoom, interactionInfo: interactionInfo, from: agent);
            var handler = env.Activate<AnnouncementHandler>();

            await handler.OnMessageInteractionAsync(message);

            var modal = env.Responder.OpenModals["TriggerId"];
            Assert.Equal("Cannot Edit Announcement", modal.Title);
            Assert.Null(modal.Submit);
            var blocks = modal.Blocks;
            var section = Assert.IsType<Section>(blocks.Single());
            Assert.NotNull(section.Text);
            Assert.Equal("It’s too late to edit this announcement. The announcement is scheduled to be sent within 5 minutes.", section.Text.Text);
        }

        [Fact]
        public async Task DisplaysErrorModalWhenBotIsNotMember()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var room = await env.CreateRoomAsync();
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = room.PlatformRoomId,
                IsMember = false
            });
            await env.Db.SaveChangesAsync();
            var interactionInfo =
                new MessageInteractionInfo(new MessageActionPayload
                {
                    TriggerId = "TriggerId",
                },
                    $"{room.PlatformRoomId}|MessageId|",
                    new InteractionCallbackInfo("AnnouncementHandler"));
            var message = env.CreatePlatformMessage(room, interactionInfo: interactionInfo, from: agent);
            var handler = env.Activate<AnnouncementHandler>();

            await handler.OnMessageInteractionAsync(message);

            var modal = env.Responder.OpenModals["TriggerId"];
            var section = Assert.IsType<Section>(Assert.Single(modal.Blocks));
            Assert.NotNull(section.Text);
            Assert.Equal("In order to create an announcement from this message, test-abbot must be a member of this channel.",
                section.Text.Text);
            Assert.Equal("Create Announcement", modal.Title);
            Assert.Equal("Close", modal.Submit);
        }
    }

    public class TheOnInteractionAsyncMethod
    {
        [Fact]
        public async Task WithLaterSelectedShowsOptionsToScheduleLater()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var room = await env.CreateRoomAsync();
            room.BotIsMember = true;
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = room.PlatformRoomId,
                IsMember = true
            });
            await env.Db.SaveChangesAsync();
            var handler = env.Activate<AnnouncementHandler>();
            var payload = new ViewBlockActionsPayload
            {
                Actions = new[]
                {
                    new RadioButtonGroup
                    {
                        ActionId = AnnouncementHandler.ActionIds.WhenOptions,
                        SelectedOption = new CheckOption { Value = "later" },
                    },
                },
                View = new ModalView
                {
                    Id = "ViewId",
                    PrivateMetadata = $"{room.PlatformRoomId}|MessageId|"
                }
            };
            var viewContext = env.CreateFakeViewContext<IViewBlockActionsPayload>(payload, handler, from: agent);

            await handler.OnInteractionAsync(viewContext);

            var modal = env.Responder.OpenModals["ViewId"];
            var blocks = modal.Blocks;
            var dateInput = Assert.IsType<Input>(blocks.FindBlockById("date-picker-input"));
            var timeInput = Assert.IsType<Input>(blocks.FindBlockById("time-picker-input"));
            Assert.Equal("Choose a date to broadcast announcement", dateInput.Label);
            Assert.Equal("Choose a time to broadcast announcement", timeInput.Label);
            Assert.IsType<DatePicker>(dateInput.Element);
            Assert.IsType<TimePicker>(timeInput.Element);
            Assert.Equal("Create Announcement", modal.Title);
            Assert.Equal("Schedule", modal.Submit);
        }
    }

    public class TheOnSubmissionAsyncMethod
    {
        [Theory]
        [InlineData(false, nameof(AllRoomsAnnouncementTarget), false)]
        [InlineData(false, nameof(SelectedRoomsAnnouncementTarget), true)]
        [InlineData(true, nameof(AllRoomsAnnouncementTarget), false)]
        [InlineData(true, nameof(SelectedRoomsAnnouncementTarget), true)]
        public async Task SchedulesAnnouncementReminderAndSendsDirectMessageWithRescheduleInstructionsAndUpdatesDirectMessage(bool sendAsBot, string channelOption, bool expectSelectedRooms)
        {
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(new DateTime(2020, 1, 1, 12, 0, 0));
            var agent = await env.CreateMemberInAgentRoleAsync(timeZoneId: "America/New_York");
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var sourceRoom = await env.CreateRoomAsync();
            sourceRoom.BotIsMember = true;
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var targetRoom3 = await env.CreateRoomAsync();
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = sourceRoom.PlatformRoomId,
                Name = sourceRoom.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom1.PlatformRoomId,
                Name = targetRoom1.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom2.PlatformRoomId,
                Name = targetRoom2.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom3.PlatformRoomId,
                Name = targetRoom3.Name!,
                IsMember = true
            });
            var handler = env.Activate<AnnouncementHandler>();
            var selectedRooms = new[]
            {
                targetRoom1.PlatformRoomId, targetRoom2.PlatformRoomId, targetRoom3.PlatformRoomId
            };
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    Id = "ViewId",
                    PrivateMetadata = $"{sourceRoom.PlatformRoomId}|1657931110.388039|https://example.com/update-message",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [AnnouncementHandler.Blocks.SendAsBot] = new()
                        {
                            [AnnouncementHandler.ActionIds.SendAsBot] = new CheckboxGroup
                            {
                                SelectedOptions = sendAsBot
                                    ? new[] { new CheckOption { Value = "true" } }
                                    : Array.Empty<CheckOption>(),
                            },
                        },
                        ["when-radio-input"] = new()
                        {
                            ["when-radio"] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = "later" }
                            }
                        },
                        ["date-picker-input"] = new()
                        {
                            ["date-picker"] = new DatePicker { Value = "2022-01-23" }
                        },
                        ["time-picker-input"] = new()
                        {
                            ["time-picker"] = new TimePicker { Value = "23:30" }
                        },
                        [AnnouncementHandler.Blocks.ChannelOptionsInput] = new()
                        {
                            [AnnouncementHandler.ActionIds.ChannelOptions] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = channelOption },
                            },
                        },
                        ["channels-input"] = new()
                        {
                            ["channels"] = new MultiExternalSelectMenu
                            {
                                SelectedOptions = selectedRooms.Select(r => new Option(r, r)).ToList(),
                            },
                        }
                    })
                }
            };
            var viewContext = env.CreateFakeViewContext<IViewSubmissionPayload>(payload, handler, from: agent);

            await handler.OnSubmissionAsync(viewContext);

            var announcement = await env.Db.Announcements.SingleEntityOrDefaultAsync();
            Assert.NotNull(announcement);
            Assert.Equal("1657931110.388039", announcement.SourceMessageId);
            Assert.Equal(
                expectSelectedRooms ? selectedRooms : Array.Empty<string>(),
                announcement.Messages.Select(m => m.Room.PlatformRoomId));
            Assert.Equal(sendAsBot, announcement.SendAsBot);
            var sentMessages = env.Responder.SentMessages.ToList(); // We really should have an UpdatedMessages property, but later. Always later.
            Assert.Equal(2, sentMessages.Count());
            var sent = sentMessages[0];
            Assert.Equal($":mega: Alright. I’ve scheduled this announcement to be sent *January 23, 2022 at 11:30 PM*.", sent.Text);
            var updated = Assert.IsType<RichActivity>(sentMessages[1]);
            Assert.Equal(":wave: Hey! Thanks for the message. What would you like to do next?", updated.Text);
            var lastBlockText = Assert.IsType<MrkdwnText>(Assert.IsType<Context>(updated.Blocks.Last()).Elements[0]);
            Assert.Equal("You chose to create an announcement.", lastBlockText.Text);
            var richMessage = Assert.IsType<RichActivity>(sent);
            var actions = richMessage.Blocks.FindBlockById<Actions>("actions-block");
            Assert.NotNull(actions);
            var button = Assert.IsType<ButtonElement>(actions.Elements.Single());
            Assert.Equal(InteractionCallbackInfo.For<AnnouncementHandler>().ToString(), button.ActionId);
            var privateMetadata = AnnouncementHandler.PrivateMetadata.Parse(button.Value);
            Assert.NotNull(privateMetadata);
            Assert.Equal("1657931110.388039", privateMetadata.MessageId);
            Assert.Equal(sourceRoom.PlatformRoomId, privateMetadata.Channel);
            var (job, state) = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            Assert.Equal(nameof(AnnouncementSender.SendReminderAsync), job.Method.Name);
            Assert.Equal(announcement.Id, job.Args[0]);
            var scheduleState = Assert.IsType<ScheduledState>(state);
            Assert.Equal(new DateTime(2022, 01, 24, 3, 30, 0, DateTimeKind.Utc), scheduleState.EnqueueAt);
        }

        [Theory]
        [InlineData(false, nameof(AllRoomsAnnouncementTarget), false)]
        [InlineData(false, nameof(SelectedRoomsAnnouncementTarget), true)]
        [InlineData(true, nameof(AllRoomsAnnouncementTarget), false)]
        [InlineData(true, nameof(SelectedRoomsAnnouncementTarget), true)]
        public async Task SchedulesAnnouncementImmediately(bool sendAsBot, string channelOption, bool expectSelectedRooms)
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync(timeZoneId: "America/New_York");
            var now = env.Clock.Freeze();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var sourceRoom = await env.CreateRoomAsync();
            sourceRoom.BotIsMember = true;
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var targetRoom3 = await env.CreateRoomAsync();
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = sourceRoom.PlatformRoomId,
                Name = sourceRoom.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom1.PlatformRoomId,
                Name = targetRoom1.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom2.PlatformRoomId,
                Name = targetRoom2.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom3.PlatformRoomId,
                Name = targetRoom3.Name!,
                IsMember = true
            });
            var handler = env.Activate<AnnouncementHandler>();
            var selectedRooms = new[]
            {
                targetRoom1.PlatformRoomId, targetRoom2.PlatformRoomId, targetRoom3.PlatformRoomId
            };
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    Id = "ViewId",
                    PrivateMetadata = $"{sourceRoom.PlatformRoomId}|1657931110.388039|https://example.com/update-message",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [AnnouncementHandler.Blocks.SendAsBot] = new()
                        {
                            [AnnouncementHandler.ActionIds.SendAsBot] = new CheckboxGroup
                            {
                                SelectedOptions = sendAsBot
                                    ? new[] { new CheckOption { Value = "true" } }
                                    : Array.Empty<CheckOption>(),
                            },
                        },
                        ["when-radio-input"] = new()
                        {
                            ["when-radio"] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = "immediately" }
                            }
                        },
                        [AnnouncementHandler.Blocks.ChannelOptionsInput] = new()
                        {
                            [AnnouncementHandler.ActionIds.ChannelOptions] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = channelOption },
                            },
                        },
                        ["channels-input"] = new()
                        {
                            ["channels"] = new MultiExternalSelectMenu
                            {
                                SelectedOptions = selectedRooms.Select(r => new Option(r, r)).ToList(),
                            },
                        }
                    })
                }
            };
            var viewContext = env.CreateFakeViewContext<IViewSubmissionPayload>(payload, handler, from: agent);

            await handler.OnSubmissionAsync(viewContext);

            var announcement = await env.Db.Announcements.SingleEntityOrDefaultAsync();
            Assert.NotNull(announcement);
            Assert.Null(announcement.ScheduledDateUtc);
            Assert.Equal("1657931110.388039", announcement.SourceMessageId);
            Assert.Equal(
                expectSelectedRooms ? selectedRooms : Array.Empty<string>(),
                announcement.Messages.Select(m => m.Room.PlatformRoomId));
            Assert.Equal(sendAsBot, announcement.SendAsBot);
            var sent = Assert.Single(env.Responder.SentMessages);
            var updated = Assert.IsType<RichActivity>(sent);
            Assert.Equal(":wave: Hey! Thanks for the message. What would you like to do next?", updated.Text);
            var lastBlockText = Assert.IsType<MrkdwnText>(Assert.IsType<Context>(updated.Blocks.Last()).Elements[0]);
            Assert.Equal("You chose to create an announcement.", lastBlockText.Text);
            var (job, state) = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            Assert.Equal(nameof(AnnouncementSender.BroadcastAnnouncementAsync), job.Method.Name);
            Assert.Equal(announcement.Id, job.Args[0]);
            var scheduleState = Assert.IsType<ScheduledState>(state);
            Assert.Equal(now, scheduleState.EnqueueAt);
        }

        [Theory]
        [InlineData(false, nameof(AllRoomsAnnouncementTarget), false)]
        [InlineData(false, nameof(SelectedRoomsAnnouncementTarget), true)]
        [InlineData(true, nameof(AllRoomsAnnouncementTarget), false)]
        [InlineData(true, nameof(SelectedRoomsAnnouncementTarget), true)]
        public async Task WithExistingAnnouncementUpdatesExistingAnnouncement(bool sendAsBot, string channelOption, bool expectSelectedRooms)
        {
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(new DateTime(2022, 7, 19, 12, 0, 0));
            var agent = await env.CreateMemberInAgentRoleAsync(timeZoneId: "America/New_York");
            var scheduledDateUtc = env.Clock.UtcNow.AddDays(1);
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var sourceRoom = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom1 = await env.CreateRoomAsync();
            var targetRoom2 = await env.CreateRoomAsync();
            var targetRoom3 = await env.CreateRoomAsync();
            var existingAnnouncement = await env.CreateAnnouncementAsync(
                sourceRoom,
                "1657924271.517229",
                scheduledDateUtc,
                targetRoom1,
                targetRoom2,
                targetRoom3);
            Assert.Equal(3, existingAnnouncement.Messages.Count);
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = sourceRoom.PlatformRoomId,
                Name = sourceRoom.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom1.PlatformRoomId,
                Name = targetRoom1.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom2.PlatformRoomId,
                Name = targetRoom2.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom3.PlatformRoomId,
                Name = targetRoom3.Name!,
                IsMember = true
            });
            var handler = env.Activate<AnnouncementHandler>();
            var selectedRooms = new[]
            {
                // Unselect the third room.
                targetRoom1.PlatformRoomId, targetRoom2.PlatformRoomId
            };
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    Id = "ViewId",
                    PrivateMetadata = $"{sourceRoom.PlatformRoomId}|1657924271.517229|",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [AnnouncementHandler.Blocks.SendAsBot] = new()
                        {
                            [AnnouncementHandler.ActionIds.SendAsBot] = new CheckboxGroup
                            {
                                SelectedOptions = sendAsBot
                                    ? new[] { new CheckOption { Value = "true" } }
                                    : Array.Empty<CheckOption>(),
                            },
                        },
                        ["when-radio-input"] = new()
                        {
                            ["when-radio"] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = "later" }
                            }
                        },
                        ["date-picker-input"] = new()
                        {
                            ["date-picker"] = new DatePicker { Value = "2022-07-21" }
                        },
                        ["time-picker-input"] = new()
                        {
                            ["time-picker"] = new TimePicker { Value = "03:30" /* America/New_York => 07:30 UTC */ }
                        },
                        [AnnouncementHandler.Blocks.ChannelOptionsInput] = new()
                        {
                            [AnnouncementHandler.ActionIds.ChannelOptions] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = channelOption },
                            },
                        },
                        ["channels-input"] = new()
                        {
                            ["channels"] = new MultiExternalSelectMenu
                            {
                                SelectedOptions = selectedRooms.Select(r => new Option(r, r)).ToList(),
                            },
                        }
                    })
                }
            };
            var viewContext = env.CreateFakeViewContext<IViewSubmissionPayload>(payload, handler, from: agent);

            await handler.OnSubmissionAsync(viewContext);

            var announcement = await env.Db.Announcements.SingleEntityOrDefaultAsync().Require();
            Assert.NotNull(announcement.ScheduledDateUtc);
            Assert.Equal("1657924271.517229", announcement.SourceMessageId);
            Assert.Equal(existingAnnouncement.Id, announcement.Id);
            Assert.Equal("07:30", announcement.ScheduledDateUtc?.ToString("hh:mm"));
            Assert.Equal(
                expectSelectedRooms ? selectedRooms : Array.Empty<string>(),
                announcement.Messages.Select(m => m.Room.PlatformRoomId));
            Assert.Equal(sendAsBot, announcement.SendAsBot);
            var sent = Assert.Single(env.Responder.SentMessages);
            Assert.Equal(":mega: Alright. I’ve rescheduled this announcement to be sent *July 21, 2022 at 3:30 AM*.", sent.Text);
            var richMessage = Assert.IsType<RichActivity>(sent);
            var actions = richMessage.Blocks.FindBlockById<Actions>("actions-block");
            Assert.NotNull(actions);
            var button = Assert.IsType<ButtonElement>(actions.Elements.Single());
            Assert.Equal(InteractionCallbackInfo.For<AnnouncementHandler>().ToString(), button.ActionId);
            var privateMetadata = AnnouncementHandler.PrivateMetadata.Parse(button.Value);
            Assert.NotNull(privateMetadata);
            Assert.Equal("1657924271.517229", privateMetadata.MessageId);
            Assert.Equal(sourceRoom.PlatformRoomId, privateMetadata.Channel);
        }

        [Fact]
        public async Task ClosesAllWindowsWhenCloseClicked()
        {
            var env = TestEnvironment.Create();
            var agent = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var room = await env.CreateRoomAsync();
            room.BotIsMember = true;
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = room.PlatformRoomId,
                IsMember = true
            });
            await env.Db.SaveChangesAsync();
            var handler = env.Activate<AnnouncementHandler>();
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    Id = "ViewId",
                    Submit = "Close",
                    PrivateMetadata = $"{room.PlatformRoomId}|1657921270.106779"
                }
            };
            var viewContext = env.CreateFakeViewContext<IViewSubmissionPayload>(payload, handler, from: agent);

            await handler.OnSubmissionAsync(viewContext);

            Assert.NotNull(env.Responder.ResponseAction);
            Assert.Equal("clear", env.Responder.ResponseAction.Action);
        }

        [Theory]
        [UseCulture("en-US")]
        // TestMember is in Vancouver; 2022-12-01 00:00 UTC
        [InlineData("2022-11-30", "16:00")] // Scheduled for exactly now
        [InlineData("2022-11-30", "17:00")] // Scheduled in an hour
        [InlineData("2022-12-01", "09:00")] // Scheduled tomorrow
        public async Task ShowsValidationErrorMessageWhenSelectedRoomDoesNotHaveAbbot(string date, string time)
        {
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(new DateTime(2022, 12, 1));
            var agent = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var sourceRoom = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom1 = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom2 = await env.CreateRoomAsync(botIsMember: false);
            var targetRoom3 = await env.CreateRoomAsync(botIsMember: false);
            var selectedRooms = new[] { targetRoom1.PlatformRoomId, targetRoom2.PlatformRoomId, targetRoom3.PlatformRoomId };
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = sourceRoom.PlatformRoomId,
                Name = sourceRoom.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom1.PlatformRoomId,
                Name = targetRoom1.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom2.PlatformRoomId,
                Name = targetRoom2.Name!,
                IsMember = false
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom3.PlatformRoomId,
                Name = targetRoom3.Name!,
                IsMember = false
            });
            await env.Db.SaveChangesAsync();
            var handler = env.Activate<AnnouncementHandler>();
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    Id = "ViewId",
                    PrivateMetadata = $"{sourceRoom.PlatformRoomId}|1657921270.106779|",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [AnnouncementHandler.Blocks.SendAsBot] = new()
                        {
                            [AnnouncementHandler.ActionIds.SendAsBot] = new CheckboxGroup
                            {
                                SelectedOptions = Array.Empty<CheckOption>(),
                            },
                        },
                        ["when-radio-input"] =
                            new()
                            {
                                ["when-radio"] = new RadioButtonGroup
                                {
                                    SelectedOption = new CheckOption { Value = "later" }
                                }
                            },
                        ["date-picker-input"] =
                            new()
                            {
                                { "date-picker", new DatePicker { Value = date } }
                            },
                        ["time-picker-input"] =
                            new()
                            {
                                { "time-picker", new TimePicker { Value = time } }
                            },
                        [AnnouncementHandler.Blocks.ChannelOptionsInput] = new()
                        {
                            [AnnouncementHandler.ActionIds.ChannelOptions] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = nameof(SelectedRoomsAnnouncementTarget) },
                            },
                        },
                        ["channels-input"] = new()
                        {
                            ["channels"] = new MultiExternalSelectMenu
                            {
                                SelectedOptions = selectedRooms.Select(r => new Option(r, r)).ToList(),
                            },
                        }
                    })
                }
            };
            var viewContext = env.CreateFakeViewContext<IViewSubmissionPayload>(payload, handler, from: agent);

            await handler.OnSubmissionAsync(viewContext);

            Assert.NotNull(env.Responder.ValidationErrors);
            var errorMessage = Assert.Contains("channels-input", env.Responder.ValidationErrors);
            Assert.Equal(errorMessage, $"In order to send an announcement to a room, Abbot must be a member of that room: Abbot is not a member of the following rooms: {targetRoom2.Name} and {targetRoom3.Name}.");

            Assert.Empty(await env.Db.Announcements.ToListAsync());
        }

        [Theory]
        [UseCulture("en-US")]
        [InlineData("2022-12-02", null, "time-picker-input", "Please specify a time in the future.")]
        [InlineData(null, "01:00", "date-picker-input", "Please specify a date in the future.")]
        // TestMember is in Vancouver; 2022-12-01 00:00 UTC
        [InlineData("2022-11-30", "16:00", "date-picker-input", "Please specify a date and time in the future.")]
        public async Task ShowsValidationErrorMessageAndDoesNotScheduleForInvalidLaterDateTime(
            string? date, string? time, string errorInput, string expectedErrorMessage)
        {
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(new DateTime(2022, 12, 1, 0, 0, 1));

            var agent = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var apiToken = organization.ApiToken!.Reveal();
            var sourceRoom = await env.CreateRoomAsync(botIsMember: true);
            var targetRoom = await env.CreateRoomAsync(botIsMember: true);
            var selectedRooms = new[] { targetRoom.PlatformRoomId };
            var conversationsApi = env.SlackApi.Conversations;
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = sourceRoom.PlatformRoomId,
                Name = sourceRoom.Name!,
                IsMember = true
            });
            conversationsApi.AddConversationInfoResponse(apiToken, new ConversationInfo
            {
                Id = targetRoom.PlatformRoomId,
                Name = targetRoom.Name!,
                IsMember = true
            });
            await env.Db.SaveChangesAsync();
            var handler = env.Activate<AnnouncementHandler>();
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    Id = "ViewId",
                    PrivateMetadata = $"{sourceRoom.PlatformRoomId}|1657921270.106779|",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [AnnouncementHandler.Blocks.SendAsBot] = new()
                        {
                            [AnnouncementHandler.ActionIds.SendAsBot] = new CheckboxGroup
                            {
                                SelectedOptions = Array.Empty<CheckOption>(),
                            },
                        },
                        ["when-radio-input"] =
                            new()
                            {
                                ["when-radio"] = new RadioButtonGroup
                                {
                                    SelectedOption = new CheckOption { Value = "later" }
                                }
                            },
                        ["date-picker-input"] =
                            new()
                            {
                                { "date-picker", new DatePicker { Value = date } }
                            },
                        ["time-picker-input"] =
                            new()
                            {
                                { "time-picker", new TimePicker { Value = time } }
                            },
                        [AnnouncementHandler.Blocks.ChannelOptionsInput] = new()
                        {
                            [AnnouncementHandler.ActionIds.ChannelOptions] = new RadioButtonGroup
                            {
                                SelectedOption = new CheckOption { Value = nameof(SelectedRoomsAnnouncementTarget) },
                            },
                        },
                        ["channels-input"] = new()
                        {
                            ["channels"] = new MultiExternalSelectMenu
                            {
                                SelectedOptions = selectedRooms.Select(r => new Option(r, r)).ToList(),
                            },
                        }
                    })
                }
            };
            var viewContext = env.CreateFakeViewContext<IViewSubmissionPayload>(payload, handler, from: agent);

            await handler.OnSubmissionAsync(viewContext);

            Assert.NotNull(env.Responder.ValidationErrors);
            var errorMessage = Assert.Contains(errorInput, env.Responder.ValidationErrors);
            Assert.Equal(errorMessage, expectedErrorMessage);

            Assert.Empty(await env.Db.Announcements.ToListAsync());
        }
    }

    public class TheOnBlockSuggestionRequestAsyncMethod
    {
        [Theory]
        [InlineData("r")]
        [InlineData("#r")]
        public async Task ReturnsOptionsFromMatchingTrackedRooms(string value)
        {
            var env = TestEnvironment.Create();
            var rooms = new[]
            {
                await env.CreateRoomAsync(name: "room0", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "room1"),
                await env.CreateRoomAsync(name: "zoom2", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "room3", managedConversationsEnabled: true),
                await env.CreateRoomAsync(name: "room4", botIsMember: false),
            };

            var handler = env.Activate<AnnouncementHandler>();

            var platformEvent = env.CreateFakePlatformEvent(new BlockSuggestionPayload { Value = value });

            var result = await handler.OnBlockSuggestionRequestAsync(platformEvent);

            Assert.Equal(
                new Option[]
                {
                    new(rooms[0].Name!, rooms[0].PlatformRoomId),
                    new(rooms[3].Name!, rooms[3].PlatformRoomId),
                },
                Assert.IsType<OptionsBlockSuggestionsResponse>(result).Options);
        }
    }
}
