using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MassTransit;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Option = Serious.Abbot.Playbooks.Option;

namespace Serious.Abbot.PayloadHandlers;

public class PlaybookSelectResponseHandler : IHandler
{
    readonly IPublishEndpoint _publishEndpoint;

    public PlaybookSelectResponseHandler(IPublishEndpoint publishEndpoint)
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
                    .SetSelectionResponse(buttonElement.Value, buttonElement.Text.Text)
                    .Outputs,
            });
    }

    public static MessageRequest RenderRequest(StepContext context, string actionsBlockId, string message, string channel, IReadOnlyList<Option> options)
    {
        static string Context(StepContext context, int i)
        {
            return new Arguments(context.PlaybookRun.CorrelationId, context.ActionReference, i);
        }

        var actions = new Actions(actionsBlockId,
            // ReSharper disable once CoVariantArrayConversion
            options
                .Select((o, i) => new ButtonElement(o.Label, o.Value)
                {
                    ActionId = InteractionCallbackInfo.For<PlaybookSelectResponseHandler>(Context(context, i)),
                })
                .ToArray());

        return new()
        {
            Text = message,
            Channel = channel,
            Blocks = new ILayoutBlock[]
            {
                new Section(new MrkdwnText(message)),
                actions,
            },
        };
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
