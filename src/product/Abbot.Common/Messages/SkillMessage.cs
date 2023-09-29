namespace Serious.Abbot.Messages;

/// <summary>
/// The message sent from Abbot Web to a skill runner to invoke a skill.
/// </summary>
public class SkillMessage
{
    /// <summary>
    /// Increment this value whenever the current version of the skill message format changes.
    /// </summary>
    /// <remarks>
    /// Only increment this value when there are changes that would _break_ current skill runners.
    /// For example, you do not need to increment this number when adding an optional property that current skill runners will ignore.
    /// </remarks>
    public static readonly int CurrentVersion = 1;

    /// <summary>
    /// A simple versioning indicator that allows Skill Runners to adapt their parsing logic when significant changes are made to the format.
    /// </summary>
    /// <remarks>
    /// This value is constant on the server and will increase whenever the format of this message changes significantly.
    /// Any changes to the format _must_ retain the following properties:
    /// * The message must be a single JSON object
    /// * The 'Version' property must be present and contain an integer
    /// The absence of the 'Version' property implies version "0" (the pre-versioning version of the message)
    /// </remarks>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// If <c>true</c>, then passive replies are expected. The runner shouldn't try
    /// to post replies, but cache them and return them as part of the response.
    /// </summary>
    public bool PassiveReplies { get; init; }

    /// <summary>
    /// Provides info about the skill that skill authors should have access to.
    /// </summary>
    public SkillInfo SkillInfo { get; init; } = new();

    /// <summary>
    /// Information about the signal that this skill is responding to.
    /// </summary>
    public SignalMessage? SignalInfo { get; init; }

    /// <summary>
    /// The conversation in which the skill was invoked, if any.
    /// </summary>
    public ChatConversation? ConversationInfo { get; init; }

    /// <summary>
    /// Information about the skill for the skill runner. This is info that skill authors should not have access to.
    /// </summary>
    public SkillRunnerInfo RunnerInfo { get; init; } = new();

    /// <summary>
    /// Allows deconstructing this object into a tuple.
    /// </summary>
    /// <param name="skillInfo">Information about the skill being called and the arguments to the skill. This information is passed to the skill.</param>
    /// <param name="runnerInfo">The runner info</param>
    public void Deconstruct(out SkillInfo skillInfo, out SkillRunnerInfo runnerInfo)
    {
        skillInfo = SkillInfo;
        runnerInfo = RunnerInfo;
    }
}
