using System;
using System.Reflection;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using NSubstitute;

namespace Serious.TestHelpers;

public class FakePerformContext : PerformContext
{
    public FakePerformContext(int retryCount = 1)
        : base(
            new FakeJobStorage(),
            CreateStorageConnection(retryCount),
            new BackgroundJob("JobID", new FakeJob(), DateTime.UtcNow),
            Substitute.For<IJobCancellationToken>())
    {
    }

    static IStorageConnection CreateStorageConnection(int retryCount = 1)
    {
        var connection = Substitute.For<IStorageConnection>();
        connection.GetJobParameter(Args.String, "RetryCount").Returns(retryCount.ToString());
        return connection;
    }
}

public class FakeJob : Job
{
    public static MethodInfo GetMethod()
    {
        return typeof(FakeJob).GetMethod(nameof(GetMethod), BindingFlags.Static | BindingFlags.Public)
               ?? throw new InvalidOperationException("Method doesn't exist with those binding flags");
    }

    public FakeJob() : base(GetMethod())
    {
    }
}

public class FakeJobStorage : JobStorage
{
    public override IMonitoringApi GetMonitoringApi()
    {
        throw new NotImplementedException();
    }

    public override IStorageConnection GetConnection()
    {
        throw new NotImplementedException();
    }
}
