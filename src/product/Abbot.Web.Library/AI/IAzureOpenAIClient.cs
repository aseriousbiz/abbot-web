namespace Serious.Abbot.AI;

/// <summary>
/// Abstracts the Azure OpenAI API.
/// </summary>
public interface IAzureOpenAIClient : IOpenAIClient
{
    /// <summary>
    /// Whether or not this client is enabled.
    /// </summary>
    /// <remarks>This is determined by the AzureOpenAI::Endpoint being configured.</remarks>
    bool Enabled { get; }
}
