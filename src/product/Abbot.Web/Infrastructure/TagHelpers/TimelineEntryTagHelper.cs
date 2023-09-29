using System;
using System.Text.Encodings.Web;
using Humanizer;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.Infrastructure.TagHelpers;

[HtmlTargetElement("timeline-entry")]
public class TimelineEntryTagHelper : TagHelper
{
    readonly HtmlEncoder _htmlEncoder;

    public string Icon { get; set; } = "star";
    public Member? Actor { get; set; }
    public Organization? ViewerOrganization { get; set; }
    public bool StaffOnly { get; set; }

    public TimelineEntryTagHelper(HtmlEncoder htmlEncoder)
    {
        _htmlEncoder = htmlEncoder;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass("flex", _htmlEncoder);
        output.AddClass("flex-grow", _htmlEncoder);
        output.AddClass("items-center", _htmlEncoder);
        output.AddClass("-ml-2.5", _htmlEncoder);
        output.AddClass("py-2", _htmlEncoder);
        output.Attributes.Add("style", "transform: translateX(-1px);");

        RenderIcon(output);
        RenderAvatar(output);

        var bgClass = StaffOnly
            ? " bg-yellow-200"
            : string.Empty;
        output.PreContent.AppendHtml($"<div class=\"pl-2 pr-4 py-4 rounded-lg flex flex-grow items-center{bgClass}\">");
        output.PreContent.AppendHtml("<p>");

        // Content goes here! We switch to PostContent now.

        if (StaffOnly)
        {
            output.PostContent.AppendHtml(
                @"<span data-tooltip=""You can see this event because you're staff""><i class=""fa-regular fa-id-badge text-yellow-900 ml-1""></i></span>");
        }

        output.PostContent.AppendHtml("</p>");

        output.PostContent.AppendHtml("</div>");
    }

    void RenderIcon(TagHelperOutput output)
    {
        output.PreContent.AppendHtml(@"<span class=""bg-white border-4 border-white flex-shrink-0"">");
        output.PreContent.AppendHtml($@"<i class=""fa-regular fa-{Icon} text-gray-400 h-4 w-4 -ml-0.5 mr-1""></i>");

        output.PreContent.AppendHtml("</span>");
    }

    void RenderAvatar(TagHelperOutput output)
    {
        if (Actor is null)
        {
            return;
        }

        // We can't easily use the avatar tag helper here, so we just copy the raw HTML... it's not ideal but it works.

        output.PreContent.AppendHtml(@"<div class=""flex-shrink-0 relative inline-block"">");
        output.PreContent.AppendHtml(
            "<img " +
            "class=\"inline-block object-cover w-8 h-8 mr-2 border-2 border-white rounded-full\" " +
            $"src=\"{Actor.User.Avatar}\" " +
            $"alt=\"{Actor.DisplayName}\" " +
            $"title=\"{Actor.DisplayName}\">");

        if (Actor.IsGuest || (ViewerOrganization is not null && ViewerOrganization.Id != Actor.OrganizationId))
        {
            var (badgeUrl, badgeAlt) = Actor.IsGuest
                ? (Avatar.Guest.Url, "Guest User")
                : (Actor.Organization.Avatar, Actor.Organization.Name);
            output.PreContent.AppendHtml(
                "<img " +
                "class=\"absolute -bottom-1 right-0 inline-block w-5 h-5 border-2 bg-white border-white rounded-full\" " +
                $"src=\"{badgeUrl}\" " +
                $"alt=\"{badgeAlt}\" " +
                $"title=\"{badgeAlt}\">");
        }

        output.PreContent.AppendHtml("</div>");
    }
}
