using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Skills;

/// <summary>
/// Helper class for building usage examples used by the built-in skills.
/// </summary>
public class UsageBuilder
{
    readonly List<Usage> _usages = new();

    public static UsageBuilder Create(string skillName, BotChannelUser bot)
    {
        return new(skillName, bot);
    }

    UsageBuilder(string skillName, BotChannelUser bot)
    {
        SkillName = skillName;
        Bot = bot;
    }

    public string SkillName { get; }

    public BotChannelUser Bot { get; }

    public void AddEmptyArgsUsage(string description)
    {
        Add(string.Empty, description);
    }

    public void Add(string args, string description)
    {
        AddAlternativeUsage(Bot.DisplayName, SkillName, args, description);
    }

    public void AddExample(string args, string description)
    {
        AddUsage("Ex. ", args, description);
    }

    public void AddAlternativeUsage(string exampleUsage, string description)
    {
        AddUsage(string.Empty, Bot.DisplayName, exampleUsage, "", description);
    }

    public void AddAlternativeUsage(string alternativeSkillName, string exampleArgs, string description)
    {
        AddUsage(
            string.Empty,
            Bot.DisplayName,
            alternativeSkillName,
            exampleArgs,
            description);
    }

    public void AddVerbatim(string text)
    {
        _usages.Add(new Usage { Description = text });
    }

    public override string ToString()
    {
        return string.Join('\n', _usages.Select(FormatUsage));
    }

    void AddAlternativeUsage(
        string userId,
        string skillNameOverride,
        string exampleArgs,
        string description)
    {
        AddUsage(string.Empty, userId, skillNameOverride, exampleArgs, description);
    }

    void AddUsage(string prefix, string exampleArgs, string description)
    {
        AddUsage(prefix, Bot.DisplayName, SkillName, exampleArgs, description);
    }

    void AddUsage(string prefix, string botId, string skillNameOverride, string exampleArgs, string description)
    {
        _usages.Add(new Usage
        {
            Prefix = prefix,
            BotId = botId,
            SkillName = skillNameOverride,
            ExampleArgs = exampleArgs,
            Description = description
        });
    }

    class Usage
    {
        public string Prefix { get; set; } = string.Empty;
        public string BotId { get; set; } = string.Empty;
        public string SkillName { get; set; } = string.Empty;
        public string ExampleArgs { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    string FormatUsage(Usage usage)
    {
        var skillInvocation = string.IsNullOrEmpty(usage.BotId)
            ? $"{usage.SkillName}"
            : $"{Bot} {usage.SkillName}";

        var example = skillInvocation
            .AppendIfNotEmpty(usage.ExampleArgs);
        if (example.Length > 0)
        {
            example = $"`{example}` ";
        }
        return $"{usage.Prefix}{example}"
            .AppendIfNotEmpty(usage.Description, "_", "_")
            .TrimEnd()
            .EnsureEndsWithPunctuation();
    }
}
