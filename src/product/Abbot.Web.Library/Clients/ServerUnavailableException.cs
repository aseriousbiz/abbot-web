using System;

namespace Serious.Abbot.Messages;

/// <summary>
/// Exception thrown when the server is known to be temporarily down.
/// </summary>
public class ServerUnavailableException : Exception
{
    public ServerUnavailableException()
    {
    }

    public ServerUnavailableException(string message) : base(message)
    {
    }

    public ServerUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
