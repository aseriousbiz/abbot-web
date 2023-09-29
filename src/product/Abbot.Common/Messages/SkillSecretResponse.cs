namespace Serious.Abbot.Messages;

/// <summary>
/// A response from the Abbot Secrets API with a secret.
/// </summary>
public class SkillSecretResponse
{
    /// <summary>
    /// The secret.
    /// </summary>
    public string Secret { get; init; } = null!;
}
