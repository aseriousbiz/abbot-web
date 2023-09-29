using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Messages;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a run of a playbook.
/// </summary>
public class PlaybookRun : EntityBase<PlaybookRun>, SagaStateMachineInstance
{
    /// <inheritdoc/>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or inits the ID of the <see cref="Entities.Playbook"/> for this run.
    /// </summary>
    public int PlaybookId { get; init; }

    /// <summary>
    /// Gets or inits the <see cref="Entities.Playbook"/> for this run.
    /// </summary>
    public required Playbook Playbook { get; init; }

    /// <summary>
    /// Gets or sets the version number of <see cref="Playbook"/> for this run.
    /// </summary>
    /// <remarks>
    /// This should be created from a <see cref="PlaybookVersion"/>,
    /// but uses <see cref="Entities.Playbook"/> for the foreign key in case we allow deleting old versions.
    /// </remarks>
    public required int Version { get; init; }

    /// <summary>
    /// Gets or inits the ID of the <see cref="PlaybookRunGroup"/> this playbook run belongs to.
    /// </summary>
    public int? GroupId { get; init; }

    /// <summary>
    /// Gets or inits the <see cref="PlaybookRunGroup"/> this playbook run belongs to.
    /// </summary>
    public PlaybookRunGroup? Group { get; init; }

