using System.Collections.Generic;
using System.Linq;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BotFramework;
using Serious.Slack.BotFramework.Model;

namespace Serious.Abbot.Messaging;

/// <summary>
/// The simplest interface for responding back to chat. This wraps an <see cref="ITurnContext"/>.
/// </summary>
#pragma warning disable CA1724
public class Responder : IResponder
#pragma warning restore CA1724
{
    readonly ISlackApiClient _slackApiClient;
    static readonly ILogger<Responder> Log = ApplicationLoggerFactory.CreateLogger<Responder>();

    readonly ITurnContext _turnContext;

    /// <summary>
    /// Creates a new instance of the <see cref="Responder"/> class with the specified <see cref="ITurnContext"/>
    /// and the <see cref="Organization"/> the responder belongs to.
    /// </summary>
    /// <param name="slackApiClient">The Slack API Client to use to respond to Slack.</param>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="bot">The bot.</param>
    public Responder(ISlackApiClient slackApiClient, ITurnContext turnContext, BotChannelUser bot)
        : this(slackApiClient, turnContext, bot.ApiToken)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Responder"/> class with the specified <see cref="ITurnContext"/>
    /// and the <see cref="Organization"/> the responder belongs to.
    /// </summary>
    /// <remarks>
    /// This constructor is used when the organization is not yet created, such as in the install event.
    /// </remarks>
    /// <param name="slackApiClient">The Slack API Client to use to respond to Slack.</param>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="apiToken">The API token needed to respond, if any.</param>
    Responder(ISlackApiClient slackApiClient, ITurnContext turnContext, SecretString? apiToken)
        : this(slackApiClient, turnContext)
    {
        if (apiToken is not null)
        {
            StoreApiToken(apiToken);
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Responder"/> class with the specified <see cref="ITurnContext"/>.
    /// </summary>
    /// <param name="slackApiClient">The Slack API Client to use to respond to Slack.</param>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    public Responder(ISlackApiClient slackApiClient, ITurnContext turnContext)
    {
        _slackApiClient = slackApiClient;
        _turnContext = turnContext;
    }

    /// <summary>
    /// Sends an activity response back to the chat platform.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="messageTarget">The target of the message. If null, targets the current thread (if in a thread) or the current room.</param>
    public async Task SendActivityAsync(IMessageActivity message, IMessageTarget? messageTarget = null)
    {
        if (messageTarget is not null)
        {
            message.OverrideDestination(messageTarget);
        }

        await _turnContext.SendActivityAsync(message);
    }

    /// <summary>
    /// Removes a chat message from the chat platform.
    /// </summary>
    /// <param name="message">The message to update.</param>
    public async Task UpdateActivityAsync(IMessageActivity message)
    {
        await _turnContext.UpdateActivityAsync(message);
    }

    /// <summary>
    /// When an interaction occurs, this method is used to report back that there were validation errors in the
    /// submission.
    /// </summary>
    /// <param name="errors">Dictionary of errors where the keys match block Ids and values are the error message to display.</param>
    public void ReportValidationErrors(IReadOnlyDictionary<string, string> errors)
    {
        if (!errors.Any())
        {
            return;
        }

        if (!_turnContext.TurnState.TryGetValue(ActivityResult.ResponseBodyKey, out var existing)
            || existing is not ErrorResponseAction existingErrors)
        {
            _turnContext.TurnState.Add(ActivityResult.ResponseBodyKey, new ErrorResponseAction(errors));
            return;
        }
        _turnContext.TurnState[ActivityResult.ResponseBodyKey] = existingErrors.Append(errors);
    }

    public void SetResponseAction(ResponseAction action)
    {
        SetJsonResponse(action);
    }

    public void SetJsonResponse(object responseBody)
    {
        _turnContext.TurnState[ActivityResult.ResponseBodyKey] = responseBody;
    }

    public bool HasValidationErrors => _turnContext.TurnState.TryGetValue(ActivityResult.ResponseBodyKey, out var action) && action is ErrorResponseAction;

    /// <summary>
    /// Removes a chat message from the chat platform.
    /// </summary>
    /// <param name="platformRoomId">The platform-specific Id of the room the activity is in.</param>
    /// <param name="activityId">The Id of the activity. In the case of Slack, the timestamp for the message.</param>
    public async Task DeleteActivityAsync(string platformRoomId, string activityId)
    {
        await DeleteActivityAsync(platformRoomId, activityId, null);
    }

    public async Task DeleteActivityAsync(Uri responseUrl)
    {
        await DeleteActivityAsync(null, null, responseUrl);
    }

    void StoreApiToken(SecretString apiToken)
    {
        _turnContext.SetApiToken(apiToken);
    }

    async Task DeleteActivityAsync(string? platformRoomId, string? activityId, Uri? responseUrl)
    {
        if (!_turnContext.TryGetApiToken(out var apiToken))
        {
            throw new InvalidOperationException("No API Token provided");
        }

        var properties = new JObject
        {
            ["ChannelData"] = JObject.FromObject(new DeleteChannelData(apiToken, responseUrl))
        };

        // Need to unwrap the api token and put it somewhere BotFramework can retrieve it.
        var conversation = new ConversationReference
        {
            ActivityId = activityId,
            ChannelId = platformRoomId,
            Conversation = new ConversationAccount
            {
                Properties = properties
            }
        };
        await _turnContext.DeleteActivityAsync(conversation);
    }

    /// <summary>
    /// Opens the view as a modal (Slack only).
    /// </summary>
    /// <param name="triggerId">A short-lived ID that can be used to <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see>.</param>
    /// <param name="view">The view to open.</param>
    public async Task<ViewResponse> OpenModalAsync(string triggerId, ViewUpdatePayload view)
    {
        return await OpenOrPushModalAsync(triggerId, push: false, view);
    }

    /// <summary>
    /// Push a view onto the stack of a root view. Use this to Push a new view onto the existing view stack by
    /// passing a view object and a valid <c>trigger_id</c> generated from an interaction within the existing
    /// modal. The pushed view is added to the top of the stack, so the user will go back to the previous view
    /// after they complete or cancel the pushed view.
    /// <para>
    /// After a modal is opened, the app is limited to pushing 2 additional views.
    /// </para>
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/views.push" /> for more info.
    /// </remarks>
    /// <param name="triggerId">A short-lived ID that can be used to <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see>.</param>
    /// <param name="view">The view to push onto the stack.</param>
    public async Task<ViewResponse> PushModalAsync(string triggerId, ViewUpdatePayload view)
    {
        return await OpenOrPushModalAsync(triggerId, push: true, view);
    }

    /// <summary>
    /// Update an existing view. Update a view by passing a new view definition object along with the
    /// <c>view_id</c> returned in views.open or the <c>external_id</c>. See the modals documentation
    /// to learn more about updating views and avoiding race conditions with the hash argument.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/views.update" /> for more info.
    /// </remarks>
    /// <param name="viewId">The Id of the view to update.</param>
    /// <param name="view">Information about the modal view to update.</param>
    public async Task<ViewResponse> UpdateModalAsync(string viewId, ViewUpdatePayload view)
    {
        if (!_turnContext.TryGetApiToken(out var apiToken))
        {
            throw new InvalidOperationException("No API Token provided");
        }

        Log.UpdatingModal(viewId, view.CallbackId, view.PrivateMetadata);

        var request = new UpdateViewRequest(viewId, null, view);
        var response = await _slackApiClient.UpdateModalViewAsync(apiToken.Reveal(), request);
        if (!response.Ok)
        {
            Log.ErrorCallingSlackApi(response.ToString());
        }
        return response;
    }

    async Task<ViewResponse> OpenOrPushModalAsync(string triggerId, bool push, ViewUpdatePayload view)
    {
        if (!_turnContext.TryGetApiToken(out var apiToken))
        {
            throw new InvalidOperationException("No API Token provided");
        }
        Log.OpeningModal(view.CallbackId, view.PrivateMetadata);

        Expect.True(triggerId.Length > 0);

        var request = new OpenViewRequest(triggerId, view);

        var response = await (push
            ? _slackApiClient.PushModalViewAsync(apiToken.Reveal(), request)
            : _slackApiClient.OpenModalViewAsync(apiToken.Reveal(), request));
        if (!response.Ok)
        {
            Log.ErrorCallingSlackApi(response.ToString());
        }
        return response;
    }
}

public static partial class ResponderLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Opening/Pushing Modal: CallbackId: {CallbackId}, PrivateMetadata: {PrivateMetadata}")]
    public static partial void OpeningModal(this ILogger<Responder> logger, string? callbackId, string? privateMetadata);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Updating Modal: ViewId: {ViewId}, CallbackId: {CallbackId}, PrivateMetadata: {PrivateMetadata}")]
    public static partial void UpdatingModal(this ILogger<Responder> logger, string viewId, string? callbackId, string? privateMetadata);
}
