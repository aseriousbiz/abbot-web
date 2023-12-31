using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// CREDIT: https://blog.stephencleary.com/2012/08/asynchronous-lazy-initialization.html
namespace Serious.Tasks;

/// <summary>
/// Provides support for asynchronous lazy initialization. This type is fully threadsafe.
/// </summary>
/// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
public sealed class AsyncLazy<T>
{
    /// <summary>
    /// The underlying lazy task.
    /// </summary>
    readonly Lazy<Task<T>> _instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
    /// </summary>
    /// <param name="factory">The delegate that is invoked on a background thread to produce the value when it is needed.</param>
    public AsyncLazy(Func<T> factory)
    {
        _instance = new Lazy<Task<T>>(() => Task.Run(factory));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
    /// </summary>
    /// <param name="factory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
    public AsyncLazy(Func<Task<T>> factory)
    {
        _instance = new Lazy<Task<T>>(() => Task.Run(factory));
    }

    /// <summary>
    /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy&lt;T&gt;"/> to be await'ed.
    /// </summary>
    public TaskAwaiter<T> GetAwaiter()
    {
        return _instance.Value.GetAwaiter();
    }

    /// <summary>
    /// Starts the asynchronous initialization, if it has not already started.
    /// </summary>
    public void Start()
    {
        var unused = _instance.Value;
    }
}

public sealed class AsyncLazy
{
    /// <summary>
    /// The underlying lazy task.
    /// </summary>
    readonly Lazy<Task> _instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy"/> class.
    /// </summary>
    /// <param name="factory">The delegate that is invoked on a background thread to produce the value when it is needed.</param>
    public AsyncLazy(Action factory)
    {
        _instance = new Lazy<Task>(() => Task.Run(factory));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy"/> class.
    /// </summary>
    /// <param name="factory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
    public AsyncLazy(Func<Task> factory)
    {
        _instance = new Lazy<Task>(() => Task.Run(factory));
    }

    /// <summary>
    /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy"/> to be await'ed.
    /// </summary>
    public TaskAwaiter GetAwaiter()
    {
        return _instance.Value.GetAwaiter();
    }
}
