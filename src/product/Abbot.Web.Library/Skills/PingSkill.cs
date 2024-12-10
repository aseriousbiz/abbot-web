using System.Collections.Generic;
using System.Threading;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Skills;

[Skill(Description = "Make sure the bot is alive, well, and responding.")]
public sealed class PingSkill : ISkill
{
    readonly AbbotOptions _abbotOptions;

    public PingSkill(IOptions<AbbotOptions> abbotOptions)
    {
        _abbotOptions = abbotOptions.Value;
    }

    public async Task OnMessageActivityAsync(
        MessageContext messageContext,
        CancellationToken cancellationToken)
    {
        var meta = typeof(PingSkill).Assembly.GetBuildMetadata();
        var text = "Pong!";
        if (messageContext.Organization.IsStaffOrganization(_abbotOptions.StaffOrganizationId.Require()))
        {
#pragma warning disable CS0436
            text += $" _({meta.CommitId})_";
#pragma warning restore
        }

        var reply = MessageFactory.Text(text);
        reply.Attachments = new List<Attachment>
        {
            new()
            {
                Name = "Pong",
                ContentType = "image/gif",
                ContentUrl = "https://media.giphy.com/media/aTGwuEFyg6d8c/giphy.gif"
            }
        };
        await messageContext.SendActivityAsync(reply);
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
    }
}
