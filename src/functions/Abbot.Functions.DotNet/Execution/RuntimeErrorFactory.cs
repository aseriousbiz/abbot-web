using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Functions;

public static class RuntimeErrorFactory
{
    public static RuntimeError Create(Exception exception, string skillName)
    {
        var stackTrace = new StackTrace(exception, true);
        var lineNumber = stackTrace
            .GetFrame(0)?
            .GetFileLineNumber();
        return new()
        {
            ErrorId = "Exception",
            Description = exception.GetType().FullName + ": " + exception.Message,
            StackTrace = CleanStackTrace(exception.StackTrace, skillName),
            LineStart = lineNumber - 1 ?? 0, // 0-based so we subtract 1
            LineEnd = lineNumber - 1 ?? 0    // 0-based so we subtract 1.
        };
    }

    static readonly Regex StackTraceRegex = new Regex(
        @"Submission#0\.(?:<+(?<method>.+?)>+[^(]+|(?<method>.+?)(?=\s))",
        RegexOptions.Compiled);

    static string? CleanStackTrace(string? stackTrace, string skillName)
    {
        if (stackTrace is null)
            return null;

        var initial = stackTrace.Replace("Submission#0.<<Initialize>>d__0.MoveNext()", skillName,
            StringComparison.Ordinal);
        var replaceSubmissions = StackTraceRegex.Replace(initial, "${method}");

        const string stackTraceMarker =
            "\n--- End of stack trace from previous location";
        var lastStackTraceMarker = replaceSubmissions.LastIndexOf(stackTraceMarker, StringComparison.Ordinal);
        if (lastStackTraceMarker > 0)
        {
            replaceSubmissions = replaceSubmissions.Substring(0, lastStackTraceMarker);
        }

        return replaceSubmissions;
    }
}
