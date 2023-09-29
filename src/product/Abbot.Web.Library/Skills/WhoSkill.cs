using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NetTopologySuite.Geometries;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Services.DefaultResponder;

namespace Serious.Abbot.Skills;

[Skill(Description = "Builds the story of a person by accumulating bits of fun information about that person. Also used to list who has permissions to a skill.")]
public sealed class WhoSkill : ISkill
{
    readonly IMemberFactRepository _memberFactRepository;
    readonly IPermissionRepository _permissions;
    readonly ISkillRepository _skillRepository;
    readonly IUserRepository _userRepository;
    readonly IGeocodeService _geocodeService;
    readonly IDefaultResponderService _defaultResponderService;
    readonly IClock _clock;

    public WhoSkill(
        IMemberFactRepository memberFactRepository,
        IPermissionRepository permissions,
        ISkillRepository skillRepository,
        IUserRepository userRepository,
        IGeocodeService geocodeService,
        IDefaultResponderService defaultResponderService,
        IClock clock)
    {
        _memberFactRepository = memberFactRepository;
        _permissions = permissions;
        _skillRepository = skillRepository;
        _userRepository = userRepository;
        _geocodeService = geocodeService;
        _defaultResponderService = defaultResponderService;
        _clock = clock;
    }

    public async Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var (verb, mention, description) = messageContext.Arguments;

        var action = verb.Value;
        // Rearrange parameters for negation.
        if (verb is { Value: "is" } && mention is { Value: "not" })
        {
            action = "is not";
            (mention, description) = messageContext.Arguments.Skip(2);
        }

        // Make sure the first mentioned is in the same org as the caller.
        // We don't care about subsequent mentions because they aren't the subject of this skill.
        var mentions = messageContext.Mentions;
        if (mentions.Count > 0 && mentions[0].OrganizationId != messageContext.Organization.Id)
        {
            await ReportUnknownUser(mention, messageContext);
            return;
        }

