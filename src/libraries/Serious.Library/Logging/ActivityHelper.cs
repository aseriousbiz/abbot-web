using System.Diagnostics;

namespace Serious.Logging;

/// <summary>
/// Used to create and start a new <see cref="Activity"/> with a traceparent and tracestate if supplied.
/// </summary>
public static class ActivityHelper
{
    /// <summary>
    /// Create an <see cref="Activity"/> with the full type name of <typeparamref name="T"/> as the activity
    /// name and then starts the activity.
    /// </summary>
    /// <typeparam name="T">The type to use for the activity name.</typeparam>
    /// <returns>An <see cref="Activity"/> that is started.</returns>
    public static Activity CreateAndStart<T>()
    {
        var activityName = typeof(T).FullName ?? "UnknownActivity";
        return CreateAndStart(activityName, null, null);
    }

    /// <summary>
    /// Create an <see cref="Activity"/> with the full type name of <typeparamref name="T"/> as the activity
    /// name and then starts the activity.
    /// </summary>
    /// <typeparam name="T">The type to use for the activity name.</typeparam>
    /// <param name="traceParent">The parent activity Id of this activity following the W3C TraceContext spec.</param>
    /// <returns>An <see cref="Activity"/> that is started.</returns>
    public static Activity CreateAndStart<T>(string traceParent)
    {
        return CreateAndStart<T>(traceParent, null);
    }

    /// <summary>
    /// Create an <see cref="Activity"/> with the full type name of <typeparamref name="T"/> as the activity
    /// name and then starts the activity.
    /// </summary>
    /// <typeparam name="T">The type to use for the activity name.</typeparam>
    /// <param name="traceParent">The parent activity Id of this activity following the W3C TraceContext spec.</param>
    /// <param name="traceState">State of the trace to add to the activity.</param>
    /// <returns>An <see cref="Activity"/> that is started.</returns>
    public static Activity CreateAndStart<T>(string? traceParent, string? traceState)
    {
        var activityName = typeof(T).FullName ?? "UnknownActivity";
        return CreateAndStart(activityName, traceParent, traceState);
    }

    /// <summary>
    /// Create an <see cref="Activity"/> with the specified <paramref name="activityName"/>,
    /// <paramref name="traceParent"/>, and <see cref="traceState"/>. The trace state is only set
    /// if traceparent is not null.
    /// </summary>
    /// <param name="activityName">The name of the activity to create.</param>
    /// <param name="traceParent">The parent activity Id of this activity following the W3C TraceContext spec.</param>
    /// <param name="traceState">State of the trace to add to the activity.</param>
    /// <returns>An <see cref="Activity"/> that is started.</returns>
    public static Activity CreateAndStart(string activityName, string? traceParent, string? traceState)
    {
#pragma warning disable CA2000
        // activity.Start returns "this"
        var activity = new Activity(activityName);
#pragma warning restore CA2000
        if (traceParent is not null)
        {
            activity.SetParentId(traceParent);
            if (traceState is not null)
            {
                activity.TraceStateString = traceState;
            }
        }

        return activity.Start();
    }
}
