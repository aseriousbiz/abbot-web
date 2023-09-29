using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Refit;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents a response from an Abbot API
/// </summary>
public record AbbotResponse : IResult // Implement this so we can return this from legacy clients.
{
    /// <summary>
    /// Gets a boolean indicating whether the response was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Problem))]
    public virtual bool Successful => this is { StatusCode: >= 200 and < 300, Problem: null };

    /// <summary>
    /// Gets the HTTP status code of the response
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets a <see cref="ProblemDetails"/> describing the failure if <see cref="Success"/> is false.
    /// </summary>
    public virtual ProblemDetails? Problem { get; init; }

    /// <summary>
    /// Creates a successful <see cref="AbbotResponse"/> with no payload.
    /// </summary>
    public static AbbotResponse Success(int statusCode) => new() { StatusCode = statusCode };

    /// <summary>
    /// Creates a successful <see cref="AbbotResponse"/> with the specified payload
    /// </summary>
    public static AbbotResponse<T> Success<T>(int statusCode, T body) => new() { StatusCode = statusCode, Body = body };

    /// <summary>
    /// Creates a failed <see cref="AbbotResponse"/> with the specified Problem.
    /// </summary>
    public static AbbotResponse Error(ProblemDetails problem) => new()
    {
        StatusCode = problem.Status,
        Problem = problem
    };

    /// <summary>
    /// Creates a failed <see cref="AbbotResponse{T}"/> with the specified Problem.
    /// </summary>
    public static AbbotResponse<T> Error<T>(ProblemDetails problem) => new()
    {
        StatusCode = problem.Status,
        Problem = problem
    };

    /// <summary>
    /// Gets a boolean indicating whether the response was successful.
    /// </summary>
#pragma warning disable CA1033
    bool IResult.Ok => Successful;
#pragma warning restore CA1033

    /// <summary>
    /// Describes the failure if <see cref="IResult.Ok"/> is false.
    /// </summary>
#pragma warning disable CA1033
    string? IResult.Error => Problem.GetErrors();
#pragma warning restore CA1033
}

/// <summary>
/// Extensions to <see cref="ProblemDetails"/>.
/// </summary>
public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Gets a string describing the errors in a <see cref="ProblemDetails"/> object.
    /// </summary>
    /// <param name="problem"></param>
    /// <returns></returns>
    public static string? GetErrors(this ProblemDetails? problem)
    {
        if (problem is null)
        {
            return null;
        }

        // Concatenate the errors into a single string
        var text = "";
        if (problem.Title is not null)
        {
            text += $"Title: {problem.Title}\n";
        }
        text += problem.Errors.Aggregate("Errors: ", (a, b) => $"{a}\n{b.Key}={b.Value}");
        if (problem.Detail is not null)
        {
            text += $"\nDetail: {problem.Detail}";
        }

        return text;
    }
}

/// <summary>
/// Represents a response from an Abbot API that includes a payload.
/// </summary>
public record AbbotResponse<T> : AbbotResponse
{
    /// <summary>
    /// Gets a boolean indicating whether the response was successful.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Body))]
    [MemberNotNullWhen(false, nameof(Problem))]
    public override bool Successful => this is { StatusCode: >= 200 and < 400, Problem: null, Body: not null };

    /// <summary>
    /// Gets the body of the response if <see cref="Successful"/> is true.
    /// </summary>
    public T? Body { get; init; }

    /// <summary>
    /// Gets a <see cref="ProblemDetails"/> describing the failure if <see cref="Successful"/> is false.
    /// </summary>
    // Must override to target with MemberNotNullWhen 😞
    public override ProblemDetails? Problem { get; init; }

    /// <summary>
    /// If the status code is not successful, an exception is thrown. Otherwise this returns the response body.
    /// </summary>
    /// <returns>The response body, if the response resulted from a successful request.</returns>
    /// <exception cref="HttpRequestException">The exception to throw if the request failed.</exception>
    public T RequireSuccess()
    {
        if (!Successful)
        {
            throw new HttpRequestException(Problem?.Detail, null, (HttpStatusCode)StatusCode);
        }

        return Body;
    }
}

/// <summary>
/// Useful extensions related to AbbotResponse.
/// </summary>
public static class AbbotResponseExtensions
{
    /// <summary>
    /// If the status code is not successful, an exception is thrown. Otherwise this returns the response body.
    /// </summary>
    /// <returns>The response body, if the response resulted from a successful request.</returns>
    /// <exception cref="HttpRequestException">The exception to throw if the request failed.</exception>
    public static async Task<T> RequireSuccess<T>(this Task<AbbotResponse<T>> responseTask)
        => (await responseTask).RequireSuccess();
}
