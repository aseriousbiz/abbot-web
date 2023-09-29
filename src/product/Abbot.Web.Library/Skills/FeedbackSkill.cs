using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Skills;

[Skill(Description = "Sends the creators of Abbot feedback on the bot.")]
public class FeedbackSkill : ISkill, IHandler
{
    readonly IHostEnvironment _hostEnvironment;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly IOptions<AbbotOptions> _options;
    readonly ILogger<FeedbackSkill> _log;

    public FeedbackSkill(
        IHostEnvironment hostEnvironment,
        IBackgroundJobClient backgroundJobClient,
        IOptions<AbbotOptions> options,
        ILogger<FeedbackSkill> log)
    {
        _hostEnvironment = hostEnvironment;
        _backgroundJobClient = backgroundJobClient;
        _options = options;
        _log = log;
    }

    public async Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var text = messageContext.Arguments.Value;

        if (text is { Length: 0 })
        {
            await messageContext.SendHelpTextAsync(this);
            return;
        }

        var feedback = new FeedbackSubmission(messageContext.From.Email, text);
        await SendFeedbackAndReplyAsync(messageContext, feedback, cancellationToken);

        var thanks = "Thanks for your feedback. I sent it to my creators."
             + (messageContext.From.Email is null
                 ? " I do not know your email so they will not be able to respond to your " +
                   $"feedback. You can tell me your email with `{messageContext.Bot} my email is {{your-email}}`."
                 : "");

        await messageContext.SendActivityAsync(thanks);
    }

    /// <summary>
    /// Handles when a user clicks on the feedback button in App Home.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    public async Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext)
    {
        var viewState = new ViewState(viewContext.FromMember, null);
        var view = Render(viewState);

        if (viewContext is IModalSource modalSource)
        {
            var triggerId = viewContext.Payload.TriggerId.Require();
            await modalSource.OpenModalAsync(triggerId, view);
        }
        else
        {
            await viewContext.PushModalViewAsync(view);
        }
    }

    /// <summary>
    /// Handles when a user clicks on the feedback button in Abbot's Direct Message response to the user.
    /// </summary>
    /// <param name="platformMessage">The incoming interaction message.</param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        var privateMetadata = platformMessage.Payload.InteractionInfo?.ResponseUrl;
        var viewState = new ViewState(platformMessage.From, privateMetadata);
        var view = Render(viewState);

        var triggerId = platformMessage.TriggerId.Require();
        await platformMessage.Responder.OpenModalAsync(triggerId, view);
    }

    record ViewState(Member From, Uri? ResponseUrl);

    static ModalView Render(ViewState viewState)
    {
        var blocks = new List<ILayoutBlock>
        {
            new Section("We would love to hear your feedback!"),
        };

        if (viewState.From.User.Email is not { Length: > 0 })
        {
            blocks.Add(new Input("Email", new PlainTextInput())
            {
                Optional = true,
                BlockId = nameof(FeedbackSubmission.Email)
            });
            blocks.Add(new Context(new MrkdwnText("Please let us know your email address so we can respond to your feedback.")));
        }

        blocks.Add(new Input("Feedback", new PlainTextInput(true, nameof(FeedbackSubmission.Feedback)))
        {
            BlockId = nameof(FeedbackSubmission.Feedback)
        });

        return new ModalView
        {
            Title = "Send Us Feedback!",
            Close = "Close",
            Submit = "Send",
            CallbackId = InteractionCallbackInfo.For<FeedbackSkill>(),
            Blocks = blocks,
            PrivateMetadata = viewState.ResponseUrl?.ToString()
        };
    }

    internal record FeedbackSubmission(string? Email, string Feedback);

    /// <summary>
    /// Called when submitting feedback via the Send Feedback button in a DM with Abbot or from the App Home page.
    /// </summary>
    /// <param name="viewContext">Information about the view that was submitted.</param>
    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        var state = viewContext.Payload.View.State.Require();
        var feedback = state.BindRecord<FeedbackSubmission>();

        await SendFeedbackAndReplyAsync(viewContext, feedback, CancellationToken.None);

        if (viewContext.Payload.View.PrivateMetadata is { Length: > 0 } responseUrl
            && Uri.TryCreate(responseUrl, UriKind.Absolute, out var url))
        {
            var message = MetaBot.CreateAbbotDmResponseMessage(
                Enumerable.Empty<IActionElement>(),
                "send us feedback");
            await viewContext.UpdateActivityAsync(message, url);
        }

        await viewContext.SendDirectMessageAsync("Thanks for sending us your feedback!");
    }

    async Task SendFeedbackAndReplyAsync(
        IEventContext messageContext,
        FeedbackSubmission feedbackSubmission,
        CancellationToken cancellationToken)
    {
        string platformId = messageContext.Organization.PlatformId;
        string platformUserId = messageContext.From.PlatformUserId;

        var email = feedbackSubmission.Email ?? messageContext.From.Email ?? "unknown";

        var feedback = new FeedbackMessage(
            User: $"<a href=\"https://{WebConstants.DefaultHost}/staff/users/{platformUserId}\">{messageContext.FromMember.DisplayName} (Email: {email})</a>",
            Source: $"<a href=\"https://{WebConstants.DefaultHost}/staff/organizations/{platformId}\">{messageContext.Organization.Name}</a>",
            Product: $"Abbot [{messageContext.Organization.PlatformType}] {messageContext.Organization.PlatformId}",
            Text: $"{feedbackSubmission.Feedback}<br /><br /><hr /><br/>Sent from {_hostEnvironment.ApplicationName} {_hostEnvironment.EnvironmentName}"
        );

        var feedbackEndpoint = GetFeedbackEndpoint();

        _backgroundJobClient.Enqueue<FeedbackSender>(s =>
            s.SendFeedbackEmailAsync(feedback, feedbackEndpoint, cancellationToken));
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{feedback}", "Sends us whatever you type as feedback");
    }

    Uri GetFeedbackEndpoint()
    {
        var urlText = _options.Value.FeedbackEndpoint;
        if (urlText is null || !Uri.TryCreate(urlText, UriKind.Absolute, out var endpoint))
        {
            _log.SettingMissingOrIncorrect("Abbot:FeedbackEndpoint", urlText ?? "(missing)");
            return new Uri("https://localhost/");
        }

        return endpoint;
    }
}

