using System;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Models.Api;

public record OrganizationResponseModel(
    int Id,
    string PlatformOrganizationId,
    PlatformType PlatformType,
    string? Name,
    bool? IsLocal,
    Uri? AvatarUri)
{
    public static OrganizationResponseModel Create(Organization org, Member? viewer = null) =>
        new(
            org.Id,
            org.PlatformId,
            org.PlatformType,
            org.Name,
            viewer is not null
                ? viewer.Organization.Id == org.Id
                : null,
            org.Avatar is { Length: > 0 } avatar
                ? new Uri(avatar)
                : null);
}
