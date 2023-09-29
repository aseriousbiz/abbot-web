using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Serious.Logging;

/// <summary>
/// Makes the <see cref="ILogger"/> or <see cref="ILogger{T}"/> provided to us via dependency injection
/// available to code via a static method.
/// See remarks for reasoning.
/// </summary>
/// <remarks>
/// <para>
/// Logging (for the most part) is a cross-cutting concern. I don't like injecting an ILogger into every class
/// as it adds yet another constructor argument and adds noise. I like each class to only have the necessary
/// ctor dependencies.
/// </para>
/// <para>
/// Also, if you add logging to a class later on, it breaks all the existing unit tests to add another ctor
/// argument. I'd just rather have our entry point configure the logger and then any other class can just call
/// <see cref="CreateLogger(string)"/>, <see cref="CreateLogger(Type)"/> or <see cref="CreateLogger{T}"/>
/// </para>
/// </remarks>
public static class ApplicationLoggerFactory
{
    static ILoggerFactory? _factory;

    /// <summary>
    /// Sets the <see cref="ILoggerFactory"/> to use to create loggers.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="dataProtector">An optional <see cref="ISensitiveLogDataProtector"/>.</param>
    public static void Configure(ILoggerFactory loggerFactory, ISensitiveLogDataProtector? dataProtector = null)
    {
        _factory = loggerFactory;
        DataProtector = dataProtector ?? ISensitiveLogDataProtector.Null;
    }

    /// <summary>
    /// Creates a new <see cref="ILogger"/> instance using the specified category name.
    /// If no logger is configured, returns a <see cref="NullLogger"/>.
    /// </summary>
    /// <param name="name">The category name for the logger.</param>
    /// <returns></returns>
    public static ILogger CreateLogger(string name) => _factory?.CreateLogger(name) ?? NullLogger.Instance;

    /// <summary>
    /// Creates a new <see cref="ILogger"/> instance using the full name of the given <paramref name="type"/>.
    /// If no logger is configured, returns a <see cref="NullLogger"/>.
    /// </summary>
    /// <param name="type">The type used to name the logger.</param>
    /// <returns></returns>
    public static ILogger CreateLogger(Type type) => _factory?.CreateLogger(type) ?? NullLogger.Instance;

    /// <summary>
    /// Creates a new <see cref="ILogger"/> instance using the full name of the given type.
    /// If no logger is configured, returns a <see cref="NullLogger"/>.
    /// </summary>
    /// <typeparam name="T">The type used to name the logger.</typeparam>
    /// <returns>The <see cref="ILogger{T}"/> that was created.</returns>
    public static ILogger<T> CreateLogger<T>() => _factory?.CreateLogger<T>() ?? NullLogger<T>.Instance;

    /// <summary>
    /// Returns an <see cref="ISensitiveLogDataProtector"/>.
    /// If not configured, returns <see cref="ISensitiveLogDataProtector.Null"/>.
    /// </summary>
    public static ISensitiveLogDataProtector DataProtector { get; private set; } = ISensitiveLogDataProtector.Null;
}
