using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messaging;
using Serious.Abbot.Security;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.PayloadHandlers;

public class AdminModalHandler : IHandler
{
    static readonly ILogger<AdminModalHandler> Log = ApplicationLoggerFactory.CreateLogger<AdminModalHandler>();

    readonly ISlackResolver _slackResolver;
    readonly IRoleManager _roleManager;

    public AdminModalHandler(ISlackResolver slackResolver, IRoleManager roleManager)
    {
        _slackResolver = slackResolver;
        _roleManager = roleManager;
    }

    /// <summary>
    /// Handle the submission of the Add Administrators modal.
    /// </summary>
    /// <param name="viewContext">The incoming view context.</param>
    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        var state = viewContext.Payload.View.State
                    .Require("No state in payload");

        var usersMultiSelectMenu = state.GetAs<UsersMultiSelectMenu>(BlockIds.AdminsInput, ActionIds.AdminsSelectMenu);
        Expect.True(usersMultiSelectMenu.SelectedValues.Count > 0,
            "This should not be possible because Slack validates that the user selected at least one user.");

        var resultingAdmins = new List<Member>();
        var actor = viewContext.FromMember;
        List<string> unResolvableIds = new List<string>();

        if (usersMultiSelectMenu.SelectedValues.All(id => id != actor.User.PlatformUserId))
        {
            viewContext.ReportValidationErrors(
                BlockIds.AdminsInput,
                "Please do not remove yourself.");
        }

        foreach (var userId in usersMultiSelectMenu.SelectedValues)
        {
            var admin = await _slackResolver.ResolveMemberAsync(userId, viewContext.Organization);

            if (admin is null)
            {
                unResolvableIds.Add(userId);
            }
            else
            {
                if (admin.IsAbbot())
                {
                    viewContext.ReportValidationErrors(
                        BlockIds.AdminsInput,
                        $"{viewContext.Organization.BotName ?? admin.DisplayName} is willing, but unable to be an Administrator.");
                }
                else if (admin.User.IsBot)
                {
                    viewContext.ReportValidationErrors(
                        BlockIds.AdminsInput,
                        $"{admin.DisplayName.Titleize()} is a bot and cannot be an Administrator.");
                }
                else
                {
                    resultingAdmins.Add(admin);
                }
            }
        }

        if (unResolvableIds.Any())
        {
            var badUserIdList = unResolvableIds.Humanize();
            // This is for our benefit.
            Log.ErrorAddingAdministrators(badUserIdList, viewContext.Organization.PlatformId);
        }

        if (viewContext.HasValidationErrors)
        {
            // Validation errors will be reported in the Modal.
            return;
        }

        if (resultingAdmins.Any())
        {
            var existingAdmins = await _roleManager.GetMembersInRoleAsync(
                Roles.Administrator,
                viewContext.Organization);

            var adminsToAdd = resultingAdmins.Where(admin => !existingAdmins.ContainsEntity(admin)).ToList();
            var adminsToRemove = existingAdmins.Where(admin => !resultingAdmins.ContainsEntity(admin)).ToList();
            // Add new admins that aren't already admins.
            foreach (var adminToAdd in adminsToAdd)
            {
                await _roleManager.AddUserToRoleAsync(
                    adminToAdd,
                    Roles.Administrator,
                    actor);
            }

            // Remove admins not in the resulting list.
            foreach (var adminToRemove in adminsToRemove)
            {
                await _roleManager.RemoveUserFromRoleAsync(
                    adminToRemove,
                    Roles.Administrator,
                    actor);
            }

            var addedAdminMentions = adminsToAdd.Select(a => a.ToMention()).Humanize();
            var addedConjugation = adminsToAdd.Count is 1 ? "is" : "are";
            var addedMessage = adminsToAdd.Any()
                ? $"{addedAdminMentions} {addedConjugation} now in the Administrators role"
                : "";
            var removedConjugation = adminsToRemove.Count is 1 ? "is" : "are";
            var removedMessage = adminsToRemove.Any()
                ? $"{adminsToRemove.Select(a => a.ToMention()).Humanize()} {removedConjugation} no longer in the Administrators role"
                : "";

            var message = (addedMessage, removedMessage) switch
            {
                ({ Length: 0 }, { Length: 0 }) => "No changes were made.",
                ({ Length: > 0 }, { Length: 0 }) => $"{addedMessage} for this Abbot instance.",
                ({ Length: 0 }, _) => $"{removedMessage} for this Abbot instance.",
                (_, _) => $"{addedMessage} and {removedMessage} for this Abbot instance.",
            };
            await viewContext.SendDirectMessageAsync(message);
        }
        else
        {
            await viewContext.SendDirectMessageAsync("An unexpected error occurred. The Abbot team has been notified.");
        }
    }

    public static ViewUpdatePayload CreateAdministratorsModal(
        IEnumerable<Member> existingAdmins,
        bool showAppHomeTabMessage = false)
    {
        var blocks = new List<ILayoutBlock>
        {
            new Section(new MrkdwnText(
                "Please select Administrators for this Abbot instance to help share the load.")),
            new Input(
                "Administrators",
                new UsersMultiSelectMenu
                {
                    ActionId = ActionIds.AdminsSelectMenu,
                    InitialUsers = existingAdmins.Select(m => m.User.PlatformUserId).ToList()
                },
                BlockIds.AdminsInput)
        };

        if (showAppHomeTabMessage)
        {
            blocks.Insert(1, new Section(new MrkdwnText("At any time you can manage the members of the Administrators role in the Home tab above.")));
        }
        return new ViewUpdatePayload
        {
            Title = "Administrators",
            Submit = "Save Administrators",
            CallbackId = new InteractionCallbackInfo(nameof(AdminModalHandler)),
            Blocks = blocks
        };
    }

    public static class BlockIds
    {
        public const string AdminsInput = nameof(AdminsInput);
    }

    public static class ActionIds
    {
        public const string AdminsSelectMenu = nameof(AdminsSelectMenu);
    }
}

public static partial class AdminModalHandlerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Could not resolve {UserIds} when adding Administrators to {PlatformId}.")]
    public static partial void ErrorAddingAdministrators(
        this ILogger<AdminModalHandler> logger,
        string? userIds,
        string platformId);
}
