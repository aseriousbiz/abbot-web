#nullable enable

using Microsoft.Bot.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Serious;
using Serious.Abbot.Functions.DotNet;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class AzureFunctionsDependencyInjectionTests
{
    [Fact]
    public void ValidateDependencies()
    {
        // if any services are missing, this will throw
        using var _ = new ScopedProvider(CreateServiceProvider);
    }

    [Fact]
    public void EnvironmentListensToShutdown()
    {
        using var serviceProvider = new ScopedProvider(CreateServiceProvider);
        var environment = serviceProvider.GetService<IEnvironment>()!;
        var application = serviceProvider.GetService<IHostApplicationLifetime>()!;

        Assert.False(environment.CancellationToken.IsCancellationRequested);
        application.StopApplication();
        Assert.True(environment.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void ScopedServiceProviderIsProperlyScoped()
    {
        IEnvironment? environment1, environment2;
        ISkillContextAccessor? context1, context2, context3;
        IBotReplyClient? replyClient1, replyClient2, replyClient3;
        ISlack? slack1, slack2, slack3;
        using (var serviceProvider = new ScopedProvider(CreateServiceProvider))
        {
            environment1 = serviceProvider.GetService<IEnvironment>()!;
            context1 = serviceProvider.GetService<ISkillContextAccessor>();
            context2 = serviceProvider.GetService<ISkillContextAccessor>();
            replyClient1 = serviceProvider.GetService<IBotReplyClient>();
            replyClient2 = serviceProvider.GetService<IBotReplyClient>();
            slack1 = serviceProvider.GetService<ISlack>();
            slack2 = serviceProvider.GetService<ISlack>();

            serviceProvider.CreateScope(); // this disposes the inner scope and creates a new one
            environment2 = serviceProvider.GetService<IEnvironment>()!;
            context3 = serviceProvider.GetService<ISkillContextAccessor>();
            replyClient3 = serviceProvider.GetService<IBotReplyClient>();
            slack3 = serviceProvider.GetService<ISlack>();
        }

        // singleton should be the same instance every time
        Assert.NotNull(environment1);
        Assert.NotNull(environment2);
        Assert.Equal(environment1, environment2);


        // transient should be different every time it's requested
        Assert.NotNull(slack1);
        Assert.NotNull(slack2);
        Assert.NotNull(slack3);
        Assert.NotEqual(slack1, slack2);
        Assert.NotEqual(slack1, slack3);

        // scoped should be different per scoped request
        Assert.NotNull(context1);
        Assert.NotNull(context2);
        Assert.NotNull(context3);
        Assert.Equal(context1, context2);
        Assert.NotEqual(context1, context3);

        Assert.NotNull(replyClient1);
        Assert.NotNull(replyClient2);
        Assert.NotNull(replyClient3);
        Assert.Same(replyClient1, replyClient2);
        Assert.NotSame(replyClient1, replyClient3);

    }

    static ServiceProvider CreateServiceProvider(IServiceCollection serviceCollection)
    {
        var cts = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.When(x => x.StopApplication()).Do(_ => cts.Cancel());
        lifetime.ApplicationStopping.Returns(cts.Token);

        var configuration = Substitute.For<IConfiguration>();

        serviceCollection.AddSingleton(configuration);
        serviceCollection.AddSingleton(lifetime);

        serviceCollection.RegisterAbbotServices();

        serviceCollection.ReplaceIt(_ => new HttpClient());
        serviceCollection.ReplaceIt(_ => Substitute.For<IBotTelemetryClient>());

        return serviceCollection.BuildServiceProvider(true);
    }
}
