using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Middleware;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Cryptography;
using Serious.Slack;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.TestHelpers
{
    public class AbbotTestAdapter : TestAdapter
    {
        const string UserId = "U42";

        readonly IBot _bot;
        readonly IServiceProvider _provider;
        readonly IdGenerator _idGenerator = new();

        public static async Task<AbbotTestAdapter> CreateAsync(
            PlatformType platformType,
            string? botUserId = null,
            string? botId = null)
        {
            var platformId = GetDefaultCustomerPlatformId();
            var platformBotId = botId ?? GetBotId();
            var platformBotUserId = botUserId ?? GetBotUserId();
            var provider = AbbotWebUnitTestStartup.CreateAbbotTestAdapterServiceProvider()
                .CreateScope()
                .ServiceProvider;
            var organizationRepository = provider.GetService<IOrganizationRepository>()!;
            var organization = await organizationRepository.CreateOrganizationAsync(
                platformId,
                PlanType.Free,
                "Unit Test Org",
                "slack-domain",
                platformId,
                "https://slack.example.com/avatar.png");

            var auth = new SlackAuthorization(
                "A01234",
                "Test Abbot",
                platformBotId,
                platformBotUserId,
                "test-abbot",
                "https://slack.example.com/org-avatar.png",
                BotResponseAvatar: null,
                new SecretString("the-api-token", new FakeDataProtectionProvider()),
                "some-scopes");
            auth.Apply(organization);

            return new AbbotTestAdapter(provider, organization, platformBotId);
        }

        AbbotTestAdapter(IServiceProvider provider, Organization organization, string botId)
        {
            _provider = provider;
            _bot = provider.GetRequiredService<IBot>();

            if (provider.GetService<ISlackApiClient>() is FakeSimpleSlackApiClient slackApiClient)
            {
                slackApiClient.AddUserInfoResponse("the-api-token", new UserInfo
                {
                    Id = UserId,
                    TeamId = GetDefaultCustomerPlatformId(),
                    Name = "username",
                    UserName = "username",
                    RealName = "realname",
                    Profile = new UserProfile
                    {
                        DisplayName = "username",
                        RealName = "realname"
                    }
                });
            }
            var formatter = provider.GetRequiredService<IMessageFormatter>();
            Use(new MessageFormatMiddleware(formatter));
            Use(new DebugMiddleware());

            Organization = organization;

            string userId = $"{UserId}:{GetDefaultCustomerPlatformId()}";
            Conversation.Bot.Id = botId;
            Conversation.Bot.Name = "abbot";
            Conversation.User.Id = userId;
            Conversation.User.AadObjectId = userId;
            Conversation.Conversation.IsGroup = true;
            Conversation.Conversation.Name = "the-room";
            Conversation.Conversation.Id = "B028535TCK0:T013108BYLS:C01A3DGTSP9";
        }

        static string GetDefaultCustomerPlatformId() => "T013108BYLS";

        static string GetBotId() => "B01U63BS0EL";

        static string GetBotUserId() => "U013WCHH9NU";

        public void PushSkillRunResponse(string responseMessage)
        {
            var response = new SkillRunResponse
            {
                Success = true,
                Replies = new List<string> { responseMessage },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            };
            PushSkillRunResponse(response);
        }

        void PushSkillRunResponse(SkillRunResponse response)
        {
            (GetService<ISkillRunnerClient>() as FakeSkillRunnerClient)?.PushResponse(response);
        }

        public Task<Member> CreateUser()
        {
            var user = new UserEventPayload("U002", "T001", "Real User", "TestUser");
            return GetService<IUserRepository>()!.EnsureAndUpdateMemberAsync(user, Organization);
        }

        public Organization Organization { get; }

        public T? GetService<T>()
        {
            return _provider.GetService<T>();
        }

        public async Task SendTextToBotAsync(string userSays)
        {
            var activity = MakeActivity(userSays);
            activity.ChannelData = CreateSlackChannelData(userSays);
            activity.ChannelId = "slack";
            await ProcessActivityAsync(activity, _bot.OnTurnAsync, CancellationToken.None);
        }

        protected override TurnContext CreateTurnContext(Activity activity)
        {
            var turnContext = base.CreateTurnContext(activity);
            turnContext.SetApiToken(Organization.ApiToken ?? new SecretString("xoxb-whatever-makes-the-test-pass", new FakeDataProtectionProvider()));
            return turnContext;
        }

        JObject CreateSlackChannelData(string userSays)
        {
            return JObject.FromObject(new {
                SlackMessage = new {
                    type = "event_callback",
                    api_app_id = Organization.BotAppId,
                    team_id = Organization.PlatformId,
                    @event = new {
                        text = userSays,
                        team = Organization.PlatformId,
                        ts = _idGenerator.GetSlackMessageId(),
                        type = "message",
                        user = UserId,
                        channel = "C01A3DGTSP9",
                        channel_type = Conversation.Conversation.IsGroup ?? false ? "channel" : "im",
                        blocks = new object[] { }
                    }
                }
            });
        }

        /// <summary>
        /// Sends text to a bot and gets the first immediate reply.
        /// If the bot responds asynchronously, this method may return <c>null</c>
        /// </summary>
        /// <param name="userSays">The text the user is sending</param>
        /// <returns>The first response to that message, or null if the bot made no response inline.</returns>
        public async Task<IMessageActivity?> SendTextToBotAndGetNextReplyAsync(string userSays)
        {
            await SendTextToBotAsync(userSays);

            // We expect our reply to be _immediately_ ready, otherwise we should return null.
            // If we're testing an async process where replies are sent off the main pipeline, don't use this :)

            // ReSharper disable once MethodHasAsyncOverload
            return GetNextReply() as IMessageActivity;
        }
    }
}
