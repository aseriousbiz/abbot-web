using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Repositories;
using Serious.Slack;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.SlackApp;

public class RoomsModel : SlackAppPageBase
{
    readonly ISlackIntegration _slackIntegration;

    public RoomsModel(IIntegrationRepository integrationRepository, ISlackIntegration slackIntegration)
        : base(integrationRepository)
    {
        _slackIntegration = slackIntegration;
    }

    public IReadOnlyList<(ConversationInfoItem, bool?)>? Rooms { get; private set; }

    [BindProperty]
    public IList<string> RoomIds { get; set; } = Array.Empty<string>();

    public async Task OnGetAsync()
    {
        Rooms = await _slackIntegration.GetRoomMembershipAsync(Settings);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Settings.DefaultAuthorization is not { ApiToken.Empty: false }
            || Settings.Authorization is not { BotUserId.Length: > 0 })
        {
            return RedirectWithStatusMessage("Could not invite without default and custom Slack installed.");
        }

        var successCount = await _slackIntegration.InviteUserToRoomsAsync(
            Settings.DefaultAuthorization.ApiToken,
            Settings.Authorization.BotUserId,
            RoomIds);
        return RedirectWithStatusMessage($"Invited @{CustomBotName} to {"room".ToQuantity(successCount)}.");
    }
}
