using System.Threading;
using System.Threading.Tasks;

namespace Serious.Abbot.Execution;

static class TaskExtensions
{
    const int TimeoutInMs = 2000;

    public static bool RunSync(this Task task)
    {
        // aspnet core doesn't have a synchronization context, so blocking waits won't deadlock, we can do them live
        if (SynchronizationContext.Current == null)
        {
            return task.Wait(TimeoutInMs);
        }

        // this is for all of you other environments out there with potential deadlocking contexts
        return Task.Run(() => task).Wait(TimeoutInMs);
    }
}
