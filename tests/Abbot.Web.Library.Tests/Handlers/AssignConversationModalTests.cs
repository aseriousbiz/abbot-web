using System.Collections.Generic;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Xunit;

public class AssignConversationModalTests
{
    public class TheOnSubmissionAsyncMethod
    {
        [Fact]
        public async Task AssignsSelectedAgentToConversationAndSendsDm()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = env.TestData.Conversation;
            var agent = env.TestData.Agent;
            var actor = env.TestData.Member;
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    PrivateMetadata = $"{conversation.Id}",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AgentsInput"] = new()
                        {
                            ["AgentsSelectMenu"] = new StaticSelectMenu
                            {
                                SelectedOption = new Option("", $"{agent.Id}")
                            }
                        }
                    })
                }
            };
            var modal = env.Activate<AssignConversationModal>();
            var viewContext = env.CreateFakeViewContext(payload, modal, actor);

            await modal.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(conversation);
            var assigned = Assert.Single(conversation.Assignees);
            Assert.Equal(env.TestData.Agent.Id, assigned.Id);
            var message = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal(agent.User.PlatformUserId, message.Channel);
            Assert.Equal($"You have been assigned to a conversation by {actor.DisplayName}.", message.Text);
            Assert.NotNull(message.Blocks);
            Assert.Collection(message.Blocks,
                b => Assert.Equal($"You have been assigned to a conversation by {actor.ToMention()}.", Assert.IsType<Section>(b).Text?.Text),
                b => Assert.Equal("https://testorg.example.com/archives/Croom/p11110006", Assert.IsType<ButtonElement>(Assert.Single(Assert.IsType<Actions>(b).Elements)).Url?.ToString()));
        }

        [Fact]
        public async Task DoesNotSendDmWhenAssignmentUnchanged()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = env.TestData.Conversation;
            var agent = env.TestData.Agent;
            conversation.Assignees.Add(agent);
            await env.Db.SaveChangesAsync();
            var actor = env.TestData.Member;
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    PrivateMetadata = $"{conversation.Id}",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AgentsInput"] = new()
                        {
                            ["AgentsSelectMenu"] = new StaticSelectMenu
                            {
                                SelectedOption = new Option("", $"{agent.Id}")
                            }
                        }
                    })
                }
            };
            var modal = env.Activate<AssignConversationModal>();
            var viewContext = env.CreateFakeViewContext(payload, modal, actor);

            await modal.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(conversation);
            var assigned = Assert.Single(conversation.Assignees);
            Assert.Equal(env.TestData.Agent.Id, assigned.Id);
            Assert.Empty(env.SlackApi.PostedMessages);
        }

        [Fact]
        public async Task AssignsSelfToConversationAndDoesNotSendDm()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = env.TestData.Conversation;
            var agent = env.TestData.Agent;
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    PrivateMetadata = $"{conversation.Id}",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AgentsInput"] = new()
                        {
                            ["AgentsSelectMenu"] = new StaticSelectMenu
                            {
                                SelectedOption = new Option("", $"{agent.Id}")
                            }
                        }
                    })
                }
            };
            var modal = env.Activate<AssignConversationModal>();
            var viewContext = env.CreateFakeViewContext(payload, modal, agent);

            await modal.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(conversation);
            var assigned = Assert.Single(conversation.Assignees);
            Assert.Equal(env.TestData.Agent.Id, assigned.Id);
            Assert.Empty(env.SlackApi.PostedMessages);
        }

        [Fact]
        public async Task ClearsAssignmentWhenNoSelectedOptionAndSendsDmToUnassignedUser()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = env.TestData.Conversation;
            var agent = env.TestData.Agent;
            var actor = env.TestData.Member;
            conversation.Assignees.Add(agent);
            await env.Db.SaveChangesAsync();
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    PrivateMetadata = $"{conversation.Id}",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AgentsInput"] = new()
                        {
                            ["AgentsSelectMenu"] = new StaticSelectMenu
                            {
                                SelectedOption = null
                            }
                        }
                    })
                }
            };
            var modal = env.Activate<AssignConversationModal>();
            var viewContext = env.CreateFakeViewContext(payload, modal, actor);

            await modal.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(conversation);
            Assert.Empty(conversation.Assignees);
            var message = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal(agent.User.PlatformUserId, message.Channel);
            Assert.Equal($"You have been unassigned from a conversation by {actor.DisplayName}.", message.Text);
            Assert.NotNull(message.Blocks);
            Assert.Collection(message.Blocks,
                b => Assert.Equal($"You have been unassigned from a conversation by {actor.ToMention()}.", Assert.IsType<Section>(b).Text?.Text),
                b => Assert.Equal("https://testorg.example.com/archives/Croom/p11110006", Assert.IsType<ButtonElement>(Assert.Single(Assert.IsType<Actions>(b).Elements)).Url?.ToString()));
        }

        [Fact]
        public async Task ClearsAssignmentWhenNoSelectedOptionAndDoesNotSendDmToSelf()
        {
            var env = TestEnvironment.Create<TestData>();
            var conversation = env.TestData.Conversation;
            var agent = env.TestData.Agent;
            var actor = agent;
            conversation.Assignees.Add(agent);
            await env.Db.SaveChangesAsync();
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    PrivateMetadata = $"{conversation.Id}",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        ["AgentsInput"] = new()
                        {
                            ["AgentsSelectMenu"] = new StaticSelectMenu
                            {
                                SelectedOption = null
                            }
                        }
                    })
                }
            };
            var modal = env.Activate<AssignConversationModal>();
            var viewContext = env.CreateFakeViewContext(payload, modal, actor);

            await modal.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(conversation);
            Assert.Empty(conversation.Assignees);
            Assert.Empty(env.SlackApi.PostedMessages);
        }
    }

    #region Test Data
    public class TestData : CommonTestData
    {
        public Member Agent { get; private set; } = null!;

        public Room Room { get; private set; } = null!;

        public Conversation Conversation { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await base.SeedAsync(env);
            Agent = await env.CreateMemberInAgentRoleAsync();
            Room = await env.CreateRoomAsync(platformRoomId: "Croom", managedConversationsEnabled: true);
            Conversation = await env.CreateConversationAsync(Room);
        }
    }
    #endregion
}
