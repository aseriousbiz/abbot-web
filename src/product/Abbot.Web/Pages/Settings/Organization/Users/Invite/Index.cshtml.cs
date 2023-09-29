using System.Collections.Generic;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messaging;
using Serious.Slack;

namespace Serious.Abbot.Pages.Settings.Organization.Users.Invite;

public class InviteIndexPage : UserPage
{
    public static readonly DomId InviteeDomId = new("invitee");
    public static readonly DomId InviteeListDomId = InviteeDomId.WithSuffix("list");
    public static readonly DomId InviteeInputDomId = InviteeDomId.WithSuffix("input");
    public static readonly DomId SendInvitationButtonDomId = InviteeDomId.WithSuffix("button");

    readonly ISlackResolver _slackResolver;
    readonly IRoleManager _roleManager;
    readonly ISlackApiClient _slackApiClient;
    readonly IBackgroundJobClient _backgroundJobClient;

    public InviteIndexPage(
        ISlackResolver slackResolver,
        IRoleManager roleManager,
        ISlackApiClient slackApiClient,
        IBackgroundJobClient backgroundJobClient)
    {
        _slackResolver = slackResolver;
        _roleManager = roleManager;
        _slackApiClient = slackApiClient;
        _backgroundJobClient = backgroundJobClient;
    }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public IList<string> InviteeIds { get; set; } = null!;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        var apiToken = Organization.RequireAndRevealApiToken();

        if (Email is { Length: > 0 })
        {
            // Validate that email matches a slack user in this org.
            var response = await _slackApiClient.LookupUserByEmailAsync(apiToken, Email);
            if (response.Ok
                && await _slackResolver.ResolveMemberAsync(response.Body.Id, Organization, true) is { } member)
            {
                Email = string.Empty;
                ModelState.Remove(nameof(Email)); // We want to clear the input field.
                return TurboStream(
                    TurboAppend(
                        InviteeListDomId,
                        Partial("_Invitee", member)),
                    TurboUpdate(
                        InviteeInputDomId,
                        Partial("_InviteeInput", this)));
            }

            ModelState.AddModelError(nameof(Email), "Could not find a user with that email address.");
        }
        else
        {
            ModelState.AddModelError(nameof(Email), "Please enter an email address to search for.");
        }

        return TurboStream(
            TurboUpdate(
                InviteeInputDomId,
                Partial("_InviteeInput", this)));
    }

    public async Task<IActionResult> OnPostRemoveAsync([FromForm] string removeUser)
    {
        return TurboStream(
            TurboRemove(
                InviteeDomId.WithSuffix("user").WithSuffix(removeUser)),
            TurboUpdate(
                InviteeInputDomId,
                Partial("_InviteeInput", this)));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        _backgroundJobClient.Enqueue<InvitationSender>(c => c.SendInvitationAsync(
            InviteeIds,
            Viewer.User.PlatformUserId,
            Organization.Id));

        StatusMessage = "Invitations are being sent.";
        return RedirectToPage();
    }
}
