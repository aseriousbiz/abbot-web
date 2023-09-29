using System.Collections.Generic;
using Microsoft.Bot.Schema;
using Serious.Abbot.Events;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Payloads;

namespace Serious.Abbot.Messaging;

/// <summary>
/// An incoming <c>block_actions</c> payload from Slack initiated by an interaction with
/// a view.
/// </summary>
public interface IViewContext<out TPayload> : IEventContext where TPayload : IViewPayload
{
    /// <summary>
    /// Contains information about the interaction if this message represents a user interaction with a UI element in
    /// a view.
    /// </summary>
    TPayload Payload { get; }

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
    /// <param name="view">The view to push onto the stack.</param>
    Task<ViewResponse> PushModalViewAsync(ViewUpdatePayload view);

    /// <summary>
    /// Update an existing view. Update a view by passing a new view definition object along with the
    /// <c>view_id</c> returned in views.open or the <c>external_id</c>. See the modals documentation
    /// to learn more about updating views and avoiding race conditions with the hash argument.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/views.update" /> for more info.
    /// </remarks>
    /// <param name="view">Information about the modal view to update.</param>
    Task<ViewResponse> UpdateModalViewAsync(ViewUpdatePayload view);

    /// <summary>
    /// Updates the parent modal of a child modal. If the current modal has a parent modal on the modal stack,
    /// this will update that modal.
    /// </summary>
    /// <param name="view">Information about the modal view to update.</param>
    Task<ViewResponse> UpdateParentModalViewAsync(ViewUpdatePayload view);

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
    /// Returns true if <c>ReportValidationErrors</c> has been called with a non-empty dictionary at least once.
    /// </summary>
    bool HasValidationErrors { get; }

    /// <summary>
    /// Updates an existing chat message.
    /// </summary>
    /// <param name="message">The message to replace the existing message with.</param>
    /// <param name="responseUrl">The Slack response URL used to update the message.</param>
    Task UpdateActivityAsync(IMessageActivity message, Uri responseUrl);
}

public static class ViewContextExtensions
{
    /// <summary>
    /// When an interaction occurs, this method is used to report back that there was a single validation error in the
    /// submission.
    /// </summary>
    /// <param name="viewContext">The view context.</param>
    /// <param name="blockId">The block id of the block in error.</param>
    /// <param name="errorMessage">The error message to display.</param>
    public static void ReportValidationErrors<TPayload>(
        this IViewContext<TPayload> viewContext,
        string blockId,
        string errorMessage) where TPayload : IViewPayload
    {
        viewContext.ReportValidationErrors(new Dictionary<string, string>
        {
            {blockId, errorMessage}
        });
    }

    /// <summary>
    /// Sets the <see cref="ResponseAction"/> for this request to clear all views.
    /// </summary>
    /// <param name="viewContext">A <see cref="IViewContext{TPayload}"/> representing any view event.</param>
    public static void RespondByClosingAllViews<TPayload>(this IViewContext<TPayload> viewContext)
        where TPayload : IViewPayload =>
        viewContext.SetResponseAction(new ClearResponseAction());

    /// <summary>
    /// Sets the <see cref="ResponseAction"/> for this request to update the current view with the specified <paramref name="payload"/>.
    /// Only valid on view submission events.
    /// </summary>
    /// <param name="viewContext">A <see cref="IViewContext{IViewSubmissionPayload}"/> representing a view submission event.</param>
    /// <param name="payload">The new view to update.</param>
    public static void RespondByUpdatingView(this IViewContext<IViewSubmissionPayload> viewContext,
        ViewUpdatePayload payload) =>
        viewContext.SetResponseAction(new UpdateResponseAction(payload));
}

/// <summary>
/// An incoming <c>block_actions</c> payload from Slack initiated by an interaction with
/// a view.
/// </summary>
public sealed record ViewContext<TPayload> : EventContext, IModalSource, IViewContext<TPayload>
    where TPayload : IViewPayload
{
    readonly string _callbackId;

    /// <summary>
    /// Constructs a new <see cref="ViewContext{TPayload}"/> from the platform event.
    /// </summary>
    /// <param name="platformEvent">The incoming view event.</param>
    /// <param name="handler">The handler that will handle the view event.</param>
    public ViewContext(IPlatformEvent<TPayload> platformEvent, IHandler handler) : base(platformEvent)
    {
        Payload = platformEvent.Payload;
        _callbackId = new InteractionCallbackInfo(handler.GetType().Name);
    }

    /// <summary>
    /// The payload from the <c>block_actions</c> event raised when interacting with a view.
    /// </summary>
    public TPayload Payload { get; }

    public async Task<ViewResponse> PushModalViewAsync(ViewUpdatePayload view)
    {
        if (Payload is IInteractionPayload { TriggerId: { Length: > 0 } triggerId })
        {
            return await Responder.PushModalAsync(triggerId, EnsureCallbackId(view));
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot push a modal from a {Payload.Type} payload without a valid Trigger Id.");
        }
    }

    public async Task<ViewResponse> UpdateModalViewAsync(ViewUpdatePayload view)
    {
        return await Responder.UpdateModalAsync(Payload.View.Id, EnsureCallbackId(view));
    }

    public async Task<ViewResponse> UpdateParentModalViewAsync(ViewUpdatePayload view)
    {
        return await Responder.UpdateModalAsync(Payload.View.PreviousViewId.Require(), EnsureCallbackId(view));
    }

    public void ReportValidationErrors(IReadOnlyDictionary<string, string> errors)
    {
        Responder.ReportValidationErrors(errors);
    }

    public void SetResponseAction(ResponseAction action)
    {
        Responder.SetResponseAction(action);
    }

    public bool HasValidationErrors => Responder.HasValidationErrors;

    public async Task UpdateActivityAsync(IMessageActivity message, Uri responseUrl)
    {
        var activity = message as RichActivity ?? new RichActivity(message.Text);

        activity.ResponseUrl = responseUrl;
        await Responder.UpdateActivityAsync(activity);
    }

    // Only the App Home view can create a modal. So we want this to be hidden from other views by default
    // so they don't accidentally try to do the wrong thing. The App Home handler can try to cast to
    // IModalSource.
    async Task<ViewResponse> IModalSource.OpenModalAsync(string triggerId, ViewUpdatePayload view)
    {
        return await Responder.OpenModalAsync(triggerId, view);
    }

    ViewUpdatePayload EnsureCallbackId(ViewUpdatePayload viewUpdatePayload)
    {
        return viewUpdatePayload.CallbackId is null
            ? viewUpdatePayload with
            {
                CallbackId = _callbackId
            }
            : viewUpdatePayload;
    }
}
