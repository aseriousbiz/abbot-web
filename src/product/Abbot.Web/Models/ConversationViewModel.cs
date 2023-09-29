using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.TagHelpers;
using Serious.Abbot.Messaging;
using Serious.Abbot.Pages.Conversations;
using Serious.Abbot.Repositories;
using Serious.AspNetCore.DataAnnotations;

namespace Serious.Abbot.Models;

public class ConversationViewModel
{
    public Conversation Conversation { get; }
    public RenderedMessage Title { get; }

    public string? Summary => Conversation.Properties.Summary;

    public string? Conclusion => Conversation.Properties.Conclusion;

    public ViewPage.StateChangeAction? SuggestedStateChange { get; }

    public string StatusMessage { get; }
    public ThresholdStatus ThresholdStatus { get; }

    [StringLength(38, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 38 characters in length.")]
    [RegularExpression(Skill.ValidNamePattern, ErrorMessage = Skill.NameErrorMessage)]
    [Remote(action: "Validate", controller: "TagValidation", areaName: "InternalApi")]
    [RequiredIf(nameof(CreateNewTag), true)]
    public string? NewTagName { get; set; }

    public bool CreateNewTag { get; set; }

    /// <summary>
    /// This conversation needs a response from the home org.
    /// </summary>
    public bool NeedsResponse => Conversation.State.IsWaitingForResponse();

    /// <summary>
    /// The set of tags for this conversation.
    /// </summary>
    public IReadOnlyList<Tag> Tags => Conversation.Tags.Where(TagRepository.VisibleTagsFilter)
        .Select(t => t.Tag)
        .OrderBy(t => t.Generated)
        .ToList();

    /// <summary>
    /// This conversation is New
    /// </summary>
    public bool IsNew => Conversation.State == ConversationState.New;

    public ConversationViewModel(Conversation conversation, RenderedMessage title, RenderedMessage? summary, IClock? clock = null)
    {
        clock ??= IClock.System;

        if (Enum.TryParse<ConversationState>(conversation.Properties.SuggestedState, out var suggestedState))
        {
            SuggestedStateChange = suggestedState switch
            {
                ConversationState.Closed when conversation.State.IsOpen() =>
                    ViewPage.StateChangeAction.Close,
                ConversationState.Snoozed when conversation.State.IsWaitingForResponse() =>
                    ViewPage.StateChangeAction.Snooze,
                _ => null,
            };
        }

        Conversation = conversation;
        Title = title;
        StatusMessage = GetStatusMessage(conversation, clock);
        ThresholdStatus = GetThresholdStatus(conversation, clock);
    }

    static string GetStatusMessage(Conversation convo, IClock clock)
    {
        var sinceLastChange = clock.UtcNow - convo.LastStateChangeOn;
        if (convo.State is ConversationState.New
                or ConversationState.NeedsResponse
                or ConversationState.Overdue && convo.Room.TimeToRespond.Deadline is { } deadline)
        {
            var remaining = deadline - sinceLastChange;
            return remaining.TotalSeconds > 0
                ? $"{remaining.Humanize()} remaining"
                : $"{remaining.Humanize()} past deadline";
        }

        return convo.State switch
        {
            ConversationState.New => $"Posted {convo.Created.Humanize()}",
            // Overdue is not likely here, because there should be a TimeToRespond. But just in case,
            // Maybe we allow removing an SLO and didn't remove the overdue state of an existing conversation.
            ConversationState.NeedsResponse or ConversationState.Overdue => $"Last message {convo.LastStateChangeOn.Humanize()}", // Can't look at LastMessagePostedOn because the message could have been posted by the customer, not the first responder.
            ConversationState.Waiting =>
                $"Responded to {(DateTime.UtcNow - convo.LastStateChangeOn).Humanize()} ago",
            ConversationState.Snoozed =>
                $"Snoozed {(DateTime.UtcNow - convo.LastStateChangeOn).Humanize()} ago",
            ConversationState.Closed => $"Closed {convo.ClosedOn.Humanize()}",
            ConversationState.Archived => $"Archived {convo.ArchivedOn.Humanize()}",
            ConversationState.Unknown => throw new UnreachableException(),
            _ => throw new UnreachableException(),
        };
    }

    static ThresholdStatus GetThresholdStatus(Conversation convo, IClock clock) => convo.State switch
    {
        ConversationState.Overdue => ThresholdStatus.Deadline,
        ConversationState.New => EvaluateThreshold(convo.Room.TimeToRespond, clock.UtcNow - convo.Created),
        ConversationState.Waiting or ConversationState.Snoozed or ConversationState.Closed or ConversationState.Archived => ThresholdStatus.Ok,
        ConversationState.NeedsResponse => EvaluateThreshold(convo.Room.TimeToRespond, clock.UtcNow - convo.LastStateChangeOn),
        _ => throw new UnreachableException(),
    };

    static ThresholdStatus EvaluateThreshold<T>(Threshold<T>? threshold, T value)
        where T : struct, IComparable<T> => threshold switch
        {
            null => ThresholdStatus.Unevaluated,
            { Deadline: { } d } when value.CompareTo(d) >= 0 => ThresholdStatus.Deadline,
            { Warning: { } w } when value.CompareTo(w) >= 0 => ThresholdStatus.Warning,
            _ => ThresholdStatus.Ok,
        };
}

public enum ThresholdStatus
{
    Unevaluated,
    Ok,
    Warning,
    Deadline
}

public static class ThresholdStatusExtensions
{
    public static string ToFontAwesomeIcon(this ThresholdStatus status, string ok = "fa-check-circle", string warning = "fa-exclamation-triangle", string deadline = "fa-times-circle", string unevaluated = "fa-clock") => status switch
    {
        ThresholdStatus.Deadline => deadline,
        ThresholdStatus.Warning => warning,
        ThresholdStatus.Ok => ok,
        _ => unevaluated,
    };

    public static PillColor ToPillColor(this ThresholdStatus status, PillColor ok = PillColor.Green, PillColor warning = PillColor.Yellow, PillColor deadline = PillColor.Red, PillColor unevaluated = PillColor.Gray) => status switch
    {
        ThresholdStatus.Deadline => deadline,
        ThresholdStatus.Warning => warning,
        ThresholdStatus.Ok => ok,
        _ => unevaluated,
    };
}
