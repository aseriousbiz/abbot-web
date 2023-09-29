namespace Serious.Abbot.Services;

public class StripeOptions
{
    public string PublishableKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public string WebhookSecret { get; set; } = null!;
    public string BusinessPlanPriceId { get; set; } = null!;
    public bool TestMode { get; set; }

    public string StripeDashboardBaseUrl => TestMode ? "https://dashboard.stripe.com/test" : "https://dashboard.stripe.com";
}
