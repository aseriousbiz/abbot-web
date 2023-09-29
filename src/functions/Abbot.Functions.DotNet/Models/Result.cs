using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Models;

/// <summary>
/// The result of an operation to retrieve a value that could fail. If it fails, <see cref="Result.Ok"/> will be false
/// and <see cref="Result.Error"/> will contain the reason for the failure.
/// </summary>
/// <typeparam name="TValue">The type of the operation result</typeparam>
public class Result<TValue> : Result, IResult<TValue>
{
    /// <summary>
    /// Creates a success result.
    /// </summary>
    /// <param name="value">The retrieved value.</param>
    public Result(TValue value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The error message.</param>
    public Result(string error) : base(error)
    {
    }

    /// <summary>
    /// The value of the operation if it succeeded.
    /// </summary>
    public TValue? Value { get; set; }
}

/// <summary>
/// The result of an operation to retrieve a value that could fail. If it fails, <see cref="Result.Ok"/> will be false
/// and <see cref="Result.Error"/> will contain the reason for the failure.
/// </summary>
public class Result : IResult
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public Result()
    {
        Ok = true;
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The error message.</param>
    public Result(string error)
    {
        Ok = false;
        Error = error;
    }

    public Result(IResult result)
    {
        Ok = result.Ok;
        Error = result.Error;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Ok { get; }

    /// <summary>
    /// The error message, if any.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Creates a <see cref="Result{TValue}"/> from a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A successful result with the specified value.</returns>
    public static Result<TValue> FromValue<TValue>(TValue value)
    {
        return new(value);
    }

    /// <summary>
    /// Creates a <see cref="Result{TValue}"/> from a value.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A successful result with the specified value.</returns>
    public static Result<TValue> FromError<TValue>(string error)
    {
        return new Result<TValue>(error);
    }
}
