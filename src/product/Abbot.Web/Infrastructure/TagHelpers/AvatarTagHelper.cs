using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.AspNetCore.TagHelpers;

namespace Serious.Abbot.Infrastructure.TagHelpers;

public enum AvatarSize
{
    Small,
    Medium
}

[HtmlTargetElement("avatar", TagStructure = TagStructure.WithoutEndTag)]
public class AvatarTagHelper : TagHelper
{
    readonly HtmlEncoder _htmlEncoder;

    /// <summary>
    /// The <see cref="Organization"/> to render the avatar for.
    /// </summary>
    public Organization? Organization { get; set; }

    /// <summary>
    /// The <see cref="Member"/> to render the avatar for.
    /// </summary>
    public Member? Member { get; set; }

    /// <summary>
    /// The <see cref="User"/> to render the avatar for, if a <see cref="Member"/> is not available.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// The <see cref="Organization"/> of the viewer.
    /// If <c>null</c>, a foreign-org badge cannot be displayed even if <see cref="ShowForeignOrgBadge"/> is <c>true</c>
    /// </summary>
    public Organization? ViewerOrganization { get; set; }

    /// <summary>
    /// Show a smaller icon for the member's org in the bottom right of the avatar, if they aren't in the viewer's organization or are a guest.
    /// Defaults to <c>true</c>
    /// </summary>
    public bool ShowForeignOrgBadge { get; set; } = true;

    /// <summary>
    /// Gets or sets the AvatarSize and sets styles accordingly.
    /// </summary>
    public AvatarSize Size { get; set; } = AvatarSize.Medium;

    public AvatarTagHelper(HtmlEncoder htmlEncoder)
    {
        _htmlEncoder = htmlEncoder;
    }

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // This should be self-closing/empty
        output.SuppressOutput();

        if (Member is not null)
        {
            if (ShowForeignOrgBadge && (Member.IsGuest || (ViewerOrganization is not null && ViewerOrganization.Id != Member.OrganizationId)))
            {
                var (badgeUrl, badgeAlt) = Member.IsGuest
                    ? (Avatar.Guest.Url, "Guest User")
                    : (Member.Organization.Avatar, Member.Organization.Name);
                RenderAvatars(context, output,
                    Member.User.Avatar, Member.DisplayName,
                    badgeUrl, badgeAlt);
            }
            else
            {
                RenderAvatars(context, output, Member.User.Avatar, Member.DisplayName);
            }

            return Task.CompletedTask;
        }

        if (User is not null)
        {
            RenderAvatars(context, output, User.Avatar, User.DisplayName);
        }

        if (Organization is not null)
        {
            RenderAvatars(context, output,
                Organization.Avatar, Organization.Name);
        }

        return Task.CompletedTask;
    }

    void RenderAvatars(TagHelperContext context, TagHelperOutput output, string? avatarUrl, string? avatarAlt, string? badgeUrl = null, string? badgeAlt = null)
    {
        if (avatarUrl is not { Length: > 0 } || avatarAlt is not { Length: > 0 })
        {
            return;
        }

        var avatarSizeClasses = Size switch
        {
            AvatarSize.Small => "w-5 h-5",
            AvatarSize.Medium => "w-8 h-8",
            _ => throw new UnreachableException(),
        };

        var orgAvatarSizeClasses = Size switch
        {
            AvatarSize.Small => "w-3 h-3 -bottom-1.5 -right-1",
            AvatarSize.Medium => "w-5 h-5 -bottom-1.5 -right-1.5",
            _ => throw new UnreachableException(),
        };

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AppendAttributeValue("class", $"relative inline-block shrink-0 {avatarSizeClasses}", context);

        // Sanitize inputs
        avatarUrl = _htmlEncoder.Encode(avatarUrl);
        avatarAlt = _htmlEncoder.Encode(avatarAlt);

        // Main avatar image
        output.Content.AppendHtml(
            "<img " +
            $"class=\"inline-block object-cover {avatarSizeClasses} shrink-0 mr-2 border-1 border-white rounded-full\" " +
            $"src=\"{avatarUrl}\" " +
            $"alt=\"{avatarAlt}\" " +
            $"title=\"{avatarAlt}\">");

        if (badgeUrl is { Length: > 0 } && badgeAlt is { Length: > 0 })
        {
            // Sanitize inputs
            badgeUrl = _htmlEncoder.Encode(badgeUrl);
            badgeAlt = _htmlEncoder.Encode(badgeAlt);

            // Org sub-badge
            output.Content.AppendHtml(
                "<img " +
                $"class=\"absolute {orgAvatarSizeClasses} inline-block border bg-white border-white rounded-full\" " +
                $"src=\"{badgeUrl}\" " +
                $"alt=\"{badgeAlt}\" " +
                $"title=\"{badgeAlt}\">");
        }
    }
}
