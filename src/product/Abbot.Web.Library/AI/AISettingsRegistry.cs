using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using OpenAI_API.Models;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.AI;

public record ModelSettings : JsonSettings
{
    /// <summary>
    /// The name of the model to use.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// The temperature parameter for the model.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double Temperature { get; init; } = 1.0;

    /// <summary>
    /// A <see cref="TemplatedPrompt"/> describing the AI prompt
    /// </summary>
    public required TemplatedPrompt Prompt { get; init; }

    /// <summary>
    /// Legacy prompt template. To be removed in a future version in lieu of <see cref="Prompt"/>.
    /// </summary>
    [Obsolete("Use Prompt instead")]
    public string? PromptTemplate { get; set; }
}

public class AISettingsRegistry
{
    readonly ISettingsManager _settingsManager;
#pragma warning disable CA1802
    static readonly string AIModelSettingsTypePrefix = "AI:Model:Settings";
#pragma warning restore CA1802

    public static readonly IReadOnlyList<AIFeature> AllFeatures = Enum.GetValues<AIFeature>();

    public static readonly IDictionary<AIFeature, ModelSettings> Defaults = new Dictionary<AIFeature, ModelSettings>()
    {
        [AIFeature.Summarization] = new()
        {
            Model = "gpt-4",
            Temperature = 1.0,
            Prompt = new TemplatedPrompt
            {
                Version = PromptVersion.Version2,
                Text =
                    """"
                    Multiple participants are involved in a conversation which is currently in the state: {{Conversation.State}}.
                    Each participant is identified by a token that looks like <@U123> where U123 is the user's unique id.
                    This token must be used verbatim when identifying a participant.
                    After the token is the participant's role and identifies whether the participant is a Customer or a Support Agent.

                    Summarize the following conversation.  As you summarize, I'll send additional messages in the conversation and you should refine your summary.
                    At the end of the summary, append the following (including square brackets) verbatim: [!conclusion:*]
                    Replace * with a conclusion, in present tense, directed at the Support Agent to instruct them on what to do next based on the current status of the conversation.
                    For example, if the Agent just posted a question you might respond with [!conclusion:Wait for more information from the customer]
                    If the Customer just provided some information about their account you might respond with: [!conclusion:Look up the customer's account status]
                    Do not put any text following the the conclusion and include the square brackets.
                    """"
            }
        },
        [AIFeature.Classifier] = new()
        {
            Model = Model.DavinciText,
            Temperature = 1.0,
            Prompt = new TemplatedPrompt
            {
                Version = PromptVersion.Version1,
                Text =
                    """"
                    You are an assistant to a Customer service Agent and you will help classify messages using category tokens such as [!category:value]. Do not output any text unless directed specifically to do so by the instructions.

                    1. Classify the sentiment of the message. For example, if the message is angry output the following category token: [!sentiment:negative]
                    2. Classify the topic of the message if the message is from a Customer.
                       - For example, if the Customer is searching for documentation, output the following category token: [!topic:documentation]
                       - If the Customer is reporting an outage, output the following category token: [!topic:outage]
                       - If the Customer is reporting a bug, output the following category token: [!topic:bug]
                       - If the message is purely social, output the following category token: [!topic:social]
                       - If the topic is something else, output the topic in the format [!topic:value] where "value" is the topic.
                    5. If it seems like the Customer's issue has been resolved, output the following verbatim [!state:closed]
                    6. If it looks like the Agent is looking into the issue or request, output the following verbatim [!state:snoozed]

                    For example:

                    Message: """
                    Customer: Hey, is your site down? I can't seem to access it.
                    """
                    Your output:

                    [Thought]The customer is unable to use the site, which is not a good outcome, hence the sentiment is negative.[/Thought]
                    [Action][!sentiment:negative][/Action]
                    [Thought]The site is down, hence the topic is an outage.[/Thought]
                    [Action][!topic:outage][/Action]

                    Message: """
                    Customer: Hey, Abbot is great! You had mentioned I can send an announcement to multiple rooms. Where can I read about how to send Announcements?
                    """
                    Your output:

                    [Thought]The customer wants to read up on how to send an announcement, hence the topic is documentation.[/Thought]
                    [Action][!topic:documentation][/Action]

                    Message: """
                    Agent: Oh, give me a moment to find them and I'll send them to you.
                    """
                    Your output:

                    [Thought]The agent responded to the customer and will get back to the customer, hence the state is snoozed.[/Thought]
                    [Action][!state:snoozed][/Action]

                    Message: """
                    Customer: Thanks for the help! That worked!
                    """
                    Your output:

                    [Thought]The customer's problem was resolved.[/Thought]
                    [Action][!state:closed][/Action]

                    Message: """
                    au aoehusnaot
                    """
                    Your output:

                    [Thought]The message did not make sense, hence no topic could be determined.[/Thought]
                    [Action][/Action]

                    Message: """
                    Customer: Did you see the season finale of Succession last night?
                    """
                    Your output:

                    [Thought]The customer is asking about a television show, hence the topic is social.[/Thought]
                    [Action][!topic:social][/Action]

                    Message: """
                    {Conversation}
                    """

                    Your output:
                    """"
            }
        },
        [AIFeature.ArgumentRecognizer] = new()
        {
            Model = "gpt-4",
            Temperature = 1.0,
            Prompt = new()
            {
                Version = PromptVersion.Version1,
                Text =
                    """"
                   You are Abbot, an AI designed to extract arguments to pass to a command given a message.
                   Abbot is highly accurate and produces only the exact arguments to be provided to the command.

                   You are extracting arguments for the '{SkillName}' command.

                   If you are unable to extract the arguments, please respond with the text '[!null]'.

                   Some example responses are below:
                   {Exemplars}

                   Now, given the following message, please provide the Arguments.

                   Message: """
                   {Message}
                   """

                   Arguments:
                   """"
            }
        },
        [AIFeature.ConversationMatcher] = new()
        {
            Model = "gpt-4",
            Temperature = 1.0,
            Prompt = new()
            {
                Version = PromptVersion.Version1,
                Text =
                    """"
                    When a message is presented, provide the ID of the conversation that the message belongs to, or `0` if no conversation
                    is appropriate. Also, include your reasoning in a Thought stanza. For example:

                    Message: """
                    <@U123> (Customer): Perfect, that solves my problem.
                    """

                    Output:
                    [Thought]<@U123> was involved in conversation 4 and this seems to be a response to a message from <U867> in the same conversation and relies on context from that
                    conversation. This response was only two days after the last response. I should assign the message to conversation 4[/Thought]
                    [Action]4[/Action]

                    Message: """
                    <@U567> (Customer): No, I haven't tried restarting it.
                    """

                    Output:
                    [Thought]<@U567> is involved in conversations 2 and 3, but conversation 3 is describing a billing issue while conversation 2 is describing errors with the customers app. I should assign the message to conversation 2[/Thought]
                    [Action]2[/Action]

                    Message: """
                    <@U890> (Customer): Hey, I'm getting an error when I log in: "Invalid operation"
                    """

                    Output:
                    [Thought]<@U890> is involved in conversation 5, but this message appears to describe a new error. I should assign the message
                    to a new conversation.[/Thought]
                    [Action]0[/Action]

                    Message: """
                    <@U912> (Agent): Try restarting your computer.
                    """

                    Output:
                    [Thought]<@U912>is not a participant in any of the existing conversations, but the message seems to be related to conversation matching. Conversation 42 is related to conversation matching. This message appears to be a response to conversation 42. I should assign the message to conversation 42.[/Thought]
                    [Action]42[/Action]

                    Take a look at the following set of conversation summaries in a chat room. Each conversation is identified by a number.
                    Each participant in a conversation is identified by a token that looks like <@U123> where U123 is the user's unique id.

                    {Conversation}

                    For each following message, identify the first conversation number the message belongs to or 0 if no conversation is a match.
                    """"
            }
        },
        [AIFeature.DefaultResponder] = new()
        {
            Model = Model.DavinciText,
            Temperature = 1.0,
            Prompt = new TemplatedPrompt()
            {
                Version = PromptVersion.Version1,
                Text = "",
            }
        },
        [AIFeature.MagicResponder] = new()
        {
            Model = "gpt-3.5-turbo",
            Temperature = 1.0,
            Prompt = new TemplatedPrompt()
            {
                Version = PromptVersion.Version2,
                Text =
                    """"
                    You are the Language Model that powers a chatbot named {{Organization.BotName}}.
                    {{Organization.BotName}} is installed into the Slack workspace used by "{{Organization.Name}}",

                    You will respond in the AbbotLang language:
                    {{PromptConstants.AbbotLangDescription}}

                    Context:

                    * Slack users are identified by a User ID like `U123`.
                    * Slack channels are identified by a Channel ID like `C123`.
                    * You can mention a user in a Slack message with the syntax `<@U123>`.
                    * You can mention a channel in a Slack message with the syntax `<#C123>`.
                    * {{Organization.BotName}} has the Slack user ID `<@{{Organization.PlatformBotUserId}}>`.
                    * This conversation started at {{CurrentTime}}.

                    {{Organization.BotName}} has a number of features you can use to help perform tasks:

                    {{#each Features}}
                    * {{this}}
                    {{/each}}

                    USE THESE FEATURES to answer user requests. IF there are no features that can help, you MAY "synthesize" a response,
                    and generate your best answer from your own model, but you MUST indicate if a response is synthesized.

                    You will use ONLY the following commands:

                    {{#each AllowedCommands}}
                    * `{{Exemplar}}` - {{Description}}
                    {{/each}}

                    Examples:
                    {{#each ExampleResponses}}

                    ```
                    {{this}}
                    ```
                    {{/each}}
                    """"
            }
        }
    };


