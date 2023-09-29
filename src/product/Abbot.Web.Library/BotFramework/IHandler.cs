using System;
using System.Threading.Tasks;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Skills;

/// <summary>
/// Interface for any type that handles user interactions with UI elements within messages or modals. Implementations
/// can choose which of these methods to implement. Skills can implement these if they so choose.
/// </summary>
public interface IHandler
{
    /// <summary>
    /// Handles events raised when the user interacts with UI elements within a modal view.
    /// </summary>
    /// <param name="viewContext">Information about the view that was interacted with.</param>
    Task OnInteractionAsync(IViewContext<IViewBlockActionsPayload> viewContext) => Task.CompletedTask;

    /// <summary>
    /// Handles the event raised when the modal view is submitted.
    /// </summary>
    /// <param name="viewContext">Information about the view that was submitted.</param>
    Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext) => Task.CompletedTask;

    /// <summary>
    /// Handles the event raised when the modal view is closed, but not submitted.
    /// </summary>
    /// <param name="viewContext">Information about the view that was closed.</param>
    Task OnClosedAsync(IViewContext<IViewClosedPayload> viewContext) => Task.CompletedTask;

    /// <summary>
    /// Handles interactions with UI elements within a message (as opposed to a modal or view).
    /// </summary>
    /// <remarks>
    /// This method is called when a message is sent to the bot with a <see cref="IMessageBlockActionsPayload"/>.
    /// Due to legacy reasons, this is represented by <see cref="IPlatformMessage"/>. I hope to change that later to
    /// a type a type specific to interactions, but for now, we work with what we got - @haacked.
    /// </remarks>
    /// <param name="platformMessage">The incoming interaction message.</param>
    Task OnMessageInteractionAsync(IPlatformMessage platformMessage) => Task.CompletedTask;

    /// <summary>
    /// Handles requests to provide options for a <see cref="MultiExternalSelectMenu"/>.
    /// </summary>
    /// <param name="platformEvent">The incoming request for options.</param>
    /// <returns>A <see cref="BlockSuggestionsResponse"/> derived type that contains the options to render.</returns>
    Task<BlockSuggestionsResponse> OnBlockSuggestionRequestAsync(IPlatformEvent<BlockSuggestionPayload> platformEvent)
        => Task.FromResult<BlockSuggestionsResponse>(new OptionsBlockSuggestionsResponse(Array.Empty<Option>()));
}

