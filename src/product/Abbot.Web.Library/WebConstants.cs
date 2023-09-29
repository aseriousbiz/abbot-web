using System.Collections.Generic;
using System.Diagnostics;

namespace Serious.Abbot;

public static class WebConstants
{
    public static readonly bool IsBeta = true;

    public const string ASeriousBizSlackId = "T013108BYLS";
    public const string FunnyBusinessSlackId = "T01CT0CT415";
    public const string PulumiSlackId = "T4PBPMA8J";
    public const string SupportEmail = "support@ab.bot";
    public const string SlackConnectInvitee = "paul@aseriousbusiness.com";
    public const string DefaultTimezoneId = "America/Los_Angeles";

    public static readonly IEnumerable<string> OurSlackTeamIds = new[] { ASeriousBizSlackId, FunnyBusinessSlackId };

    public const string MigrationsAssembly = "Abbot.Web";

    /// <summary>
    /// The default page size for "long" views, where we're fine with having to scroll the list to see more.
    /// Think: List of available skills.
    /// </summary>
    public const int LongPageSize = 20;

    /// <summary>
    /// The default page size for "short" views, where we'd like all the rows to be visible without scrolling.
    /// Think: Room lists in settings pages that co-exist with other settings controls.
    /// </summary>
    public const int ShortPageSize = 10;

    public const int SlackApiLimit = 200;

    public const string SkillApiBaseRouteTemplate = "api/skills";

    /// <summary>
    /// The prefix for Status messages that should be rendered as an error rather than a success message.
    /// </summary>
    public const string ErrorStatusPrefix = "Error: ";

    /// <summary>
    /// Gets the default base domain to use for email addresses.
    /// </summary>
    public static string EmailDomain => DefaultHost.StartsWith("localhost", StringComparison.Ordinal)
        ? "ab.bot.localhost"
        : DefaultHost;

    static string? _defaultHost;

    public static string DefaultHost
    {
        get => _defaultHost ??= AllowedHosts.Web is [var host, ..] ? host : "localhost:4979";
    }

    static string? _defaultIngestionHost;

    public static string DefaultIngestionHost
    {
        get => _defaultIngestionHost ??= AllowedHosts.Ingestion is [var host, ..] ? host : DefaultHost;
    }

    public static string DefaultScheme => "https";

    /// <summary>
    /// When we make changes to Abbot.Scripting.Interfaces that are not binary compatible
    /// (but still backwards compatible at compile time), update this cache key to trigger
    /// Abbot to recompile all C# skills.
    /// </summary>
    // NOTE: Two relatively quick ways to regenerate this:
    // * Say `random` in a DM with Abbot
    // * Run `openssl rand -base64 32` on the Terminal
    public const string CodeCacheKeyHashSeed = "x5SQx4P0qFqMPL6X4ahdGIV9xHxuktYepidjInEGZHc=";

    /// <summary>
    /// When caching calls to retrieve custom emojis, this is the hash seed we use.
    /// </summary>
    public const string EmojiCacheKeyHashSeed = "OuFrJ3aK3itbpduX1Es5Ki/jzdkKPGW2WhyoTOMzU7Y=";

    public static readonly TimeSpan StaleAssemblyTimeSpan = new(2, 0, 0);

    public const string UnexpectedBotErrorMessage =
        "I have encountered an unexpected error and notified my creators. They are very sorry for the inconvenience.";

    /// <summary>
    /// Gets a "Contact support for help" sentence that can be used in error messages.
    /// If a current <see cref="System.Diagnostics.Activity"/> can be identified, the message includes the activity ID.
    /// </summary>
    /// <returns></returns>
    public static string GetContactSupportSentence()
    {
        return Activity.Current?.Id is { Length: > 0 } activityId
            ? $"Contact '{SupportEmail}' and give them this identifier `{activityId}` if you're stuck."
            : $"Contact '{SupportEmail}' if you're stuck.";
    }

    /// <summary>
    /// A regular expression that represents characters allowed in various symbolic names in our system (Skills, Tags, Playbook Slugs, etc.)
    /// </summary>
    public const string NameCharactersPattern = "[a-zA-Z0-9](?:[a-zA-Z0-9]|-(?=[a-zA-Z0-9]))";
}
