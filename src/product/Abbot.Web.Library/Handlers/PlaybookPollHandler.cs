using System.Collections.Generic;
using System.Globalization;
using MassTransit;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Skills;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.PayloadHandlers;

public class PlaybookPollHandler : IHandler
{
    readonly IPublishEndpoint _publishEndpoint;

    public PlaybookPollHandler(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        var interactionInfo = platformMessage.Payload.InteractionInfo.Require();
        var buttonElement = interactionInfo.ActionElement.Require<ButtonElement>();
        var callbackInfo = interactionInfo.CallbackInfo.Require<InteractionCallbackInfo>();
        var context = Arguments.Parse(callbackInfo.ContextId);

        await _publishEndpoint.Publish(
            new ResumeSuspendedStep(context.PlaybookRunId, context.Step)
            {
                SuspendState = new OutputsBuilder()
                    .SetActor(platformMessage.From)
                    .SetPollResponse(buttonElement.Value, buttonElement.Text.Text)
                    .Outputs,
            });

        // TODO: Drop Actions and add Actor/Timestamp
    }

    public static string Context(StepContext context, int i)
    {
        return new Arguments(context.PlaybookRun.CorrelationId, context.ActionReference, i);
    }

    // PrivateMetadataBase uses | which conflicts with Unwrap logic
    record Arguments(Guid PlaybookRunId, ActionReference Step, int OptionIndex)
    {
        public static Arguments Parse(string? arguments)
        {
            return arguments?.Split(',', 5) is [var runId, var seqId, var actionId, var actionIndex, var optionIndex]
                ? new Arguments(
                    Guid.Parse(runId),
                    new(seqId, actionId, int.Parse(actionIndex, CultureInfo.InvariantCulture)),
                    int.Parse(optionIndex, CultureInfo.InvariantCulture))
                : throw new InvalidOperationException($"Could not parse arguments: {arguments}");
        }

        public override string ToString() => $"{PlaybookRunId},{Step.SequenceId},{Step.ActionId},{Step.ActionIndex},{OptionIndex}";

        public static implicit operator string(Arguments arguments) => arguments.ToString();
    }
}
