using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Controllers;

public class UsersController : SkillRunnerApiControllerBase
{
    static readonly ILogger<UsersController> Log = ApplicationLoggerFactory.CreateLogger<UsersController>();
    readonly ISlackApiClient _apiClient;
    readonly ISlackResolver _slackResolver;

    public UsersController(
        ISlackApiClient apiClient,
        ISlackResolver slackResolver)
    {
        _apiClient = apiClient;
        _slackResolver = slackResolver;
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetAsync(string userId)
    {
        if (!Skill.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return Unauthorized();
        }

        var response = await _apiClient.GetUserProfileAsync(apiToken, userId);
        return response.Ok
            ? new ObjectResult(await CreateUserDetailsAsync(userId, response.Body, Skill.Organization))
            : Problem(
                type: ProblemTypes.FromSlack(response.Error),
                detail: $"Error from Slack: {response.Error}",
                title: "Error from Slack");
    }

    async Task<UserDetails> CreateUserDetailsAsync(string userId, UserProfile profile, Organization organization)
    {
        var customFields =
            profile.Fields is null
                ? new Dictionary<string, UserProfileField>()
                : profile.Fields
                    .Select(p => new UserProfileField()
                    {
                        Id = p.Key,
                        Value = p.Value.Value ?? "",
                        Alt = p.Value.Alt,
                    })
                    .ToDictionary(f => f.Id);

        // Resolve the member
        var member = await _slackResolver.ResolveMemberAsync(userId, organization);
        if (member is null)
        {
            return new UserDetails(
                userId,
                profile.DisplayName ?? "",
                profile.DisplayName ?? "",
                profile.Email ?? "",
                customFields: customFields);
        }

        return new UserDetails(member.User.PlatformUserId,
            member.User.DisplayName,
            member.User.DisplayName,
            member.User.Email,
            member.TimeZoneId,
            member.FormattedAddress,
            member.Location?.X,
            member.Location?.Y,
            member.WorkingHours,
            customFields);
    }
}
