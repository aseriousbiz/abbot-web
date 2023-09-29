using System;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Clients;

public class SkillRunnerRetryPolicy : ISkillRunnerRetryPolicy
{
    readonly AsyncRetryPolicy _retryPolicy;

    public SkillRunnerRetryPolicy()
        : this(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
    {
    }

    public SkillRunnerRetryPolicy(int retryCount, Func<int, TimeSpan> retryDelayCalculator)
    {
        _retryPolicy = Policy
            .Handle<ServerUnavailableException>()
            .WaitAndRetryAsync(retryCount, retryDelayCalculator);
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action)
    {
        return await _retryPolicy.ExecuteAsync(action);
    }
}

public interface ISkillRunnerRetryPolicy
{
    Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action);
}