    /// <summary>
    /// Gets or sets the current state of this run. Enum-ish; managed by MassTransit.
    /// </summary>
    public required string State { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookDefinition"/> for this run.
    /// </summary>
    [Column("Definition", TypeName = "jsonb")]
    public required string SerializedDefinition { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when this run was started.
    /// </summary>
    /// <remarks>
    /// This is set when the playbook starts executing in the state machine, not when it is first created.
    /// </remarks>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this run was completed.
    /// </summary>
    /// <remarks>
    /// This is set when the playbook reaches the final state in the state machine, regardless of outcome.
    /// </remarks>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookRunProperties"/> representing additional properties of this run of the playbook.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public required PlaybookRunProperties Properties { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookRunRelatedEntities"/> representing related entities of this run of the playbook.
    /// </summary>
    public PlaybookRunRelatedEntities? Related { get; set; }
}

[Owned]
public record PlaybookRunRelatedEntities
{
    /// <summary>
    /// Gets or sets the ID of the <see cref="Customer"/> associated with this playbook run, if any.
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Customer"/> associated with this playbook run, if any.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// Gets or sets the ID of the <see cref="Room"/> associated with this playbook run, if any.
    /// </summary>
    public int? RoomId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Room"/> associated with this playbook run, if any.
    /// </summary>
    public Room? Room { get; set; }

    /// <summary>
    /// Gets or sets the ID of the <see cref="Conversation"/> associated with this playbook run, if any.
    /// </summary>
    public int? ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Conversation"/> associated with this playbook run, if any.
    /// </summary>
    public Conversation? Conversation { get; set; }

    public static PlaybookRunRelatedEntities From(Customer customer)
    {
        return new()
        {
            Customer = customer,
        };
    }

    public static PlaybookRunRelatedEntities From(Room room)
    {
        return new()
        {
            Customer = room.Customer,
            Room = room,
        };
    }

    public static PlaybookRunRelatedEntities From(Conversation conversation)
    {
        return new()
        {
            Customer = conversation.Room.Customer,
            Room = conversation.Room,
            Conversation = conversation,
        };
    }
}

/// <summary>
/// Represents additional properties of a playbook run.
/// </summary>
public record PlaybookRunProperties
{
    /// <summary>
    /// The activity ID that started this playbook run.
    /// This ID should flow through all activities in the playbook.
    /// </summary>
    public required string ActivityId { get; init; }

    /// <summary>
    /// Indicates if cancellation has been requested and by whom.
    /// If non-<c>null</c>, the playbook will be cancelled at the next possible opportunity.
    /// </summary>
    public Id<Member>? CancellationRequestedBy { get; set; }

    /// <summary>
    /// References the current step of the playbook.
    /// </summary>
    public ActionReference? ActiveStep { get; set; }

    /// <summary>
    /// The ID of the trigger that started this playbook run.
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    /// The initiating <see cref="HttpTriggerRequest"/>, if applicable.
    /// </summary>
    public HttpTriggerRequest? TriggerRequest { get; set; }

    /// <summary>
    /// The initiating <see cref="SignalMessage"/>, if applicable.
    /// </summary>
    public SignalMessage? SignalMessage { get; set; }

    /// <summary>
    /// Represents the result of the playbook run.
    /// If <c>null</c>, the playbook is still executing.
    /// </summary>
    public PlaybookRunResult? Result { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this run was suspended.
    /// </summary>
    public DateTime? LastSuspendTime { get; set; }

    /// <summary>
    /// Gets or inits the results of each step of the playbook.
    /// </summary>
    public IDictionary<string, StepResult> StepResults { get; init; } = new Dictionary<string, StepResult>();

    /// <summary>
    /// The list of completed steps, including the trigger, in order of execution.
    /// Steps that have failed or timed-out ARE included in this list.
    /// </summary>
    public IList<ActionReference> CompletedSteps { get; init; } = new List<ActionReference>();

    /// <summary>
    /// The ID of the root audit event for this playbook run.
    /// All audit events within the playbook run will have this ID as their <see cref="AuditEventBase.ParentIdentifier"/>.
    /// </summary>
    public Guid? RootAuditEventId { get; set; }

    /// <summary>
    /// Metadata about the context in which this playbook was dispatched.
    /// For example, if it was dispatched <see cref="DispatchType.ByCustomer"/>, this will reference the customer ID.
    /// </summary>
    public DispatchContext? DispatchContext { get; set; }

    /// <summary>
    /// The call stack of the playbook.
    /// When a step suspends in order to call another sequence, that step's context is pushed onto the stack, so that it can be resumed when the sequence returns.
    /// </summary>
    // Why not use a stack, you say? Well, JSON doesn't have a native concept of a stack, so we'd have to serialize it as an array anyway.
    // I prefer to model the JSON as closely as possible to avoid any surprises.
    // Treating a list as a stack is trivial.
    public IList<PlaybookStackFrame> CallStack { get; init; } = new List<PlaybookStackFrame>();

    public DateTime? SuspendedUntil { get; set; }

    public StepResult? GetActiveStepResult()
    {
        return ActiveStep is not null && StepResults.TryGetValue(ActiveStep.ActionId, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Pops a frame off the playbook call stack, if there is one.
    /// After this method returns, the returned frame has been removed from the stack.
    /// </summary>
    /// <returns>The top frame on the call stack, or <c>null</c> if the stack is empty.</returns>
    public PlaybookStackFrame? PopFrame()
    {
        if (CallStack is [.., var frame])
        {
            CallStack.RemoveAt(CallStack.Count - 1);
            return frame;
        }

        return null;
    }

    /// <summary>
    /// Pushes a frame on to the playbook call stack.
    /// </summary>
    /// <param name="frame">The <see cref="PlaybookStackFrame" /> to push.</param>
    public void PushFrame(PlaybookStackFrame frame)
    {
        CallStack.Add(frame);
    }
}

public record PlaybookStackFrame
{
    /// <summary>
    /// An <see cref="ActionReference"/> representing the step to return to when this frame is popped from the call stack.
    /// </summary>
    public required ActionReference Step { get; init; }
}

public record DispatchContext
{
    /// <summary>
    /// The <see cref="DispatchType"/> used to trigger this run.
    /// </summary>
    public required DispatchType Type { get; init; }

    /// <summary>
    /// If the playbook was dispatched <see cref="DispatchType.ByCustomer"/>, this will reference the customer ID.
    /// </summary>
    public int? EntityId { get; init; }

    /// <summary>
    /// If the playbook was dispatched <see cref="DispatchType.ByCustomer"/>, this will reference the customer name.
    /// </summary>
    public string? EntityName { get; set; }

    public string? GetAuditDescription()
    {
        return Type switch
        {
            DispatchType.ByCustomer => $"for customer `{EntityName}`",
            _ => null,
        };
    }
}

/// <summary>
/// Contains the result of a playbook run.
/// </summary>
public record PlaybookRunResult
{
    /// <summary>
    /// The outcome of running the playbook.
    /// </summary>
    public required PlaybookRunOutcome Outcome { get; init; }

    /// <summary>
    /// Describes the error that caused the playbook to fail, if any.
    /// </summary>
    public required ProblemDetails? Problem { get; init; }
}

/// <summary>
/// Represents the outcome of a playbook run.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybookRunOutcome
{
    /// <summary>
    /// The playbook completed in an unknown state.
    /// </summary>
    Unknown,

    /// <summary>
    /// The playbook successfully ran to completion.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The playbook encountered a fault and failed.
    /// </summary>
    Faulted,

    /// <summary>
    /// The playbook was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// A step in the playbook timed out.
    /// </summary>
    TimedOut,
}
