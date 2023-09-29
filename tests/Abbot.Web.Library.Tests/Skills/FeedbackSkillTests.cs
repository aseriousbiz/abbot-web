using System.Net;
using Abbot.Common.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Serialization;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Serious.TestHelpers;

public class FeedbackSenderTests
{
    public class TheSendFeedbackEmailAsyncMethod
    {
        [Fact]
        public async Task SendsEmailToUs()
        {
            var feedbackEndpoint = new Uri("https://example.com/feedback");
            var httpMessageHandler = new FakeHttpMessageHandler();
            var requestHandler = httpMessageHandler.AddResponse(
                feedbackEndpoint,
                HttpMethod.Post,
                new HttpResponseMessage(HttpStatusCode.OK));
            var httpClient = new HttpClient(httpMessageHandler);
            var sender = new FeedbackSender(new FakeHttpClientFactory(httpClient), NullLogger<FeedbackSkill>.Instance);
            var message = new FeedbackMessage(
                "Daenerys",
                "My Org",
                "Abbot",
                "You rock!");

            await sender.SendFeedbackEmailAsync(message, feedbackEndpoint, CancellationToken.None);

            var json = AbbotJsonFormat.Default.Deserialize<JObject>(requestHandler.GetReceivedRequestBody());
            Assert.NotNull(json);
            Assert.Equal("You rock!", json.Value<string>("Text"));
            Assert.Equal("Daenerys", json.Value<string>("User"));
            Assert.Equal("My Org", json.Value<string>("Source"));
            Assert.Equal("Abbot", json.Value<string>("Product"));
        }

        [Fact]
        public async Task LogsFailure()
        {
            var log = FakeLogger.Create<FeedbackSkill>();
            var feedbackEndpoint = new Uri("https://example.com/feedback");
            var httpMessageHandler = new FakeHttpMessageHandler();
            httpMessageHandler.AddResponse(feedbackEndpoint, new HttpResponseMessage(HttpStatusCode.Forbidden));
            var httpClient = new HttpClient(httpMessageHandler);
            var sender = new FeedbackSender(new FakeHttpClientFactory(httpClient), log);
            var message = new FeedbackMessage(
                "Daenerys",
                "My Org",
                "Abbot",
                "You rock!");

            await sender.SendFeedbackEmailAsync(message, feedbackEndpoint, CancellationToken.None);

            var logged = log.Provider.GetAllEvents().Single();
            Assert.StartsWith("Error sending user feedback", logged.Message);
        }

        [Fact]
        public async Task LogsException()
        {
            var log = FakeLogger.Create<FeedbackSkill>();
            var feedbackEndpoint = new Uri("https://example.com/feedback");
            var httpMessageHandler = new FakeHttpMessageHandler();
            httpMessageHandler.AddResponseException(
                feedbackEndpoint,
                HttpMethod.Post,
                new InvalidOperationException("Gurgle"));
            var httpClient = new HttpClient(httpMessageHandler);
            var sender = new FeedbackSender(new FakeHttpClientFactory(httpClient), log);
            var message = new FeedbackMessage(
                "Daenerys",
                "My Org",
                "Abbot",
                "You rock!");

            await sender.SendFeedbackEmailAsync(message, feedbackEndpoint, CancellationToken.None);

            var logged = log.Provider.GetAllEvents().Single();
            Assert.StartsWith("Error sending user feedback", logged.Message);
        }
    }
}

public class FeedbackSkillTests
{
    public class TheOnMessageActivityAsyncMethod
    {
        [Theory]
        [InlineData(null, "Real Name (Email: unknown)", "Thanks for your feedback. I sent it to my creators. I do not know your email so they will not be able to respond to your feedback. You can tell me your email with `<@U001> my email is {your-email}`.")]
        [InlineData("a@example.com", "Real Name (Email: a@example.com)", "Thanks for your feedback. I sent it to my creators.")]
        public async Task SchedulesEmail(string email, string expectedName, string expected)
        {
            var options = Options.Create(new AbbotOptions()
            {
                FeedbackEndpoint = "https://localhost/api",
            });
            var backgroundClient = new FakeBackgroundJobClient();
            var skill = new FeedbackSkill(
                new FakeHostEnvironment(),
                backgroundClient,
                options,
                NullLogger<FeedbackSkill>.Instance);
            var messageContext = FakeMessageContext.Create(
                skillName: "feedback",
                arguments: "Your shit is broken.",
                organization: new Organization
                {
                    Id = 42,
                    Name = "My Org",
                    Domain = "test.slack.com",
                    PlatformId = "T013108BYLS",
                    PlatformBotId = "B001",
                    PlatformBotUserId = "U001",
                    PlatformType = PlatformType.Slack,
                    Slug = "abc42",
                },
                sender: new Member
                {
                    Id = 17,
                    User = new User
                    {
                        Id = 99,
                        DisplayName = "Real Name",
                        PlatformUserId = "U42",
                        Email = email
                    }
                });

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal(expected, reply);
            var (job, _, _) = Assert.Single(backgroundClient.EnqueuedJobs);
            Assert.Equal(nameof(FeedbackSender.SendFeedbackEmailAsync), job.Method.Name);
            var sentMessage = Assert.IsType<FeedbackMessage>(job.Args[0]);
            Assert.Equal("Your shit is broken.<br /><br /><hr /><br/>Sent from Unit Tests Development", sentMessage.Text);
            Assert.Equal($@"<a href=""https://localhost:4979/staff/users/U42"">{expectedName}</a>", sentMessage.User);
            Assert.Equal(@"<a href=""https://localhost:4979/staff/organizations/T013108BYLS"">My Org</a>", sentMessage.Source);
            Assert.Equal("Abbot [Slack] T013108BYLS", sentMessage.Product);
            var feedbackEndpoint = Assert.IsType<Uri>(job.Args[1]);
            Assert.Equal(new Uri("https://localhost/api"), feedbackEndpoint);
        }

