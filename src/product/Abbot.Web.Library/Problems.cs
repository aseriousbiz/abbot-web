using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Messages;
using Serious.Slack;

namespace Serious.Abbot;

public static class Problems
{
    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> representing a generic 404 Not Found result.
    /// </summary>
    /// <param name="title">The title of the <see cref="ProblemDetails"/>.</param>
    /// <param name="detail">An optional detail message. Defaults to the <paramref name="title"/> if not specified.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails NotFound(string title, string? detail = null) => new()
    {
        Type = "https://schema.ab.bot/problems/not-found",
        Status = StatusCodes.Status404NotFound,
        Title = title,
        Detail = detail ?? title,
    };

    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> representing a generic 400 Bad Request result.
    /// </summary>
    /// <param name="title">The title of the <see cref="ProblemDetails"/>.</param>
    /// <param name="detail">An optional detail message. Defaults to the <paramref name="title"/> if not specified.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails BadRequest(string title, string? detail = null) => new()
    {
        Type = "https://schema.ab.bot/problems/bad-request",
        Status = StatusCodes.Status400BadRequest,
        Title = title,
        Detail = detail ?? title,
    };

    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> indicating a program location that was thought to be unreachable was executed.
    /// The <see cref="ProblemDetails"/> equivalent to <see cref="UnreachableException"/>.
    /// </summary>
    /// <param name="detail">A detail message.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails Unreachable(string detail) => new()
    {
        Type = "https://schema.ab.bot/problems/unreachable",
        Status = StatusCodes.Status500InternalServerError,
        Title = "Unexpected error",
        Detail = detail,
    };

    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> indicating a program location that has not yet been implemented was executed.
    /// The <see cref="ProblemDetails"/> equivalent to <see cref="NotImplementedException"/>.
    /// </summary>
    /// <param name="detail">A detail message.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails NotImplemented(string detail) => new()
    {
        Type = "https://schema.ab.bot/problems/not-implemented",
        Status = StatusCodes.Status500InternalServerError,
        Title = "Not yet implemented",
        Detail = detail,
    };

    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> indicating the org has no Slack API token.
    /// </summary>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails SlackApiTokenMissing() => new()
    {
        Type = "https://schema.ab.bot/problems/slack-api-token-missing",
        Status = StatusCodes.Status500InternalServerError,
        Title = "Organization has no Slack API token",
        Detail = "Please make sure Abbot is installed in your workspace",
    };

    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> indicating an argument was invalid.
    /// </summary>
    /// <param name="argument">The name of the invalid argument.</param>
    /// <param name="detail">A detail message.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails ArgumentError(string argument, string detail) => new()
    {
        Type = "https://schema.ab.bot/problems/argument-error",
        Instance = $"/arguments/{argument}",
        Status = StatusCodes.Status400BadRequest,
        Title = $"Invalid argument: {argument}",
        Detail = detail,
    };

    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> indicating a playbook was invalid.
    /// </summary>
    /// <param name="detail">A detail message.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails InvalidPlaybook(string detail) => new()
    {
        Type = "https://schema.ab.bot/problems/invalid-playbook",
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid Playbook",
        Detail = detail,
    };

    /// <summary>
    /// Returns a <see cref="ProblemDetails"/> indicating the organization is disabled.
    /// </summary>
    /// <param name="detail">An optional detail message.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails OrganizationDisabled(string? detail = null) => new()
    {
        Type = "https://schema.ab.bot/problems/organization-disabled",
        Status = StatusCodes.Status400BadRequest,
        Title = "Organization is disabled.",
        Detail = detail ?? "Cannot perform this action on a disabled organization",
    };

    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from a Slack <see cref="ApiResponse"/>.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="title">The title for the problem.</param>
    /// <param name="additionalDetails">Any additional details to append.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails FromSlackErrorResponse(ApiResponse response, string title, string? additionalDetails = null) => new()
    {
        Type = $"https://schema.ab.bot/problems/slack/{response.Error}",
        Status = StatusCodes.Status500InternalServerError,
        Title = title,
        Detail = $"{response}" + (additionalDetails is not null ? $"\n{additionalDetails}" : ""),
    };

    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from an exception.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails FromException(Exception ex)
    {
        // ProblemDetails are generally intended for user consumption.
        // So we filter to certain user-friendly information.
        // Other information we place in the Extensions dictionary.

        ProblemDetails problem = ex switch
        {
            ValidationException vex => new ProblemDetails
            {
                Type = $"https://schema.ab.bot/problems/validation",
                Title = "Validation Error",
                Detail = vex.Message,
                Status = StatusCodes.Status400BadRequest,
            },
            _ => new()
            {
                Type = $"https://schema.ab.bot/problems/unreachable",
                Title = "Unexpected error",
                Detail = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
            }
        };

        // Using a nested object here doesn't seem to work, and I'm too tired to figure out why.
        problem.Extensions["exception_type"] = ex.GetType().FullName;
        problem.Extensions["exception_message"] = ex.Message;
        problem.Extensions["exception_stack"] = ex.StackTrace;

        return problem;
    }

    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from a <see cref="ExceptionInfo"/>
    /// </summary>
    /// <param name="ex">The <see cref="ExceptionInfo"/>.</param>
    /// <returns>A <see cref="ProblemDetails"/>.</returns>
    public static ProblemDetails FromException(ExceptionInfo ex)
    {
        // This one is only called when exceptions escape a Consumer
        // So we don't need to try and translate it to a ProblemDetails
        return new()
        {
            Type = "https://schema.ab.bot/problems/unreachable",
            Status = StatusCodes.Status500InternalServerError,
            Title = "Unexpected error",
            Detail = "An unexpected error occurred.",
            Extensions =
            {
                // Using a nested object here doesn't seem to work, and I'm too tired to figure out why.
                ["exception_type"] = ex.ExceptionType,
                ["exception_message"] = ex.Message,
                ["exception_stack"] = ex.StackTrace,
            }
        };
    }

    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from a <see cref="SkillRunResponse"/>
    /// </summary>
    /// <param name="response">The <see cref="SkillRunResponse"/>.</param>
    /// <returns>A <see cref="ProblemDetails"/> if the response was not successful, otherwise <c>null</c>.</returns>
    public static ProblemDetails? FromSkillRunResponse(SkillRunResponse response)
    {
        return response.Success
            ? null
            : new()
            {
                Title = "Skill run failed",
                Type = $"https://schema.ab.bot/problems/skill-run-failed",
            };
    }
}

public record ExceptionDetail
{
    public string? TypeName { get; init; }

    public string? Message { get; init; }

    public string? StackTrace { get; init; }
}

public static class ProblemDetailsExtensions
{
    public static IActionResult ToActionResult(this ProblemDetails problem)
    {
        return new ObjectResult(problem)
        {
            StatusCode = problem.Status ?? 500,
        };
    }
}
