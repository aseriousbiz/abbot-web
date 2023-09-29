using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Azure.Cosmos.Linq;
using Serious.Abbot.Services;

namespace Serious.Abbot.AI;

/// <summary>
/// Wrapper interface for <see cref="TextAnalyticsClient"/>.
/// </summary>
public interface ITextAnalyticsClient
{
    /// <summary>
    /// Runs a predictive model to identify a collection of entities containing
    /// Personally Identifiable Information found in the passed-in document,
    /// and categorize those entities into types such as US social security
    /// number, drivers license number, or credit card number.
    /// <para>For more information on available categories, see
    /// <see href="https://aka.ms/tanerpii"/>.</para>
    /// <para>For a list of languages supported by this operation, see
    /// <see href="https://aka.ms/talangs"/>.</para>
    /// <para>For document length limits, maximum batch size, and supported text encoding, see
    /// <see href="https://aka.ms/azsdk/textanalytics/data-limits"/>.</para>
    /// </summary>
    /// <remarks>
    /// This method is only available for <see cref="TextAnalyticsClientOptions.ServiceVersion.V3_1"/>, <see cref="TextAnalyticsClientOptions.ServiceVersion.V2022_05_01"/>, and newer.
    /// </remarks>
    /// <param name="document">The document to analyze.</param>
    /// <param name="language">The language that the document is written in.
    /// If unspecified, this value will be set to the default language in
    /// <see cref="TextAnalyticsClientOptions.DefaultLanguage"/> in the request sent to the
    /// service.  If set to an empty string, the service will apply a model
    /// where the language is explicitly set to "None".</param>
    /// <param name="options">The additional configurable <see cref="RecognizePiiEntitiesOptions"/> that may be passed when
    /// recognizing PII entities. Options include entity domain filters, model version, and more.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>
    /// controlling the request lifetime.</param>
    /// <returns>A result containing the collection of entities identified
    /// in the document, as well as a score indicating the confidence
    /// that the entity correctly matches the identified substring.</returns>
    /// <exception cref="NotSupportedException">This method is only supported in service API version v3.1 and newer.</exception>
    /// <exception cref="RequestFailedException">Service returned a non-success
    /// status code.</exception>
    Task<RedactResult> RecognizePiiEntitiesAsync(
        string document,
        string? language = null,
        RecognizePiiEntitiesOptions? options = null,
        CancellationToken cancellationToken = default);
}

public class RedactResult : ReadOnlyCollection<SensitiveValue>
{
    public static readonly RedactResult Empty = new(string.Empty);

    public RedactResult(string originalText) : this(
        originalText,
        Array.Empty<TextAnalyticsWarning>(),
        Array.Empty<SensitiveValue>())
    {
    }

    public RedactResult(string originalText, IList<SensitiveValue> entities)
        : this(originalText, Array.Empty<TextAnalyticsWarning>(), entities)
    {
    }

    public RedactResult(string originalText, IReadOnlyCollection<TextAnalyticsWarning> warnings, IList<SensitiveValue> entities)
        : base(entities)
    {
        RedactedText = originalText;
        Warnings = warnings;
    }

    public RedactResult(PiiEntityCollection entityCollection) : this(
        entityCollection.RedactedText,
        entityCollection.Warnings,
        entityCollection.Select(SensitiveValue.Create).ToList())
    {
    }

    /// <summary>
    /// Gets the text of the input document with all of the Personally Identifiable Information
    /// redacted out.
    /// </summary>
    public string RedactedText { get; }

    /// <summary>
    /// Warnings encountered while processing the document.
    /// </summary>
    public IReadOnlyCollection<TextAnalyticsWarning> Warnings { get; }
}