        await ((action, mention, description) switch
        {
            ("is not", IMentionArgument mentionArgument, var descriptionArg) => RemoveInformationAboutUserAndReply(mentionArgument, descriptionArg.Value, messageContext),
            ("is not", var mentionArgument, _) => ReportUnknownUser(mentionArgument, messageContext),
            ("am", { Value: "i" } or { Value: "i?" } or { Value: "I" } or { Value: "I?" }, IMissingArgument) => ReplyWithCurrentUserName(messageContext),
            ("is", { Value: "nearby" } or { Value: "nearby?" }, IMissingArgument) => ReplyWithNearbyPeople(messageContext),
            ("is", { Value: "near" }, { Value: "me" } or { Value: "me?" }) => ReplyWithNearbyPeople(messageContext),
            ("is", { Value: "near" }, var location) => ReplyWithNearbyPeople(messageContext, location),
            ("is", IMentionArgument user, IMissingArgument) => ReplyWithWhatBotKnowsAboutUser(user, false, messageContext),
            ("list", IMentionArgument user, IMissingArgument) => ReplyWithWhatBotKnowsAboutUser(user, true, messageContext),
            ("is", IMissingArgument _, _) => ReportHelp(messageContext),
            ("is", IMentionArgument user, var value) => AddInformationAboutUserAndReply(user, value.Value, messageContext),
            ("list", var user, IMissingArgument) => ReportUnknownUser(user, messageContext),
            ("can", var skill, IMissingArgument) => ReplyWithPermissionsForSkill(skill.Value, messageContext),
            ("help", _, _) => messageContext.SendHelpTextAsync(this),
            ({ Length: > 0 }, _, _) => UseDefaultResponder(messageContext),
            _ => messageContext.SendHelpTextAsync(this)
        });
    }

    async Task UseDefaultResponder(MessageContext messageContext)
    {
        var defaultResponse = await _defaultResponderService.GetResponseAsync(
            messageContext.CommandText,
            messageContext.FromMember.FormattedAddress,
            messageContext.FromMember,
            messageContext.Organization);
        await messageContext.SendActivityAsync(defaultResponse);
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.AddAlternativeUsage("@Username is {something}", "remembers {something} about the user");
        usage.AddAlternativeUsage("@Username is not {something}", "forgets {something} about the user");
        usage.Add("is @UserName", "returns everything I know about the mentioned user");
        usage.Add("{question}", "tries to answer the question if nobody is mentioned. For example, `@abbot who is George Washington?` or `@abbot who was Marie Curie?`");
        usage.Add("is near me", "returns a list of people near you");
        usage.Add("is near {location}", "returns a list of people near {location}");
        usage.Add("list @UserName", "returns a list of things I know about the mentioned user and who added those things");
        usage.Add("can {skill}", "returns a list of people with permissions to {skill}");
    }

    static Task ReportHelp(MessageContext messageContext)
    {
        return messageContext.SendActivityAsync(
            $"Who is? Did you forget to complete the question? Say `{messageContext.Bot} help who` to learn how `who` works.");
    }

    static Task ReportUnknownUser(IArgument user, MessageContext messageContext)
    {
        return messageContext.SendActivityAsync(
            $"I don’t believe {user.Value} is even a person in this organization.");
    }

    static Member GetMentionedMember(IMentionArgument mentionArgument, MessageContext messageContext)
    {
        var mentioned = mentionArgument.Mentioned;
        return messageContext.Mentions.Single(m => m.User.PlatformUserId == mentioned.Id);
    }

    async Task AddInformationAboutUserAndReply(
        IMentionArgument mention,
        string value,
        MessageContext messageContext)
    {
        var mentioned = GetMentionedMember(mention, messageContext);

        var facts = await _memberFactRepository.GetFactsAsync(mentioned);
        var foundItem = GetFact(facts, value);
        if (foundItem is not null)
        {
            // EASTER EGG FOR US.
            var response = messageContext.Organization.IsSerious()
                ? "https://media.giphy.com/media/s3tpyHuSSr98A/giphy.gif"
                : "I know.";
            await messageContext.SendActivityAsync(response);
            return;
        }

        value = CleanFact(value);

        var fact = new MemberFact
        {
            Content = value,
            Created = _clock.UtcNow,
            Subject = mentioned
        };
        await _memberFactRepository.CreateAsync(fact, messageContext.From);

        await messageContext.SendActivityAsync(
            $"OK, {mentioned.FormatMention()} is {value}.");
    }

    static string CleanFact(string value)
    {
        // Remove trailing period from value, but not if its preceded by a period.
        return value.Length > 2 && value.EndsWith('.') && !value.EndsWith("..", StringComparison.Ordinal)
            ? value[..^1]
            : value;
    }

    static async Task ReplyWithCurrentUserName(MessageContext messageContext)
    {
        var reply = $"I thought you would know that by now. You are {messageContext.From.FormatMention()}.";
        await messageContext.SendActivityAsync(reply);
    }

    async Task RemoveInformationAboutUserAndReply(
        IMentionArgument mentionArgument,
        string value,
        MessageContext messageContext)
    {
        var mentioned = GetMentionedMember(mentionArgument, messageContext);

        var facts = await _memberFactRepository.GetFactsAsync(mentioned);
        if (!facts.Any())
        {
            await messageContext.SendActivityAsync(
                $"I don’t know anything about {mentioned.FormatMention()}, " +
                $"so there’s nothing to forget. Say `{messageContext.Bot} {mentioned.FormatMention()} " +
                "is something` to tell me something about that human.");
            return;
        }

        var fact = GetFact(facts, value);
        if (fact is null)
        {
            // No match, let's see if there's similar items.
            var similarItems = facts
                .Select(item => item.Content)
                .WhereFuzzyMatch(value)
                .Select(item => $"`{item}`")
                .ToList();
            if (similarItems.Any())
            {
                await messageContext.SendActivityAsync(
                    @$"That may be true, but did you mean to say {mentioned.FormatMention()} is not one of these things?
{similarItems.ToMarkdownList()}");
                return;
            }

            await messageContext.SendActivityAsync("I did not know that.");
            return;
        }

        await _memberFactRepository.RemoveAsync(fact, messageContext.From);

        await messageContext.SendActivityAsync($"OK, {mentioned.FormatMention()} is not {value}.");
    }

    async Task ReplyWithWhatBotKnowsAboutUser(IMentionArgument mentionArgument, bool asList, MessageContext messageContext)
    {
        var mentioned = GetMentionedMember(mentionArgument, messageContext);

        var facts = await _memberFactRepository.GetFactsAsync(mentioned);
        if (!facts.Any())
        {
            await messageContext.SendActivityAsync(
                $"I don’t know anything about {mentioned.FormatMention()}. " +
                $"Say `{messageContext.Bot} {mentioned.FormatMention()} is something` " +
                "to tell me something about that human.");
            return;
        }

        var known = asList
            ? facts.ToDetailedMarkdownList()
            : string.Join(", ", facts.Select(f => CleanFact(f.Content)));

        var reply = asList
            ? $"This is what we know about {mentioned.FormatMention()}:\n{known}"
            : $"{mentioned.FormatMention()} is {known}.";
        await messageContext.SendActivityAsync(reply);
    }

    async Task ReplyWithPermissionsForSkill(string skillArg, MessageContext messageContext)
    {
        var skill = skillArg.TrimTrailingCharacter('?');
        var dbSkill = await _skillRepository.GetAsync(skill, messageContext.Organization);
        if (dbSkill is null)
        {
            await messageContext.SendActivityAsync($"{skill} does not exist.");
            return;
        }

        if (!dbSkill.Restricted)
        {
            await messageContext.SendActivityAsync($"`{skill}` is not restricted so EVERYBODY can!");
            return;
        }

        var permissions = await _permissions.GetPermissionsForSkillAsync(dbSkill);
        if (permissions.Count == 0)
        {
            await messageContext.SendActivityAsync($"There are no permissions set on `{skill}`. " +
                                                   $"Only members of the `Administrator` group have permissions to it. `{messageContext.Bot} help can` to learn how to give permissions to skills.");
            return;
        }

        var permissionList = permissions.GroupBy(p => p.Capability)
            .Select(group => $"`{group.Key,-5}` - {group.Select(m => m.Member).Select(m => m.FormatMention()).ToCommaSeparatedList()}")
            .ToMarkdownList() + "\nThis list does not include the members of the `Administrator` group who have full permissions to all skills.";
        await messageContext.SendActivityAsync(permissionList);
    }

    static MemberFact? GetFact(IEnumerable<MemberFact> facts, string value)
    {
        return facts.FirstOrDefault(i => i.Content.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    async Task ReplyWithNearbyPeople(MessageContext messageContext, IArgument locationArgument)
    {
        if (locationArgument is IMissingArgument)
        {
            await messageContext.SendActivityAsync(
                $"You’ll have to be more specific. You can ask `{messageContext.Bot} who is near me` " +
                $"or `{messageContext.Bot} who is near {{location}}` where {{location}} is a zip code or an address.");
            return;
        }

        var locationText = locationArgument.Value.TrimTrailingCharacter('?');

        var geocode = await _geocodeService.GetGeocodeAsync(locationText);
        if (geocode?.Coordinate is null)
        {
            await messageContext.SendActivityAsync($"Sorry, I could not figure where `{locationText}` is.");
            return;
        }

        var location = new Point(geocode.Coordinate.Latitude, geocode.Coordinate.Longitude);

        await ReplyWithPeopleNearLocation(messageContext, location, messageContext.Organization, $"`{locationText}`");
    }

    Task ReplyWithNearbyPeople(MessageContext messageContext)
    {
        var member = messageContext.FromMember;
        return member is { Location: { } }
            ? ReplyWithPeopleNearLocation(messageContext, member.Location, messageContext.Organization, "you")
            : messageContext.SendActivityAsync($"I do not know your location. Try `{messageContext.Bot} my location is {{address or zip}}` to tell me your location.");
    }

    async Task ReplyWithPeopleNearLocation(MessageContext messageContext, Point location, Organization organization, string locationName)
    {
        const double radius = 40; // 25 miles
        var nearby = await _userRepository.GetUsersNearAsync(messageContext.FromMember, location, radius, 5, organization);

        if (nearby.Count is 0)
        {
            await messageContext.SendActivityAsync("There is nobody within 25 miles of you. If there are " +
                                                   $"people nearby, they haven’t set their location by saying `{messageContext.Bot} my location is _location_` with their location.");
            return;
        }

        var subject = nearby.TotalCount switch
        {
            1 => "is one person",
            _ => $"are {nearby.TotalCount} people"
        };
        var suffix = nearby.TotalCount > nearby.Count
            ? $". Here are {nearby.Count} of them!\n"
            : nearby.TotalCount is 1
                ? ": "
                : ".\n";

        var reply = $"There {subject} within 25 miles of {locationName}{suffix}{nearby.ToCommaSeparatedList()}";
        await messageContext.SendActivityAsync(reply);
    }
}