public record FeedbackMessage(string User, string Source, string Product, string Text);

public class FeedbackSender
{
    readonly IHttpClientFactory _httpClientFactory;
    readonly ILogger<FeedbackSkill> _log;

    public FeedbackSender(IHttpClientFactory httpClientFactory, ILogger<FeedbackSkill> log)
    {
        _httpClientFactory = httpClientFactory;
        _log = log;
    }

    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task SendFeedbackEmailAsync(FeedbackMessage feedback, Uri feedbackEndpoint, CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        try
        {
            using var response = await httpClient.PostJsonAsync(
                feedbackEndpoint,
                feedback,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _log.ErrorSendingFeedback(feedback.User,
                    feedback.Source,
                    feedback.Text,
                    response.StatusCode,
                    responseBody);
            }
        }
        catch (Exception e)
        {
            _log.ExceptionSendingFeedback(e, feedback.User, feedback.Source, feedback.Text);
        }
    }
}

public static partial class FeedbackSkillLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Error sending user feedback (User: {FeedbackUser}, Source: {FeedbackSource}, Text: {Feedback}, StatusCode: {StatusCode}, Error: {ErrorMessage})")]
    public static partial void ErrorSendingFeedback(
        this ILogger<FeedbackSkill> logger,
        string feedbackUser,
        string feedbackSource,
        string feedback,
        HttpStatusCode statusCode,
        string errorMessage);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message =
            "Error sending user feedback (User: {FeedbackUser}, Source: {FeedbackSource}, Text: {Feedback})")]
    public static partial void ExceptionSendingFeedback(
        this ILogger<FeedbackSkill> logger,
        Exception exception,
        string feedbackUser,
        string feedbackSource,
        string feedback);
}
