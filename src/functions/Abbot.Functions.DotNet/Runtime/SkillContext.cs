using System;
using Microsoft.Bot.Schema;
using Serious.Abbot.Messages;
using Serious.Abbot.Storage;

namespace Serious.Abbot.Functions.Runtime;

/// <summary>
/// Information needed in order to call the skill and for the skill to call the skill APIs.
/// </summary>
public class SkillContext
{
    string? _assemblyName;

    /// <summary>
    /// Constructs a <see cref="SkillContext" />.
    /// </summary>
    /// <param name="message">The incoming request to run a skill.</param>
    /// <param name="apiKey">The API Key needed to call the skill APIs.</param>
    public SkillContext(SkillMessage message, string apiKey)
    {
        SkillInfo = message.SkillInfo;
        SkillRunnerInfo = message.RunnerInfo;
        ApiKey = apiKey;
        ConversationInfo = message.ConversationInfo;
        SignalInfo = message.SignalInfo;
        PassiveReplies = message.PassiveReplies;
    }

    /// <summary>
    /// Creates a legacy conversation reference for the current skill context.
    /// </summary>
    public ConversationReference CreateConversationReference()
    {
        var room = SkillInfo.Room.Id;
        var thread = SkillInfo.Message?.ThreadId;

        // This duplicates logic in SkillConversationId, but it's a temporary measure.
        var id = $"::{room}";
        if (thread is not null)
        {
            id += $":{thread}";
        }

        return new ConversationReference
        {
            Conversation = new ConversationAccount
            {
                Id = id
            }
        };
    }

    /// <summary>
    /// The API key needed to call the skill APIs.
    /// </summary>
    public string ApiKey { get; }

    /// <summary>
    /// Information about the skill for the skill runner. This is info that user skills don't have access to.
    /// </summary>
    public SkillRunnerInfo SkillRunnerInfo { get; }

    /// <summary>
    /// Information about the skill being called and the arguments to the skill.
    /// This information is passed to the skill.
    /// </summary>
    public SkillInfo SkillInfo { get; }

    /// <summary>
    /// Information about the incoming signal, if a signal caused the skill to run.
    /// </summary>
    public SignalMessage? SignalInfo { get; }

    /// <summary>
    /// The conversation in which the skill was invoked, if any.
    /// </summary>
    public ChatConversation? ConversationInfo { get; }

    public bool PassiveReplies { get; }

    /// <summary>
    /// The current assembly name.
    /// </summary>
    public string AssemblyName => _assemblyName
                                  ?? throw new InvalidOperationException($"Attempted to retrieve {nameof(AssemblyName)} before it's been set.");

    /// <summary>
    /// Sets the <see cref="AssemblyName"/> property so that the <see cref="BrainSerializer"/> can work
    /// effectively.
    /// </summary>
    /// <param name="assemblyName">The name of the compiled skill assembly.</param>
    /// <exception cref="InvalidOperationException">Thrown if it's already set.</exception>
    public void SetAssemblyName(string assemblyName)
    {
        if (_assemblyName is not null)
        {
            throw new InvalidOperationException("The assembly name has already been set");
        }

        _assemblyName = assemblyName;
    }
}
