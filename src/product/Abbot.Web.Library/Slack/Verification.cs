using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Serious.Slack;

public enum VerificationResult
{
    TimestampExpired,
    MissingTimestamp,
    SignatureMismatch,
    Ok,
    Ignored,
}

public static class VerificationResultExtensions
{
    public static bool IsSuccessful(this VerificationResult result) =>
        result is VerificationResult.Ignored or VerificationResult.Ok;
}

/// <summary>
/// Provides a method to verify the content of a request coming from Slack.
/// </summary>
public static class Verification
{
    /// <summary>
    /// Verifies the content of a request coming from slack.
    /// </summary>
    /// <param name="body">The body of the request as a string.</param>
    /// <param name="timeStampHeader">The X-Slack-Request-Timestamp request header.</param>
    /// <param name="slackSignature">The X-Slack-Signature request header.</param>
    /// <param name="slackSigningSecret">Your Slack app's signing secret.</param>
    /// <param name="now">The current date time</param>
    /// <returns>A <see cref="VerificationResult"/> based on the parameters.</returns>
    public static VerificationResult VerifyRequest(
        string body,
        string timeStampHeader,
        string slackSignature,
        string slackSigningSecret,
        DateTimeOffset now,
        ILogger? logger)
    {
        logger ??= NullLogger.Instance;

        // Verify that the headers contain the right auth
        if (!int.TryParse(timeStampHeader, out var epochSeconds))
        {
            throw new InvalidOperationException($"Timestamp header not an int {timeStampHeader}");
        }
        DateTimeOffset messageTime = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);

        if (messageTime < now.ToUniversalTime().AddMinutes(-5)) // Message time is more than 5 minutes in the past
        {
            return VerificationResult.TimestampExpired;
        }

        var sigBase = $"v0:{timeStampHeader}:{body}";

        using var hash = new HMACSHA256(Encoding.UTF8.GetBytes(slackSigningSecret));
        var computedHash = hash.ComputeHash(Encoding.UTF8.GetBytes(sigBase));

        var computedSignature = "v0=" + BitConverter
            .ToString(computedHash)
            .Replace("-", "", StringComparison.Ordinal);

        if (!slackSignature.Equals(computedSignature, StringComparison.OrdinalIgnoreCase))
        {
            logger.SignatureMismatch(
                sigBase,
                computedSignature,
                slackSignature);
            return VerificationResult.SignatureMismatch;
        }
        return VerificationResult.Ok;
    }
}

public static partial class SlackRequestVerificationFilterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Signature mismatch, payload '{SignaturePayload}' should have signature '{ComputedSignature}', but Slack provided signature '{ProvidedSignature}'.")]
    public static partial void SignatureMismatch(this ILogger logger,
        string SignaturePayload,
        string ComputedSignature,
        string ProvidedSignature);
}
