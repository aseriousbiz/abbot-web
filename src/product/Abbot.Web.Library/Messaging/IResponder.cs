using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Serious.Abbot.Scripting;
using Serious.Slack;

namespace Serious.Abbot.Messaging;

/// <summary>
/// The simplest interface for responding back to chat. This wraps an <see cref="ITurnContext"/>.
/// </summary>
public interface IResponder : IModalSource
{
    /// <summary>
    /// Sends an activity response back to the chat platform.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="messageTarget">The target of the message. If null, targets the current thread (if in a thread) or the current room.</param>
    Task SendActivityAsync(IMessageActivity message, IMessageTarget? messageTarget = null);

    /// <summary>
    /// Updates an existing chat message.
    /// </summary>
    /// <param name="message">The message to update.</param>
    Task UpdateActivityAsync(IMessageActivity message);

    /// <summary>
    /// When an interaction occurs, this method is used to report back that there were validation errors in the
    /// submission.
    /// </summary>
    /// <param name="errors">Dictionary of errors where the keys match block Ids and values are the error message to display.</param>
    void ReportValidationErrors(IReadOnlyDictionary<string, string> errors);

    /// <summary>
    /// Sets the <see cref="ResponseAction"/> to be sent for this submission.
    /// This will override any validation errors specified in <see cref="ReportValidationErrors"/>.
    /// </summary>
    void SetResponseAction(ResponseAction action);

    /// <summary>
    /// This is used by by the <see cref="BlockSuggestionPayloadHandler"/> so we can respond with
    /// a JSON payload.
    /// </summary>
    void SetJsonResponse(object responseBody);

    /// <summary>
    /// Returns true if there are any validation errors.
    /// </summary>
    bool HasValidationErrors { get; }

    /// <summary>
    /// Removes a chat message from the chat platform.
    /// </summary>
    /// <param name="platformRoomId">The platform-specific Id of the room the activity is in.</param>
    /// <param name="activityId">The Id of the activity. In the case of Slack, the timestamp for the message.</param>
    Task DeleteActivityAsync(string platformRoomId, string activityId);

    /// <summary>
    /// Removes a chat message from the chat platform.
    /// </summary>
    /// <param name="responseUrl">The message response URL used to delete a message.</param>
    Task DeleteActivityAsync(Uri responseUrl);

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
    Task<ViewResponse> PushModalAsync(string triggerId, ViewUpdatePayload view);

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
    Task<ViewResponse> UpdateModalAsync(string viewId, ViewUpdatePayload view);
}

/// <summary>
/// A source that can open new modals.
/// </summary>
public interface IModalSource
{
    /// <summary>
    /// Opens the view as a modal (Slack only).
    /// </summary>
    /// <param name="triggerId">A short-lived ID that can be used to <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see>.</param>
    /// <param name="view">The view to open.</param>
    Task<ViewResponse> OpenModalAsync(string triggerId, ViewUpdatePayload view);
}
