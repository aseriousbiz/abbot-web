using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using HandlebarsDotNet;
using HandlebarsDotNet.Extension.Json;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Evaluates handlebar templates against the <see cref="InputTemplateContext"/>.
/// </summary>
public interface ITemplateEvaluator
{
    /// <summary>
    /// Evaluates the template against the <see cref="InputTemplateContext"/>.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The resulting string.</returns>
    object? Evaluate(string template);
}

public class TemplateEvaluator : ITemplateEvaluator
{
    static readonly Histogram<long> TemplateDurationMetric =
        AbbotTelemetry.Meter.CreateHistogram<long>(
            "playbooks.template.duration",
            "milliseconds",
            "How long it takes to render an individual input template");

    static readonly IHandlebars HandlebarsCompiler = Handlebars.Create(
        configuration: new HandlebarsConfiguration().Configure(cfg => {
            cfg.UseJson();
            cfg.NoEscape = true;
        }));

    readonly InputTemplateContext _templateContext;
    readonly TagList _metricTags;

    public TemplateEvaluator(InputTemplateContext templateContext, in TagList metricTags)
    {
        _templateContext = templateContext;
        _metricTags = metricTags;
    }

    public object? Evaluate(string template)
    {
        if (OutputsRegex.Match(template) is { Success: true } match
            && match.Groups["key"].Value is { Length: > 0 } key
            && _templateContext.Outputs.TryGetValue(key, out var output))
        {
            // Short-circuit:
            return output;
        }

        if (TriggerOutputsRegex.Match(template) is { Success: true } triggerMatch
            && triggerMatch.Groups["key"].Value is { Length: > 0 } triggerKey
            && _templateContext.Trigger is { } trigger
            && trigger.Outputs.TryGetValue(triggerKey, out var triggerOutput))
        {
            // Short-circuit:
            return triggerOutput;
        }

        using var _ = TemplateDurationMetric.Time(_metricTags);
        // String inputs must be run through the templating engine.
        try
        {
            var compiled = HandlebarsCompiler.Compile(template);
            return compiled(_templateContext);
        }
        catch (HandlebarsException e)
        {
            throw new ValidationException(
                $"""
                Template error: {e.Message}

                Template:
                {template}
                """,
                e);
        }
    }

    static readonly Regex OutputsRegex = new(@"\{\{\s*outputs\.(?<key>[^.]+?)\s*\}\}", RegexOptions.Compiled);
    static readonly Regex TriggerOutputsRegex = new(@"\{\{\s*trigger\.outputs\.(?<key>[^.]+?)\s*\}\}", RegexOptions.Compiled);
}
