using System;
using System.Threading;
using System.Threading.Tasks;

namespace Serious.Tasks;

/// <summary>
/// Helper class for creating an asynchronous scope.
/// A scope is simply a using block that calls an async method
/// at the end of the block by returning an <see cref="IAsyncDisposable"/>.
/// This is the same concept as
/// the <see cref="Disposable.Create"/> method.
/// </summary>
public static class AsyncDisposable
{
    /// <summary>
    /// Creates an <see cref="IAsyncDisposable"/> that calls
    /// the specified method asynchronously at the end
    /// of the scope upon disposal.
    /// </summary>
    /// <param name="onDispose">The method to call at the end of the scope.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> that represents the scope.</returns>
    public static IAsyncDisposable Create(Func<ValueTask> onDispose)
    {
        return new AsyncScope(onDispose);
    }

    class AsyncScope : IAsyncDisposable
    {
        Func<ValueTask>? _onDispose;

        public AsyncScope(Func<ValueTask> onDispose)
        {
            _onDispose = onDispose;
        }

        public ValueTask DisposeAsync()
        {
            return Interlocked.Exchange(ref _onDispose, null)?.Invoke() ?? ValueTask.CompletedTask;
        }
    }
}
