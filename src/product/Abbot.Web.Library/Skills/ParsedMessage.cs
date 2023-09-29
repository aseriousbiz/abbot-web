using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Skills;

/// <summary>
/// Represents an incoming chat message in a parsed format that contains important information about the
/// incoming message.
/// </summary>
public class ParsedMessage
{
    static readonly string HeyRobotPattern = $@"{SkillPatterns.HeyPattern}{SkillPatterns.GetMentionPattern("robot")}";
    const string SkillAndArgumentsPattern = @$"^(?<skill>{SkillPatterns.NamePattern})(?<sigil>!?)(\s+(?<args>.+?))?\s*$";
    static readonly string MessageToRobotPattern = @$"^{HeyRobotPattern}(?:\s+(?<rest>.+?)\s*)?$";
    static readonly string MentionFirstPattern = $@"^{GetMentionPattern("user")}\s+(?<verb>(?:is|can)(?: not)?)\s+(?<value>.*)$";

    static string GetMentionPattern(string groupName)
    {
        return $@"<@(?<{groupName}>.+?)>"; // Abbot Normal Form for mentions (aka Slack's mention format).
    }

    static readonly Regex MessageToRobotRegex = new(MessageToRobotPattern,
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
    static readonly Regex SkillAndArgumentsRegex = new(SkillAndArgumentsPattern,
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
    static readonly Regex MentionFirstPatternRegex = new(MentionFirstPattern,
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses an incoming message and returns a <see cref="ParsedMessage" /> representation of that message.
    /// </summary>
    /// <param name="message">The text of the incoming message</param>
    /// <param name="botUserId">The user id of the bot</param>
    /// <param name="shortcutCharacter">The shortcut character used to invoke Abbot skills as an alternative to mentioning Abbot.</param>
    public static ParsedMessage Parse(string message, string botUserId, char shortcutCharacter)
    {
        if (message is { Length: 0 })
        {
            return new ParsedMessage(message, message);
        }

        string textAfterBotMention = string.Empty;

        if (shortcutCharacter != ' ' && message[0] == shortcutCharacter)
        {
            textAfterBotMention = message[1..];

            if (textAfterBotMention is { Length: 0 } || !char.IsLetter(textAfterBotMention[0]))
            {
                // It's looking like this is not a skill call, but maybe it's a mention first message.
                // 9 is a conservative minimum length of a mention first message: "<@X> is y"
                if (textAfterBotMention.Length >= 9 && textAfterBotMention[..2] == " <")
                {
                    textAfterBotMention = textAfterBotMention[1..];
                }
                else
                {
                    return new ParsedMessage(textAfterBotMention.Trim(), original: message);
                }
            }
        }
        else
        {
            var match = MessageToRobotRegex.Match(message);

            bool isBotCommand = match.Success
                && string.Equals(botUserId, match.Groups["robot"].Value,
                    StringComparison.OrdinalIgnoreCase);

            if (!isBotCommand)
            {
                return new ParsedMessage(textAfterBotMention, message);
            }

            textAfterBotMention = match.Groups["rest"].Value;
        }

        // Attempt to parse skill and arguments
        return ParseCommand(textAfterBotMention, message);
    }

    /// <summary>
    /// Parses a command. This is the part after the Bot mention or the shortcut character. This is used by
    /// things such as the <see cref="AliasSkill"/> where control the formatting of the message.
    /// </summary>
    /// <remarks>
    /// For example, with the command "@abbot who are you" or ".who are you" this would be "who are you".
    /// If the message is not directed at Abbot but is a command because of pattern matching or raising an AI signal,
    /// this is the full message text.
    /// </remarks>
    /// <param name="commandText">The skill name and arguments</param>
    public static ParsedMessage ParseCommand(string commandText)
    {
        return ParseCommand(commandText, commandText);
    }

    /// <summary>
    /// Parses a command. This is the part after the Bot mention or the shortcut character. This is used by
    /// things such as the <see cref="AliasSkill"/> where control the formatting of the message.
    /// </summary>
    /// <param name="commandText">The skill name and arguments</param>
    /// <param name="original">The original full message</param>
    static ParsedMessage ParseCommand(string commandText, string original)
    {
        var (skill, sigil, arguments) = ParseSkillSigilAndArguments(commandText);
        return new ParsedMessage(isBotCommand: true, skill, arguments, commandText, original, sigil);
    }

    /// <summary>
    /// Use this when a message is unequivocally not intended for Abbot.
    /// </summary>
    /// <param name="text">The text of the message</param>
    /// <returns>A <see cref="ParsedMessage"/>.</returns>
    public static ParsedMessage CreateMessageNotForBot(string text)
    {
        return new ParsedMessage(text, text);
    }

    /// <summary>
    /// Creates a <see cref="ParsedMessage" /> from the known <see cref="Skill"/> and arguments.
    /// This is used when parsing interactive events with a user skill.
    /// </summary>
    /// <param name="skill">The skill to call</param>
    /// <param name="arguments">The arguments to pass to the skill.</param>
    /// <param name="contextId">Id that tracks the state and context of chat messages. See <see cref="ContextId"/> for more info.</param>
    public static ParsedMessage Create(Skill skill, string arguments, string? contextId)
    {
        string command = $"{skill} {arguments}";

        return new ParsedMessage(
            true,
            skill.Name,
            arguments,
            command,
            command,
            string.Empty,
            skill: skill,
            contextId: contextId);
    }

    /// <summary>
    /// Creates a <see cref="ParsedMessage" /> from the known skill name and arguments.
    /// This is used when parsing interactive events with a built-in skill.
    /// </summary>
    /// <param name="skillName">The name of the skill to call</param>
    /// <param name="arguments">The arguments to pass to the skill.</param>
    /// <param name="contextId">Id that tracks the state and context of chat messages. See <see cref="ContextId"/> for more info.</param>
    public static ParsedMessage Create(string skillName, string arguments, string? contextId)
    {
        string command = $"{skillName} {arguments}";

        return new ParsedMessage(
            true,
            skillName,
            arguments,
            command,
            command,
            string.Empty,
            contextId: contextId);
    }

    /// <summary>
    /// Creates a parsed message using the set of <see cref="SkillPattern"/> instances and the original message.
    /// </summary>
    /// <param name="patterns">The set of <see cref="SkillPattern"/> instances that matched the incoming message.</param>
    /// <param name="original">The incoming message.</param>
    public static ParsedMessage Create(IReadOnlyList<SkillPattern> patterns, string original)
    {
        Debug.Assert(patterns.Count > 0);

        // RemoteSkillCallSkill has special handling for patterns so we short-circuit
        // skill resolution by specifying this built-in.
        const string skillName = RemoteSkillCallSkill.SkillName;

        return new ParsedMessage(
            isBotCommand: false,
            skillName,
            arguments: original,
            commandText: original,
            original,
            string.Empty,
            patterns);
    }

    public void Deconstruct(out string skill, out string args)
    {
        skill = PotentialSkillName;
        args = PotentialArguments;
    }

    static (string Skill, string Sigil, string Arguments) ParseSkillSigilAndArguments(string message)
    {
        var skillMatch = SkillAndArgumentsRegex.Match(message);

        if (skillMatch.Success)
        {
            return (skillMatch.Groups["skill"].Value, skillMatch.Groups["sigil"].Value, skillMatch.Groups["args"].Value);
        }

        // Try the mention first pattern.
        var mentionFirstMatch = MentionFirstPatternRegex.Match(message);
        if (mentionFirstMatch.Success)
        {
            var user = mentionFirstMatch.Groups["user"].Value;
            string mention = $"<@{user}>";
            var value = mentionFirstMatch.Groups["value"].Value;
            var verb = mentionFirstMatch.Groups["verb"].Value.ToLowerInvariant();
            return verb switch
            {
                "is" or "is not" => ("who", string.Empty, $"{verb} {mention} {value}"),
                "can" => ("can", string.Empty, $"{mention} {value}"),
                "can not" => ("can", string.Empty, $"not {mention} {value}"),
                _ => (string.Empty, string.Empty, string.Empty)
            };
        }

        return (string.Empty, string.Empty, string.Empty);
    }

    ParsedMessage(string textAfterBotMention, string original)
        : this(false, string.Empty, string.Empty, textAfterBotMention, original, string.Empty)
    {
    }

    ParsedMessage(
        bool isBotCommand,
        string skillName,
        string arguments,
        string commandText,
        string original,
        string sigil,
        IEnumerable<SkillPattern>? patterns = null,
        Skill? skill = null,
        string? contextId = null)
    {
        IsBotCommand = isBotCommand;
#pragma warning disable CA1308
        PotentialSkillName = skillName.ToLowerInvariant();
#pragma warning disable
        PotentialArguments = arguments;
        CommandText = commandText;
        OriginalMessage = original;
        Sigil = sigil;
        Patterns = patterns?.ToReadOnlyList() ?? Array.Empty<SkillPattern>();
        Skill = skill;
        ContextId = contextId;
    }

    /// <summary>
    /// <c>true</c> if this message is a command to Abbot. For example, it must start with `@abbot` or `hey @abbot`
    /// or start with the shortcut character such as ".skill-name" ...
    /// </summary>
    public bool IsBotCommand { get; }

    /// <summary>
    /// <c>true</c> if this message is in response to an interactive element in a skill message. For example,
    /// when clicking on a button.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Skill))]
    public bool IsInteraction => Skill != null;

    /// <summary>
    /// The potential skill name. Empty string if the message doesn't match a command.
    /// </summary>
    public string PotentialSkillName { get; }

    /// <summary>
    /// The potential arguments to the skill. Empty string if no arguments are supplied.
    /// </summary>
    public string PotentialArguments { get; }

    /// <summary>
    /// If this is a command to Abbot, this is the text after the mention of abbot. Aka, the part after `@abbot`
    /// or `hey @abbot`. This is what we will pass to the fallthrough.
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// The raw original message
    /// </summary>
    public string OriginalMessage { get; }

    /// <summary>
    /// The "sigil" applied when invoking this skill, such as the '!' character to force exact matching of arguments.
    /// </summary>
    public string Sigil { get; }

    /// <summary>
    /// The set of <see cref="SkillPattern"/> instances that were matched (if any) for this message.
    /// </summary>
    public IReadOnlyList<SkillPattern> Patterns { get; }

    /// <summary>
    /// The skill - when parsing interactive events, we get the skill id in the payload, so we can
    /// resolve it immediately and pass it along.
    /// </summary>
    public Skill? Skill { get; }

    /// <summary>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// We get this when parsing interactive events,
    /// so we know which skill execution the user is interacting with.
    /// </summary>
    public string? ContextId { get; }
}
