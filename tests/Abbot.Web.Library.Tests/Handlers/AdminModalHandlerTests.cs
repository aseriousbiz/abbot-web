using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.PayloadHandlers;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Serious.TestHelpers.CultureAware;

public class AdminModalHandlerTests
{
    public class TheOnSubmissionAsyncMethod
    {
        [Fact]
        [UseCulture("en-US")]
        public async Task AddsMemberToTheAdministratorsRole()
        {
            var env = TestEnvironment.Create();
            var installer = await env.CreateAdminMemberAsync();
            var pinky = await env.CreateMemberAsync();
            var brain = await env.CreateMemberAsync();
            Assert.False(pinky.IsAdministrator());
            Assert.False(brain.IsAdministrator());
            var viewSubmissionPayload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AdminsInput"] = new()
                        {
                            ["AdminsSelectMenu"] = new UsersMultiSelectMenu
                            {
                                SelectedValues = new[]
                                {
                                    installer.User.PlatformUserId,
                                    pinky.User.PlatformUserId,
                                    brain.User.PlatformUserId,
                                }
                            }
                        }
                    })
                }
            };
            var handler = env.Activate<AdminModalHandler>();
            var viewContext = env.CreateFakeViewContext(viewSubmissionPayload, handler, from: installer);

            await handler.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(pinky);
            await env.ReloadAsync(brain);
            await env.ReloadAsync(installer);
            Assert.True(installer.IsAdministrator());
            Assert.True(pinky.IsAdministrator());
            Assert.True(brain.IsAdministrator());

            var sentMessage = env.Responder.SentMessages.Single();
            Assert.Equal($"{pinky.ToMention()} and {brain.ToMention()} are now in the Administrators role for this Abbot instance.", sentMessage.Text);
        }

        [Fact]
        public async Task RemovesMemberToTheAdministratorsRole()
        {
            var env = TestEnvironment.Create();
            var installer = await env.CreateAdminMemberAsync();
            var pinky = await env.CreateAdminMemberAsync();
            Assert.True(pinky.IsAdministrator());
            var viewSubmissionPayload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AdminsInput"] = new()
                        {
                            ["AdminsSelectMenu"] = new UsersMultiSelectMenu
                            {
                                SelectedValues = new[]
                                {
                                    installer.User.PlatformUserId,
                                }
                            }
                        }
                    })
                }
            };
            var handler = env.Activate<AdminModalHandler>();
            var viewContext = env.CreateFakeViewContext(viewSubmissionPayload, handler, from: installer);

            await handler.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(pinky);
            await env.ReloadAsync(installer);
            Assert.True(installer.IsAdministrator());
            Assert.False(pinky.IsAdministrator());

            var sentMessage = env.Responder.SentMessages.Single();
            Assert.Equal($"{pinky.ToMention()} is no longer in the Administrators role for this Abbot instance.", sentMessage.Text);
        }

        [Fact]
        public async Task MakesNoChangesWhenNoChanges()
        {
            var env = TestEnvironment.Create();
            var installer = await env.CreateAdminMemberAsync();
            var viewSubmissionPayload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AdminsInput"] = new()
                        {
                            ["AdminsSelectMenu"] = new UsersMultiSelectMenu
                            {
                                SelectedValues = new[]
                                {
                                    installer.User.PlatformUserId,
                                }
                            }
                        }
                    })
                }
            };
            var handler = env.Activate<AdminModalHandler>();
            var viewContext = env.CreateFakeViewContext(viewSubmissionPayload, handler, from: installer);

            await handler.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(installer);
            Assert.True(installer.IsAdministrator());

            var sentMessage = env.Responder.SentMessages.Single();
            Assert.Equal("No changes were made.", sentMessage.Text);
        }

        [Fact]
        public async Task SyncsMembershipToTheAdministratorsRole()
        {
            var env = TestEnvironment.Create();
            var installer = await env.CreateAdminMemberAsync();
            var pinky = await env.CreateMemberAsync();
            var brain = await env.CreateAdminMemberAsync();
            Assert.False(pinky.IsAdministrator());
            Assert.True(brain.IsAdministrator());
            var viewSubmissionPayload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AdminsInput"] = new()
                        {
                            ["AdminsSelectMenu"] = new UsersMultiSelectMenu
                            {
                                SelectedValues = new[]
                                {
                                    installer.User.PlatformUserId,
                                    pinky.User.PlatformUserId,
                                }
                            }
                        }
                    })
                }
            };
            var handler = env.Activate<AdminModalHandler>();
            var viewContext = env.CreateFakeViewContext(viewSubmissionPayload, handler, from: installer);

            await handler.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(pinky);
            await env.ReloadAsync(brain);
            await env.ReloadAsync(installer);
            Assert.True(installer.IsAdministrator());
            Assert.True(pinky.IsAdministrator());
            Assert.False(brain.IsAdministrator());

            var sentMessage = env.Responder.SentMessages.Single();
            Assert.Equal($"{pinky.ToMention()} is now in the Administrators role and {brain.ToMention()} is no longer in the Administrators role for this Abbot instance.", sentMessage.Text);
        }

        [Fact]
        public async Task ShowsValidationErrorWhenRemovingSelf()
        {
            var env = TestEnvironment.Create();
            var installer = await env.CreateAdminMemberAsync();
            var pinky = await env.CreateMemberAsync();
            var brain = await env.CreateMemberAsync();
            Assert.False(pinky.IsAdministrator());
            Assert.False(brain.IsAdministrator());
            var viewSubmissionPayload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AdminsInput"] = new()
                        {
                            ["AdminsSelectMenu"] = new UsersMultiSelectMenu
                            {
                                SelectedValues = new[]
                                {
                                    pinky.User.PlatformUserId,
                                    brain.User.PlatformUserId,
                                }
                            }
                        }
                    })
                }
            };
            var handler = env.Activate<AdminModalHandler>();
            var viewContext = env.CreateFakeViewContext(viewSubmissionPayload, handler, from: installer);

            await handler.OnSubmissionAsync(viewContext);

            Assert.NotNull(env.Responder.ValidationErrors);
            Assert.True(env.Responder.ValidationErrors.TryGetValue(AdminModalHandler.BlockIds.AdminsInput, out var validationErrors));
            Assert.Equal("Please do not remove yourself.", validationErrors);
            await env.ReloadAsync(pinky);
            await env.ReloadAsync(brain);

            // Don't save anything when an error occurs.
            Assert.False(pinky.IsAdministrator());
            Assert.False(brain.IsAdministrator());
        }

        [Theory]
        [InlineData(null, "test-abbot")]
        [InlineData("Test Abbot", "Test Abbot")]
        public async Task ShowsValidationErrorWhenAddingBotAndAbbot(string? botName, string expectedAbbotDisplay)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.BotName = botName;
            var installer = await env.CreateAdminMemberAsync();
            var randomBot = await env.CreateMemberAsync();
            var pinky = await env.CreateMemberAsync();
            var brain = await env.CreateMemberAsync();
            randomBot.User.DisplayName = "Rando Bot";
            randomBot.User.IsBot = true;
            await env.Db.SaveChangesAsync();
            Assert.False(pinky.IsAdministrator());
            Assert.False(brain.IsAdministrator());
            var viewSubmissionPayload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AdminsInput"] = new()
                        {
                            ["AdminsSelectMenu"] = new UsersMultiSelectMenu
                            {
                                SelectedValues = new[]
                                {
                                    randomBot.User.PlatformUserId,
                                    installer.User.PlatformUserId,
                                    organization.PlatformBotUserId!,
                                    pinky.User.PlatformUserId,
                                    brain.User.PlatformUserId
                                }
                            }
                        }
                    })
                }
            };
            var handler = env.Activate<AdminModalHandler>();
            var viewContext = env.CreateFakeViewContext(viewSubmissionPayload, handler, from: installer);

            await handler.OnSubmissionAsync(viewContext);

            Assert.NotNull(env.Responder.ValidationErrors);
            Assert.True(env.Responder.ValidationErrors.TryGetValue(AdminModalHandler.BlockIds.AdminsInput, out var validationErrors));
            Assert.Equal($"Rando Bot is a bot and cannot be an Administrator. {expectedAbbotDisplay} is willing, but unable to be an Administrator.", validationErrors);
            await env.ReloadAsync(pinky);
            await env.ReloadAsync(brain);

            // Don't save anything when an error occurs.
            Assert.False(pinky.IsAdministrator());
            Assert.False(brain.IsAdministrator());
        }
    }

    public class TheCreateAdministratorsModalMethod
    {
        [Fact]
        public void PopulatesInitialUsers()
        {
            var existing = new List<Member>
            {
                new() { User = new User { PlatformUserId = "U00001" } },
                new() { User = new User { PlatformUserId = "U00002" } }
            };
            var modal = AdminModalHandler.CreateAdministratorsModal(existing);

            var block = modal.Blocks.FindBlockById(AdminModalHandler.BlockIds.AdminsInput);
            var input = Assert.IsType<Input>(block);
            var usersMultiSelect = Assert.IsType<UsersMultiSelectMenu>(input.Element);
            Assert.Collection(usersMultiSelect.InitialUsers,
                u => Assert.Equal("U00001", u),
                u => Assert.Equal("U00002", u));
        }
    }
}
