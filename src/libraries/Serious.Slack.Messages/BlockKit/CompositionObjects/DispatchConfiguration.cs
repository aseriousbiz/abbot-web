using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serious.Slack.Payloads;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Determines when a <see cref="PlainTextInput"/> will return a
/// <see cref="BlockActionsPayload"/>.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#dispatch_action_config"/>
/// for more information.
/// </remarks>
public class DispatchConfiguration
{
    /// <summary>
    /// Constructs a <see cref="DispatchConfiguration"/> with the specified
    /// <see cref="TriggerAction"/>s.
    /// </summary>
    /// <param name="triggerActions"></param>
    public DispatchConfiguration(params TriggerAction[] triggerActions)
    {
        TriggerActionsOn = triggerActions;
    }

    /// <summary>
    /// <para>
    /// An array of interaction types that you would like to receive a
    /// <see cref="BlockActionsPayload" /> for. Should be one or both of:
    /// </para>
    /// <para>
    ///   <c>on_enter_pressed</c> — payload is dispatched when user presses
    /// the enter key while the input is in focus. Hint text will appear underneath
    /// the input explaining to the user to press enter to submit.
    /// </para>
    /// <para>
    ///   <c>on_character_entered</c> — payload is dispatched when a character is
    /// entered (or removed) in the input.
    /// </para>
    /// </summary>
    [JsonProperty("trigger_actions_on")]
    [JsonPropertyName("trigger_actions_on")]
    public IReadOnlyList<TriggerAction> TriggerActionsOn { get; init; }
}

/// <summary>
/// The types of triggers that can be used to dispatch a <see cref="BlockActionsPayload"/>.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum TriggerAction
{
    /// <summary>
    /// <c>on_enter_pressed</c> — payload is dispatched when user presses
    /// the enter key while the input is in focus. Hint text will appear underneath
    /// the input explaining to the user to press enter to submit.
    /// </summary>
    [EnumMember(Value = "on_enter_pressed")]
    OnEnterPressed,

    /// <summary>
    /// <c>on_character_entered</c> — payload is dispatched when a character is
    /// entered (or removed) in the input.
    /// </summary>
    [EnumMember(Value = "on_character_entered")]
    OnCharacterEntered
}
