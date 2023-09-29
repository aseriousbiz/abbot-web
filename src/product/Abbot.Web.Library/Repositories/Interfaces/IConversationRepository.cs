using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Serialization;
using Serious.Collections;
using Serious.Filters;
using Serious.Slack;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The repository for conversations.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Given a room, returns a list of recent active conversations in that room. This is used when trying to
    /// identify a conversation for a top-level message.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> the message is in.</param>
    /// <param name="timeSpan">How far back to get recent messages.</param>
    /// <returns>A list of recent <see cref="Conversation"/> candidates for the message.</returns>
    Task<IReadOnlyList<Conversation>> GetRecentActiveConversationsAsync(Room room, TimeSpan timeSpan);

    /// <summary>
    /// Returns <c>true</c> if any conversations (including Hidden) exist for the organization. Otherwise <c>false</c>.
    /// </summary>
    /// <remarks>
    /// This is used to determine if the Organization is brand new and should be show the Getting Started flow,
    /// hence we need to include Hidden conversations.
    /// </remarks>
    /// <param name="organization">The organization.</param>
    Task<bool> HasAnyConversationsAsync(Organization organization);

    /// <summary>
    /// Looks up an existing <see cref="Conversation"/> based on the provided thread Id. If no such conversation exists, returns <c>null</c>.
    /// </summary>
    /// <param name="threadId">The thread ID of the incoming message used to use to look up the conversation. If the message is a top-level message, then this is the same as the message id.</param>
    /// <param name="roomId">The id of the room containing the conversation.</param>
    /// <param name="followHubThread">If <paramref name="roomId"/> is a <see cref="Hub"/>, follow <paramref name="threadId"/> to its <see cref="Conversation"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="Conversation"/> that was started by the provided message ID, or <c>null</c> if no such conversation exists.</returns>
    Task<Conversation?> GetConversationByThreadIdAsync(
        string threadId,
        Id<Room> roomId,
        bool followHubThread = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a set of <see cref="Conversation"/> instances using the platform-specific thread IDs, and the
    /// organization containing the conversations.
    /// </summary>
    /// <param name="threadIds">The platform-specific thread IDs.</param>
    /// <param name="organization">The organization containing the conversation.</param>
    /// <returns>A list of <see cref="EntityResult{Conversation}"/> for each passed in thread Id.</returns>
    Task<IReadOnlyList<EntityResult<Conversation>>> GetConversationsByThreadIdsAsync(
        IEnumerable<string> threadIds,
        Organization organization);

    /// <summary>
    /// Gets a conversation from it's ID
    /// </summary>
    /// <param name="id">The ID of the conversation to retrieve.</param>
    /// <returns>The <see cref="Conversation"/> with the provided ID, or <c>null</c> if no such conversation exists.</returns>
    Task<Conversation?> GetConversationAsync(Id<Conversation> id);

    /// <summary>
    /// Bulk closes conversations by their Ids.
    /// </summary>
    /// <param name="ids">The IDs of the conversation to bulk close.</param>
    /// <param name="newState">The new state (either <see cref="ConversationState.Closed"/> or <see cref="ConversationState.Archived"/>).</param>
    /// <param name="organizationId">The Id of the organization.</param>
    /// <param name="actor">The person who is closing all the conversations.</param>
    /// <param name="source">The source of closing or archiving</param>
    /// <returns>The <see cref="Conversation"/> with the provided ID, or <c>null</c> if no such conversation exists.</returns>
    Task<int> BulkCloseOrArchiveConversationsAsync(
        IEnumerable<Id<Conversation>> ids,
        ConversationState newState,
        Id<Organization> organizationId,
        Member actor,
        string source);

    /// <summary>
    /// Gets a conversation from it's ID
    /// </summary>
    /// <param name="id">The ID of the conversation to retrieve.</param>
    /// <returns>The <see cref="Conversation"/> with the provided ID, or <c>null</c> if no such conversation exists.</returns>
    Task<Conversation?> GetConversationAsync(int id);

    /// <summary>
    /// Gets a list of all conversations matching the criteria in the provided <see cref="ConversationQuery"/>.
    /// </summary>
    /// <param name="query">A <see cref="ConversationQuery"/> defining the criteria to use when searching for conversations.</param>
    /// <param name="nowUtc">The current time in UTC, for computing SLOs.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The number of elements on a page.</param>
    /// <returns>A list of conversations matching the criteria defined in <paramref name="query"/>.</returns>
    Task<ConversationListWithStats> QueryConversationsWithStatsAsync(
        ConversationQuery query,
        DateTime nowUtc,
        int pageNumber,
        int pageSize);

    /// <summary>
    /// Gets a list of all conversations matching the criteria in the provided <see cref="ConversationQuery"/>.
    /// </summary>
    /// <param name="query">A <see cref="ConversationQuery"/> defining the criteria to use when searching for conversations.</param>
    /// <param name="nowUtc">The current time in UTC, for computing SLOs.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The number of elements on a page.</param>
    /// <returns>A list of conversations matching the criteria defined in <paramref name="query"/>.</returns>
    Task<IPaginatedList<Conversation>> QueryConversationsAsync(
        ConversationQuery query,
        DateTime nowUtc,
        int pageNumber,
        int pageSize);

    /// <summary>
    /// Gets the daily stats rollups for the given user.
    /// </summary>
    /// <remarks>
    /// The roll up is generated over all rooms the user is assigned as a first responder for.
    /// This means rooms they are assigned as a first responder for.
    /// </remarks>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    /// <param name="datePeriodSelector">The period to roll-up stats for.</param>
    /// <param name="tagSelector">Filter on a tag.</param>
    /// <param name="organization">The current organization.</param>
    /// <param name="filter">Filters to apply to the query such as customer or segment.</param>
    /// <returns>A <see cref="ConversationTrendsRollup"/> containing the stats for a given day.</returns>
    Task<IReadOnlyList<ConversationTrendsRollup>> GetDailyRollupsAsync(RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        Organization organization,
        FilterList filter);

    /// <summary>
    /// Updates the conversation state in response to a new message by a member
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="messagePostedEvent">A <see cref="MessagePostedEvent"/> representing the message that was just posted.</param>
    /// <param name="message">The <see cref="ConversationMessage"/>.</param>
    /// <param name="posterIsSupportee">A boolean indicating if the user who posted the message is considered a "supportee" (i.e. someone seeking support).</param>
    Task UpdateForNewMessageAsync(
        Conversation conversation,
        MessagePostedEvent messagePostedEvent,
        ConversationMessage message,
        bool posterIsSupportee);

    /// <summary>
    /// Updates the <see cref="Conversation"/> with the provided <see cref="SummarizationResult"/>.
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="threadId">The platform-specific Id of the thread where the new message is in. A <see cref="Conversation"/> can span more than one thread.</param>
    /// <param name="summarizationResult">The result of a summarizing a conversation.</param>
    /// <param name="properties">
    /// The <see cref="ConversationProperties"/> containing additional important values, e.g. processed from
    /// <paramref name="summarizationResult"/>.
    /// This replaces all existing <see cref="Conversation.Properties"/>, so have caution to preserve existing values.
    /// </param>
    /// <param name="timestamp">The Slack timestamp of the last message covered by the summary.</param>
    /// <param name="actor">The <see cref="Member"/> who summarized the conversation, aka Abbot.</param>
    /// <param name="utcTimestamp">The UTC timestamp of the message.</param>
    /// <returns></returns>
    Task UpdateSummaryAsync(
        Conversation conversation,
        string threadId,
        SummarizationResult summarizationResult,
        ConversationProperties properties,
        SlackTimestamp timestamp,
        Member actor,
        DateTime utcTimestamp);

    /// <summary>
    /// Updates the set of thread Ids mapped to this conversation.
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="threadId">The platform-specific Id of the thread where the new message is in. A <see cref="Conversation"/> can span more than one thread.</param>
    Task SaveThreadIdToConversationAsync(Conversation conversation, string threadId);

    /// <summary>
    /// Updates the conversation state in response to a new emoji by a home member.
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="actor">The <see cref="Member"/> who posted the new message.</param>
    /// <param name="utcTimestamp">The UTC timestamp of the message.</param>
    Task<StateChangedEvent?> SnoozeConversationAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp);

    /// <summary>
    /// Changes the <see cref="ConversationState"/> for a <see cref="Conversation"/>
    /// to <see cref="ConversationState.NeedsResponse"/> after the snooze period is over..
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="actor">The <see cref="Member"/> that is waking up the message, aka Abbot.</param>
    /// <param name="utcTimestamp">The UTC timestamp of the message.</param>
    /// <returns>Returns the restored state if snoozed, otherwise the current state.</returns>
    Task<StateChangedEvent?> WakeConversationAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp);

    /// <summary>
    /// Creates a conversation.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> in which the conversation is taking place.</param>
    /// <param name="firstMessageEvent">A <see cref="MessagePostedEvent"/> representing the message that started the conversation.</param>
    /// <param name="title">The title of the new conversation.</param>
    /// <param name="startedBy">The <see cref="Member"/> who started the conversation.</param>
    /// <param name="startedAtUtc">The UTC timestamp of the message that started this conversation.</param>
    /// <param name="importedOnUtc">The UTC timestamp at which the conversation was imported.</param>
    /// <param name="initialState">The initial state to create the conversation in. Defaults to <see cref="ConversationState.New"/>.</param>
    /// <returns>The created conversation.</returns>
    Task<Conversation> CreateAsync(
        Room room,
        MessagePostedEvent firstMessageEvent,
        string title,
        Member startedBy,
        DateTime startedAtUtc,
        DateTime? importedOnUtc,
        ConversationState initialState = ConversationState.New);

    /// <summary>
    /// Gets the timeline of <see cref="ConversationEvent"/>s for the provided <paramref name="conversation"/>.
    /// The events will be sorted from earliest to latest,
    /// first by <see cref="ConversationEvent.Created"/> and then by <see cref="ConversationEvent.Id"/>
    /// though that is not a guaranteed ordering due to clock skew.
    /// </summary>
    /// <remarks>
    /// The <see cref="Conversation.Events" /> property is a lazy-loaded store fo this value.
    /// This method loads that collection if it hasn't been loaded already.
    /// Thus you are guaranteed that <see cref="Conversation.Events"/> is non-<c>null</c> upon a successful return from this method.
    /// </remarks>
    /// <param name="conversation">The <see cref="Conversation"/> to get the timeline for.</param>
    /// <returns></returns>
    Task<IReadOnlyList<ConversationEvent>> GetTimelineAsync(Conversation conversation);

    /// <summary>
    /// Archives the conversation.
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="actor">The <see cref="Member"/> that is performing this action.</param>
    /// <param name="utcTimestamp">The UTC timestamp of the event that caused this change.</param>
    Task<StateChangedEvent?> ArchiveAsync(Conversation conversation, Member actor, DateTime utcTimestamp);

    /// <summary>
    /// Unarchives the conversation.
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="actor">The <see cref="Member"/> that is performing this action.</param>
    /// <param name="utcTimestamp">The UTC timestamp of the event that caused this change.</param>
    Task<StateChangedEvent?> UnarchiveAsync(Conversation conversation, Member actor, DateTime utcTimestamp);

    /// <summary>
    /// Closes the conversation
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="actor">The <see cref="Member"/> that is performing this action.</param>
    /// <param name="utcTimestamp">The UTC timestamp of the event that caused this change.</param>
    /// <param name="source">The source of closing this conversation.</param>
    Task<StateChangedEvent?> CloseAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp,
        string? source = null);

    /// <summary>
    /// Re-opens the conversation
    /// </summary>
    /// <remarks>
    /// Reopening via this method, rather than via <see cref="UpdateForNewMessageAsync"/>,
    /// causes the conversation to be placed in the <see cref="ConversationState.Waiting"/> state.
    /// </remarks>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="actor">The <see cref="Member"/> that is performing this action.</param>
    /// <param name="utcTimestamp">The UTC timestamp of the event that caused this change.</param>
    /// <param name="newState">The new state to put the conversation in when re-opening. Defaults to Waiting.</param>
    Task<StateChangedEvent?> ReopenAsync(Conversation conversation, Member actor, DateTime utcTimestamp, ConversationState newState = ConversationState.Waiting);

    /// <summary>
    /// This retrieves every conversation waiting for a response from the support team in a room that has a
    /// Time To Respond SLO where the conversation is in breach of the Warning SLO (but not Critical).
    /// </summary>
    /// <param name="utcNow">The current time in UTC, for computing SLOs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of conversations meeting the criteria.</returns>
    Task<IReadOnlyList<Conversation>> GetConversationsInWarningPeriodForTimeToRespond(
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the conversation to note that a warning notification was sent.
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="dateSent">The date the notification was sent.</param>
    Task UpdateTimeToRespondWarningNotificationSentAsync(Conversation conversation, DateTime dateSent);

    /// <summary>
    /// Updates the conversation to note that a warning notification was sent.
    /// </summary>
    /// <param name="conversation">The conversation to update.</param>
    /// <param name="dateSent">The date the notification was sent.</param>
    /// <param name="actor">Every state change needs an actor. In this case, we'll use Abbot as the actor.</param>
    Task<StateChangedEvent?> UpdateOverdueConversationAsync(Conversation conversation, DateTime dateSent, Member actor);

    /// <summary>
    /// This retrieves every conversation waiting for a response from the support team in a room that has a
    /// Time To Respond SLO where the conversation is in breach of the Critical SLO.
    /// </summary>
    /// <remarks>
    /// This only returns overdue conversations that have not been notified yet (State != ConversationState.Overdue).
    /// </remarks>
    /// <param name="utcNow">The current time in UTC, for computing SLOs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of conversations meeting the criteria.</returns>
    Task<IReadOnlyList<Conversation>> GetOverdueConversationsToNotifyAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new <see cref="ConversationLink"/> to link the provided conversation with an external resource.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> to be linked.</param>
    /// <param name="type">The <see cref="ConversationLinkType"/> representing the type of the link.</param>
    /// <param name="externalId">The external ID of the resource being linked.</param>
    /// <param name="settings">Additional settings for the conversation link.</param>
    /// <param name="actor">The <see cref="Member"/> who is creating the link.</param>
    /// <param name="utcTimestamp">The UTC timestamp at which the link was created.</param>
    Task<ConversationLink> CreateLinkAsync(
        Conversation conversation,
        ConversationLinkType type,
        string externalId,
        JsonSettings? settings,
        Member actor,
        DateTime utcTimestamp);

    /// <summary>
    /// Gets the <see cref="ConversationLink"/> for the provided external resource.
    /// </summary>
    /// <param name="organizationId">The Id of the <see cref="Organization"/> containing the conversation.</param>
    /// <param name="type">The <see cref="ConversationLinkType"/> representing the type of the link.</param>
    /// <param name="externalId">The external ID of the linked resource.</param>
    /// <returns>The matching <see cref="ConversationLink"/>, if any.</returns>
    Task<ConversationLink?> GetConversationLinkAsync(Id<Organization> organizationId, ConversationLinkType type, string externalId);

    /// <summary>
    /// Gets the <see cref="ConversationLink"/> for the provided external resource when we don't know the organization.
    /// </summary>
    /// <param name="type">The <see cref="ConversationLinkType"/> representing the type of the link.</param>
    /// <param name="externalId">The external ID of the linked resource.</param>
    /// <returns>The matching <see cref="ConversationLink"/>, if any.</returns>
    Task<ConversationLink?> GetConversationLinkAsync(ConversationLinkType type, string externalId);

    /// <summary>
    /// Gets the <see cref="ConversationLink"/> for <paramref name="linkedConversationId"/>.
    /// </summary>
    /// <param name="linkedConversationId">The ID of the <see cref="ConversationLink"/>.</param>
    /// <returns>The matching <see cref="ConversationLink"/>, if any.</returns>
    Task<ConversationLink?> GetConversationLinkAsync(Id<ConversationLink> linkedConversationId);

    /// <summary>
    /// Adds a <see cref="ConversationEvent"/> to the provided <see cref="Conversation"/>'s timeline
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> to add the event to.</param>
    /// <param name="actor">The <see cref="Member"/> who performed the action.</param>
    /// <param name="utcTimestamp">The UTC time at which the event took place.</param>
    /// <param name="conversationEvent">The <see cref="ConversationEvent"/> to add.</param>
    Task AddTimelineEventAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp,
        ConversationEvent conversationEvent);

    /// <summary>
    /// Calls one of <see cref="UnarchiveAsync"/>, <see cref="CloseAsync"/>, <see cref="ArchiveAsync"/>, or <see cref="ReopenAsync"/> depending on the desired target state of the conversation.
    /// </summary>
    /// <remarks>
    /// Only certain state transitions are allowed, others will cause a <see cref="InvalidOperationException"/> to be thrown.
    /// <list type="bullet">
    /// <item>
    /// <description>A <see cref="ConversationState.Closed"/> conversation can be transitioned to any state except <see cref="ConversationState.New"/> or <see cref="ConversationState.Overdue"/>.</description>
    /// </item>
    /// <item>
    /// <description>A <see cref="ConversationState.Archived"/> conversation can only be set to the <see cref="ConversationState.Closed"/> state.</description>
    /// </item>
    /// <item>
    /// <description>A conversation in any other state can only transition to <see cref="ConversationState.Archived"/> or <see cref="ConversationState.Closed"/></description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The desired state transition cannot be completed.</exception>
    /// <param name="conversation">The <see cref="Conversation"/> to transition.</param>
    /// <param name="targetState">The desired target state. See remarks for more information.</param>
    /// <param name="actor">The <see cref="Member"/> who performed the action</param>
    /// <param name="utcTimestamp">The UTC timestamp at which this operation is taking place.</param>
    /// <param name="source">The source of changing the state.</param>
    async Task<StateChangedEvent?> ChangeConversationStateAsync(
        Conversation conversation,
        ConversationState targetState,
        Member actor,
        DateTime utcTimestamp,
        string source)
    {
        switch (targetState)
        {
            case ConversationState.Closed when conversation.State is ConversationState.Archived:
                return await UnarchiveAsync(conversation, actor, utcTimestamp);
            case ConversationState.Closed:
                return await CloseAsync(conversation, actor, utcTimestamp, source);
            case ConversationState.Archived:
                return await ArchiveAsync(conversation, actor, utcTimestamp);
            case ConversationState.NeedsResponse:
            case ConversationState.Waiting:
                return await ReopenAsync(conversation, actor, utcTimestamp, targetState);
            default:
                throw new InvalidOperationException(
                    $"Cannot transition conversation from {conversation.State} to {targetState}");
        }
    }

    /// <summary>
    /// Assign a <see cref="Conversation" /> to an agent.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> to assign.</param>
    /// <param name="assignees">The final set of <see cref="Member"/>s to assign to the conversation.</param>
    /// <param name="actor">The <see cref="Member"/> doing the assigning.</param>
    Task AssignConversationAsync(Conversation conversation, IReadOnlyList<Member> assignees, Member actor);

    /// <summary>
    /// Assign a <see cref="Conversation"/> to a <see cref="Hub"/>
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> to assign.</param>
    /// <param name="hub">The <see cref="Hub"/> to which the conversation should be assigned.</param>
    /// <param name="hubThreadId">The platform-specific ID of the message representing the Hub Thread for this conversation.</param>
    /// <param name="hubThreadUrl">The platform-specific URL to the message representing the Hub Thread for this conversation.</param>
    /// <param name="actor">The <see cref="Member"/> doing the assigning.</param>
    /// <param name="utcTimestamp">The UTC timestamp at the time the conversation was attached.</param>
    /// <returns></returns>
    Task<EntityResult> AttachConversationToHubAsync(Conversation conversation, Hub hub, string hubThreadId, Uri hubThreadUrl, Member actor, DateTime utcTimestamp);

    /// <summary>
    /// Update the settings for <see cref="ConversationLink"/>.
    /// </summary>
    Task UpdateConversationLinkAsync<TSettings>(ConversationLink conversationLink, TSettings settings)
        where TSettings : JsonSettings, ITicketingSettings;
}

