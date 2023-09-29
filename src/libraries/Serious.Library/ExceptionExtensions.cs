using System;
using System.Reflection;

namespace Serious;

public static class ExceptionExtensions
{
    /// <summary>
    /// If the exception is a <see cref="TargetInvocationException" />, this returns the inner exception if it is
    /// not null.
    /// </summary>
    /// <param name="e">The exception</param>
    public static Exception Unwrap(this Exception e)
    {
        return e is TargetInvocationException { InnerException: not null } te
            ? te.InnerException!
            : e;
    }

    /// <summary>
    /// Searches the exception and its inner exceptions for an exception of type <typeparamref name="T" />.
    /// </summary>
    /// <param name="e">The exception.</param>
    /// <typeparam name="T">The type of exception to look for.</typeparam>
    /// <returns>The exception of type T or null.</returns>
    public static T? FindInnerException<T>(this Exception e) where T : Exception
    {
        while (true)
        {
            if (e is T t)
            {
                return t;
            }

            if (e.InnerException is null)
            {
                return null;
            }

            e = e.InnerException;
        }
    }
}