    public AISettingsRegistry(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    /// <summary>
    /// Retrieves the model settings for the <see cref="AIFeature"/>.
    /// </summary>
    /// <remarks>
    /// We used to save these settings as individual settings, but now we save them as a single JSON object. This
    /// is why there's a bit of migration code here so we don't lose what we already have.
    /// </remarks>
    /// <param name="feature">The AI Feature to get the model for.</param>
    /// <returns>A <see cref="ModelSettings"/> with information for the model to use for the feature.</returns>
    public async Task<ModelSettings> GetModelSettingsAsync(AIFeature feature)
    {
        ModelSettings modelSettings;
        var setting = await _settingsManager.GetAsync(SettingsScope.Global, $"{AIModelSettingsTypePrefix}:{feature}");
        if (setting is not { Value.Length: > 0 })
        {
            modelSettings = Defaults.TryGetValue(feature, out var defaultSettings)
                ? defaultSettings
                : throw new UnreachableException($"Settings for {feature} not found and no default settings were provided.");
        }
        else
        {
            modelSettings = JsonSettings.FromJson<ModelSettings>(setting.Value).Require();
        }


        // WE LIE!! Prompt actually _could_ be null in one case, and it's right here.
        // Legacy values in the DB will have Prompt be null, so we need to migrate them.
        // But we make it non-nullable in the type because we'll always fix it here and we want dependent code to
        // be able to assume it's not null.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (modelSettings.Prompt is null)
        {
            // Fill in newer 'Prompt' property if it's not already filled in
            modelSettings = modelSettings with
            {
                Prompt = new()
                {
                    Version = PromptVersion.Version1,
#pragma warning disable CS0618
                    Text = modelSettings.PromptTemplate ?? "",
#pragma warning restore CS0618
                }
            };
        }

        return modelSettings;
    }

    /// <summary>
    /// Sets the model settings for the <see cref="AIFeature"/>.
    /// </summary>
    /// <param name="feature">The AI Feature to get the model for.</param>
    /// <param name="modelSettings">The model settings to set.</param>
    /// <param name="actor">The <see cref="Member" /> setting the model settings"/>.</param>
    /// <returns>A <see cref="ModelSettings"/> with information for the model to use for the feature.</returns>
    public async Task SetModelSettingsAsync(
        AIFeature feature,
        ModelSettings modelSettings,
        Member actor)
    {
        await _settingsManager.SetWithAuditingAsync(
            SettingsScope.Global,
            $"{AIModelSettingsTypePrefix}:{feature}",
            modelSettings.ToJson(),
            actor.User,
            actor.Organization);
    }

    /// <summary>
    /// Resets the globally configured prompt for summarizing a conversation back to the default.
    /// </summary>
    /// <param name="feature">The AI Feature to reset the model for.</param>
    /// <param name="actor">The person doing the setting.</param>
    public async Task ResetModelSettingsAsync(AIFeature feature, Member actor)
    {
        await _settingsManager.RemoveWithAuditingAsync(
            SettingsScope.Global,
            $"{AIModelSettingsTypePrefix}:{feature}",
            actor.User,
            actor.Organization);
    }
}

public enum AIFeature
{
    DefaultResponder,
    Summarization,
    Classifier,
    ArgumentRecognizer,
    ConversationMatcher,
    MagicResponder,
}

public static class AIFeatureExtensions
{
    public static bool UsesChatModel(this AIFeature? feature) =>
        feature.HasValue && feature.Value.UsesChatModel();

    public static bool UsesChatModel(this AIFeature feature)
    {
        // Assume new features are chat models.
        return feature switch
        {
            AIFeature.DefaultResponder => false,
            AIFeature.Classifier => false,
            _ => true,
        };
    }
}
