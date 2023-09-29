using System;
using System.Globalization;

namespace Serious.Abbot.Functions.Services;

/// <summary>
/// Useful extension methods of <see cref="IEnvironment"/> used to safely retrieve
/// configuration values needed by the function runner.
/// </summary>
public static class EnvironmentExtensions
{
    const string AbbotApiBaseUrl = nameof(AbbotApiBaseUrl);
    const string SkillApiBaseUriFormatString = nameof(SkillApiBaseUriFormatString);

    /// <summary>
    /// Retrieves the base <see cref="Uri"/> for the Abbot Skill Api used by
    /// the function runner to reply and call other APIs such as the brain etc.
    /// </summary>
    /// <param name="environment">The <see cref="IEnvironment"/>.</param>
    public static Uri? GetAbbotApiBaseUrl(this IEnvironment environment)
    {
        var url = environment.GetEnvironmentVariable(AbbotApiBaseUrl);
        return url is null
            ? null
            : new Uri(url);
    }

    /// <summary>
    /// Retrieve the URL used to reply back to Abbot.
    /// </summary>
    /// <param name="environment">The <see cref="IEnvironment"/>.</param>
    /// <param name="skillId">The skill that is replying to Abbot.</param>
    public static Uri GetAbbotReplyUrl(this IEnvironment environment, int skillId)
    {
        return environment.GetSkillApiUrl(skillId).Append("/reply");
    }

    public static Uri GetSkillApiUrl(this IEnvironment environment, int skillId)
    {
        var urlFormat = environment.GetEnvironmentVariable(SkillApiBaseUriFormatString);
        if (urlFormat is not null)
        {
            return new(string.Format(CultureInfo.InvariantCulture, urlFormat, skillId));
        }

        var baseUrl = environment.GetAbbotApiBaseUrl()
                      ?? throw new InvalidOperationException(
                          $"Environment variable `{SkillApiBaseUriFormatString}` is not set, " +
                          $"and neither is {AbbotApiBaseUrl}.");

        return baseUrl.Append($"/skills/{skillId}");
    }
}