        [Fact]
        public async Task ShowsUsageForEmptyArguments()
        {
            var options = Options.Create(new AbbotOptions()
            {
                FeedbackEndpoint = "https://localhost/api",
            });
            var skill = new FeedbackSkill(
                new FakeHostEnvironment(),
                new FakeBackgroundJobClient(),
                options,
                NullLogger<FeedbackSkill>.Instance);
            var message = FakeMessageContext.Create(skillName: "feedback", arguments: "");

            await skill.OnMessageActivityAsync(message, CancellationToken.None);
            Assert.Equal(
                "`<@U001> feedback {feedback}` _Sends us whatever you type as feedback_.",
                message.SingleReply());
        }
    }

    public class TheOnMessageInteractionAsyncMethod
    {
        [Fact]
        public async Task ShowsFeedbackDialog()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var messageInteractionInfo = new MessageInteractionInfo(
                new InteractiveMessagePayload { TriggerId = "some-trigger-id", ResponseUrl = new Uri("https://example.com/response-url") },
                "",
                InteractionCallbackInfo.For<FeedbackSkill>());
            var message = env.CreatePlatformMessage(room, interactionInfo: messageInteractionInfo);
            var skill = env.Activate<FeedbackSkill>();

            await skill.OnMessageInteractionAsync(message);

            var modal = Assert.IsType<ModalView>(env.Responder.OpenModals["some-trigger-id"]);
            Assert.Equal("Send Us Feedback!", modal.Title);
            Assert.Equal("https://example.com/response-url", modal.PrivateMetadata);
        }
    }

    public class TheOnSubmissionAsyncMethod
    {
        [Fact]
        public async Task SendsEmailToUsAndUpdatesOriginalMessage()
        {
            var backgroundClient = new FakeBackgroundJobClient();
            var responder = new FakeResponder();
            var options = Options.Create(new AbbotOptions()
            {
                FeedbackEndpoint = "https://localhost/api",
            });

            var skill = new FeedbackSkill(
                new FakeHostEnvironment(),
                backgroundClient,
                options,
                NullLogger<FeedbackSkill>.Instance);

            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [nameof(FeedbackSkill.FeedbackSubmission.Feedback)] = new()
                        {
                            ["xyz"] = new PlainTextInput { Value = "Your shit is broken." }
                        },
                        [nameof(FeedbackSkill.FeedbackSubmission.Email)] = new()
                        {
                            ["xyz"] = new PlainTextInput { Value = "b@example.com" }
                        }
                    }),
                    PrivateMetadata = "https://example.com/response-url"
                }
            };
            var organization = new Organization
            {
                Id = 42,
                Name = "My Org",
                PlatformId = "T013108BYLS",
                PlatformType = PlatformType.Slack,
                Slug = "abc42"
            };
            var from = new Member
            {
                Id = 17,
                User = new User
                {
                    Id = 99,
                    DisplayName = "Real Name",
                    PlatformUserId = "U42",
                    Email = null
                }
            };
            var platformEvent = new PlatformEvent<IViewSubmissionPayload>(
                payload,
                "triggerId",
                BotChannelUser.GetBotUser(organization),
                DateTimeOffset.UtcNow,
                responder,
                from,
                null,
                organization);
            var viewContext = new ViewContext<IViewSubmissionPayload>(platformEvent, skill);

            await skill.OnSubmissionAsync(viewContext);

            var sentMessages = responder.SentMessages.ToList();
            Assert.Equal(2, sentMessages.Count);
            var updated = Assert.IsType<RichActivity>(sentMessages[0]);
            Assert.Equal(":wave: Hey! Thanks for the message. What would you like to do next?", updated.Text);
            var lastBlockText = Assert.IsType<MrkdwnText>(Assert.IsType<Context>(updated.Blocks.Last()).Elements[0]);
            Assert.Equal("You chose to send us feedback.", lastBlockText.Text);
            Assert.Equal("Thanks for sending us your feedback!", sentMessages[1].Text);
            var (job, _, _) = Assert.Single(backgroundClient.EnqueuedJobs);
            Assert.Equal(nameof(FeedbackSender.SendFeedbackEmailAsync), job.Method.Name);
            var emailedMessage = Assert.IsType<FeedbackMessage>(job.Args[0]);
            Assert.Equal("Your shit is broken.<br /><br /><hr /><br/>Sent from Unit Tests Development", emailedMessage.Text);
            Assert.Equal($@"<a href=""https://localhost:4979/staff/users/U42"">Real Name (Email: b@example.com)</a>", emailedMessage.User);
            Assert.Equal(@"<a href=""https://localhost:4979/staff/organizations/T013108BYLS"">My Org</a>", emailedMessage.Source);
            Assert.Equal("Abbot [Slack] T013108BYLS", emailedMessage.Product);
            var feedbackEndpoint = Assert.IsType<Uri>(job.Args[1]);
            Assert.Equal(new Uri("https://localhost/api"), feedbackEndpoint);
        }
    }
}
