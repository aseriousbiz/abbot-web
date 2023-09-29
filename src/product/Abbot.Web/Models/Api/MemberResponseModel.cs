using System;
using System.Globalization;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models.Api;

public record MemberResponseModel(
    int Id,
    bool? IsLocal,
    string NickName,
    string SearchKey,
    string UserName,
    string PlatformUserId,
    Uri? AvatarUrl,
    string? TimeZoneId,
    OrganizationResponseModel Organization,
    WorkingHoursResponseModel? WorkingHours,
    WorkingHoursResponseModel? WorkingHoursInYourTimeZone)
{
    public static MemberResponseModel Create(Member member, Member? viewer = null, bool startsWithAtSign = false)
    {
        return new(
            member.Id,
            viewer is not null
                ? viewer.Organization.Id == member.Organization.Id
                : null,
            member.User.DisplayName,
            startsWithAtSign ? $"@{member.User.DisplayName}" : member.User.DisplayName,
            member.User.DisplayName,
            member.User.PlatformUserId,
            member.User.Avatar is { Length: > 0 } avatar
                ? new Uri(avatar)
                : null,
            member.TimeZoneId,
            OrganizationResponseModel.Create(member.Organization),
            member.TimeZoneId is { Length: > 0 }
                ? WorkingHoursResponseModel.Create(member.GetWorkingHoursOrDefault())
                : null,
            member.GetWorkingHoursInViewerTimeZone(viewer?.TimeZoneId) is { } hrs
                ? WorkingHoursResponseModel.Create(hrs)
                : null);
    }
}

public record WorkingHoursResponseModel(string Start, string End)
{
    public static WorkingHoursResponseModel Create(WorkingHours workingHours) =>
        new(workingHours.Start.ToString("HH:mm", CultureInfo.InvariantCulture), workingHours.End.ToString("HH:mm", CultureInfo.InvariantCulture));
}
