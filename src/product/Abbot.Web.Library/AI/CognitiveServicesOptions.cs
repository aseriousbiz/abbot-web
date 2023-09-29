namespace Serious.Abbot.AI;

/// <summary>
/// Configuration information for the Azure Cognitive Services.
/// </summary>
public record CognitiveServicesOptions
{
    /// <summary>
    /// The API endpoint for the Azure Cognitive Services.
    /// </summary>
    public string? Endpoint { get; init; }
}

/// <summary>
/// Configuration information for the Azure OpenAI Service.
/// </summary>
public record AzureOpenAIOptions
{
    /// <summary>
    /// The API endpoint for the Azure OpenAI Service.
    /// </summary>
    public required string? Endpoint { get; init; }
}
