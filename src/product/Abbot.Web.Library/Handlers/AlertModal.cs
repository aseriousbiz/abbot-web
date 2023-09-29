using System.Collections.Generic;
using Serious.Abbot.Events;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.PayloadHandlers;

public class AlertModal : IHandler
{
    public static ViewUpdatePayload Render(string message, string title)
    {
        var blocks = new List<ILayoutBlock>
        {
            new Section(new MrkdwnText(message)),
        };
        var payload = new ViewUpdatePayload
        {
            CallbackId = InteractionCallbackInfo.For<AlertModal>(),
            Title = title,
            Close = "Back",
            Blocks = blocks,
        };

        return payload;
    }
}
