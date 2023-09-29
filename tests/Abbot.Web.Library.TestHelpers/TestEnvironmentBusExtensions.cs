namespace Abbot.Common.TestHelpers;

public static class TestEnvironmentBusExtensions
{
    public static async Task PublishAsync<T>(this TestEnvironment env, T message, CancellationToken cancellationToken = default)
        where T : class
    {
        await env.BusTestHarness.Bus.Publish(message, cancellationToken);
    }

    public static async Task PublishAndWaitForConsumptionAsync<T>(this TestEnvironment env, T message, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        where T : class
    {
        await env.BusTestHarness.Bus.Publish(message, cancellationToken);
        await env.ConsumerObserver.WaitForConsumptionAsync<T>(timeout, cancellationToken);
    }
}
