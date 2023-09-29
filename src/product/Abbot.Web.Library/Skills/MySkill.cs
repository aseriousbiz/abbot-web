using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NodaTime;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Skills;

[Skill(Description = "Tell Abbot information about yourself that can come in useful for skills.")]
public class MySkill : ISkill
{
    const string TimezoneDatabaseResponse =
        "Visit https://en.wikipedia.org/wiki/List_of_tz_database_time_zones and look at the \"TZ database name\" column for a list of valid timezone names. You can also set your timezone by telling me your location.";
    readonly IUserRepository _userRepository;
    readonly IGeocodeService _geoCodeService;
    readonly IAuditLog _auditLog;

    public MySkill(IUserRepository userRepository, IGeocodeService geoCodeService, IAuditLog auditLog)
    {
        _userRepository = userRepository;
        _geoCodeService = geoCodeService;
        _auditLog = auditLog;
    }

    public Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var (property, verb, value) = messageContext.Arguments;

        var task = (property, verb, value) switch
        {
            (_, IMissingArgument, IMissingArgument) => ReplyWithValue(messageContext, property.Value),
            (_, _, IMissingArgument) => ReplyWithUsage(messageContext),
            (_, _, _) when verb.Value == "is" => SaveFactAndReply(messageContext, property.Value, value.Value),
            _ => ReplyWithUsage(messageContext)
        };
        return task;
    }

    static Task ReplyWithUsage(MessageContext messageContext)
    {
        return messageContext.SendActivityAsync($"Try `{messageContext.Bot} help my` to learn how to use this skill.");
    }

    static Task ReplyWithValue(MessageContext messageContext, string property)
    {
        return property switch
        {
            "location" => messageContext.SendActivityAsync(FormatLocation(messageContext.FromMember, messageContext.Bot)),
            "timezone" or "tz" => ReportTimezone(messageContext, property),
            "email" => ReportEmail(messageContext),
            _ => messageContext.SendActivityAsync(
                $"I do not support retrieving the `{property}` property. Only `timezone` (or `tz`) is supported.")
        };
    }

    static Task ReportTimezone(MessageContext messageContext, string property)
    {
        var timezoneId = messageContext.FromMember.TimeZoneId;
        if (timezoneId is null)
        {
            return messageContext.SendActivityAsync(
                $"I do not know your {property}. `{messageContext.Bot} my {property} is {{value}}` to set it. {TimezoneDatabaseResponse}");
        }

        var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timezoneId);
        if (tz is null)
        {
            return messageContext.SendActivityAsync(
                $"Your {property} is set to an invalid value, `{timezoneId}`.  Use `{messageContext.Bot} my {property} is {{value}}` to set it. {TimezoneDatabaseResponse}");
        }

        return messageContext.SendActivityAsync($"Your {property} is {tz.Id}.");
    }

    Task SaveFactAndReply(MessageContext messageContext, string property, string value)
    {
        return property switch
        {
            "location" => SaveLocationAndReply(messageContext, value),
            "timezone" or "tz" => SaveTimezoneAndReply(messageContext, value),
            "email" => SaveEmailAndReply(messageContext, value),
            _ => messageContext.SendActivityAsync($"I do not support setting the `{property}` property.")
        };
    }

    static Task ReportEmail(MessageContext messageContext)
    {
        var email = messageContext.From.Email;
        return messageContext.SendActivityAsync(email
                                                ?? $"I do not know your email. `{messageContext.Bot} my {{email}} is {{value}}` to set it.");
    }

    async Task SaveLocationAndReply(MessageContext messageContext, string value)
    {
        var geocode = await _geoCodeService.GetGeocodeAsync(value);
        if (geocode?.Coordinate is null)
        {
            await messageContext.SendActivityAsync($"Sorry, {value} is an unknown location.");
            return;
        }

        var member = messageContext.FromMember;
        string? timeZoneInfo = null;
        // Slack gives us the user's timezone, so don't set it here.
        if (messageContext.Organization.PlatformType is not PlatformType.Slack)
        {
            var timezoneId = await _geoCodeService.GetTimeZoneAsync(geocode.Coordinate.Latitude, geocode.Coordinate.Longitude);
            member.TimeZoneId = timezoneId;
            timeZoneInfo = $" and your timezone to `{timezoneId}`";
        }

        member.Location = new Point(geocode.Coordinate.Latitude, geocode.Coordinate.Longitude);
        member.FormattedAddress = geocode.FormattedAddress;
        await _userRepository.UpdateUserAsync();

        await messageContext.SendActivityAsync($"I updated your location to {FormatLocation(geocode)}{timeZoneInfo}.");

        await _auditLog.LogUserPropertyChangedAsync(
            "location",
            $"{FormatLocation(geocode)}{timeZoneInfo}",
            member,
            messageContext.Organization);
    }

    async Task SaveTimezoneAndReply(MessageContext messageContext, string value)
    {
        if (messageContext.Organization.PlatformType is PlatformType.Slack)
        {
            await messageContext.SendActivityAsync("I get your timezone from Slack, so that’s the best place to change it.");
            return;
        }

        var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(value);
        if (tz is null)
        {
            await messageContext.SendActivityAsync($"Sorry, {value} is an unknown timezone. {TimezoneDatabaseResponse}");
            return;
        }

        var member = messageContext.FromMember;
        member.TimeZoneId = tz.Id;
        await _userRepository.UpdateUserAsync();
        await messageContext.SendActivityAsync("Thank you, I updated your timezone information.");
        await _auditLog.LogUserPropertyChangedAsync(
            "timezone",
            tz.Id,
            member,
            messageContext.Organization);
    }


    static readonly Regex EmailRegex = new(@"\<mailto:(?<email>.*?)\|.*?\>", RegexOptions.Compiled);

    static string GetEmail(string text)
    {
        var match = EmailRegex.Match(text);
        return match.Success
            ? match.Groups["email"].Value
            : text;
    }

    async Task SaveEmailAndReply(MessageContext messageContext, string value)
    {
        var user = messageContext.From;
        user.Email = GetEmail(value);
        await _userRepository.UpdateUserAsync();
        await messageContext.SendActivityAsync("Thank you, I updated my records of your email.");
        await _auditLog.LogUserPropertyChangedAsync(
            "email",
            string.Empty,
            messageContext.FromMember,
            messageContext.Organization);
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{property} is {value}", "Sets the {property} to {value}. Right now, only `timezone` (or `tz`), `location`, and `email` are supported.");
        usage.AddExample("timezone is America/Los_Angeles", "Sets your timezone to America/Los_Angeles.");
        usage.AddExample("location is 98008", "Sets your location to Bellevue, WA.");
        usage.AddExample("email is youremail@example.com", "Sets your email to youremail@example.com. If you use the `feedback` skill, this allows us to reply to your feedback.");
    }

    static string FormatLocation(Geocode geocode)
    {
        return geocode is { FormattedAddress: { }, Coordinate: { } }
            ? FormatLocation(geocode.FormattedAddress, geocode.Coordinate.Latitude, geocode.Coordinate.Longitude)
            : "(unknown)";
    }

    static string FormatLocation(Member member, IChannelUser bot)
    {
        return member is { FormattedAddress: { }, Location: { } }
            ? FormatLocation(member.FormattedAddress, member.Location.X, member.Location.Y)
            : $"I do not know your location. Try `{bot} my location is {{address or zip}}` to tell me your location.";
    }

    static string FormatLocation(string formattedAddress, double latitude, double longitude)
    {
        return $"`{formattedAddress}` (Lat: `{latitude}`, Lon: `{longitude}`)";
    }
}
