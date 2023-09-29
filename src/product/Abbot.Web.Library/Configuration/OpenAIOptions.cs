namespace Serious.Abbot.Configuration;

/// <summary>
/// The configuration options for Open AI.
/// </summary>
public class OpenAIOptions
{
    /// <summary>
    /// The name of the configuration section.
    /// </summary>
    public const string OpenAI = "OpenAI";

    /// <summary>
    /// The Open AI api key.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// The Open AI Organization Id.
    /// </summary>
    public string? OrganizationId { get; init; }

    /// <summary>
    /// The Id of the custom model to use, if any.
    /// </summary>
    public string? CustomModelId { get; init; }

    /// <summary>
    /// The maximum number of tokens to generate in the completion. The token count of your prompt plus
    /// <see cref="MaxTokens"/> cannot exceed the model's context length. Most models have a context length
    /// of 2048 tokens (except for the newest models, which support 4096).
    /// </summary>
    public int? MaxTokens { get; init; }
}
