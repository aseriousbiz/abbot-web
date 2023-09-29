namespace Serious.Abbot.Models;

public static class ProblemTypes
{
    const string BaseUrl = "https://schema.ab.bot/problems/";

    public const string NotFound = BaseUrl + "not-found";

    public static string? FromSlack(string errorCode) => BaseUrl + "slack/" + errorCode;
}