/// <summary>
/// Extensions for <see cref="IConversationRepository"/>.
/// </summary>
public static class ConversationRepositoryExtensions
{
    /// <summary>
    /// Creates a new <see cref="ConversationLink"/> to link the provided conversation with an external resource.
    /// </summary>
    /// <param name="repository">The <see cref="IConversationRepository"/>.</param>
    /// <param name="conversation">The <see cref="Conversation"/> to be linked.</param>
    /// <param name="type">The <see cref="ConversationLinkType"/> representing the type of the link.</param>
    /// <param name="externalId">The external ID of the resource being linked.</param>
    /// <param name="actor">The <see cref="Member"/> who is creating the link.</param>
    /// <param name="utcTimestamp">The UTC timestamp at which the link was created.</param>
    public static async Task<ConversationLink> CreateLinkAsync(
        this IConversationRepository repository,
        Conversation conversation,
        ConversationLinkType type,
        string externalId,
        Member actor,
        DateTime utcTimestamp) => await repository.CreateLinkAsync(
            conversation,
            type,
            externalId,
            settings: null,
            actor,
            utcTimestamp);

    /// <summary>
    /// Retrieves a sanitized message history for a <see cref="Conversation"/>.
    /// </summary>
    /// <param name="conversationRepository">The <see cref="IConversationRepository"/>.</param>
    /// <param name="conversation">The <see cref="Conversation"/> to get the history for.</param>
    /// <returns></returns>
    public static async Task<SanitizedConversationHistory> GetSanitizedConversationHistoryAsync(
        this IConversationRepository conversationRepository,
        Conversation conversation)
    {
        var timeline = await conversationRepository.GetTimelineAsync(conversation);
        var messageEvents = timeline.OfType<MessagePostedEvent>()
            .Where(me => me.Metadata is not null)
            .ToList();

        return SanitizedConversationHistory.Sanitize(messageEvents);
    }
}
