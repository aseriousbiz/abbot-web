namespace Serious.Abbot.Configuration;

public class Auth0Options
{
    public const string Auth0 = nameof(Auth0);

    public string? Domain { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}
