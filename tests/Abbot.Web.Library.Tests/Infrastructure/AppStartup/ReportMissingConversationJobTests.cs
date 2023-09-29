using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Models;

public class ReportMissingConversationJobTests
{
    public class TheRunAsync
    {
        [Fact]
        public async Task CallsRepairerForEachBusinessOrganizationsAndFreeTrialPlansPrioritizingPaidSeats()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IMissingConversationsReporter>(out var repairer)
                .Build();
            env.StopwatchFactory.Elapsed = TimeSpan.FromHours(23);
            repairer.LogUntrackedConversationsAsync(Args.Organization)
                .Returns(Task.CompletedTask);
            var clock = env.Clock.Freeze();
            var businessOrg = await env.CreateOrganizationAsync(
                plan: PlanType.Business,
                purchasedSeats: 5);
            var paidBusinessOrg = await env.CreateOrganizationAsync(
                plan: PlanType.Business,
                purchasedSeats: 10);
            var freeOrg = await env.CreateOrganizationAsync(
                plan: PlanType.Free);
            var freeTrialPlanOrg = await env.CreateOrganizationAsync(
                plan: PlanType.Free,
                trialPlan: new TrialPlan(PlanType.Business, clock.AddDays(1)));
            var expiredFreeTrialPlanOrg = await env.CreateOrganizationAsync(
                plan: PlanType.Free,
                trialPlan: new TrialPlan(PlanType.Business, clock.AddDays(-1)));
            var disabledBusiness = await env.CreateOrganizationAsync(plan: PlanType.Business,
                name: "Disabled Org",
                enabled: false);
            var paidBusinessOrgWithNoManagedRooms = await env.CreateOrganizationAsync(
                plan: PlanType.Business,
                purchasedSeats: 12);
            var paidBusinessOrgWithNoApiToken = await env.CreateOrganizationAsync(
                plan: PlanType.Business,
                purchasedSeats: 12);
            paidBusinessOrgWithNoApiToken.ApiToken = null;
            await env.Db.SaveChangesAsync();
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: businessOrg);
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: paidBusinessOrg);
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: freeOrg);
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: freeTrialPlanOrg);
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: expiredFreeTrialPlanOrg);
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: disabledBusiness);
            await env.CreateRoomAsync(managedConversationsEnabled: false, org: paidBusinessOrgWithNoManagedRooms);
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: false, org: paidBusinessOrgWithNoManagedRooms);
            await env.CreateRoomAsync(managedConversationsEnabled: true, deleted: true, org: paidBusinessOrgWithNoManagedRooms);
            await env.CreateRoomAsync(managedConversationsEnabled: true, archived: true, org: paidBusinessOrgWithNoManagedRooms);
            await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: paidBusinessOrgWithNoApiToken);
            var job = env.Activate<ReportMissingConversationsJob>();

            await job.RunAsync();

            await repairer.Received().LogUntrackedConversationsAsync(paidBusinessOrg);
            await repairer.Received().LogUntrackedConversationsAsync(businessOrg);
            await repairer.Received().LogUntrackedConversationsAsync(freeTrialPlanOrg);
            await repairer.DidNotReceive().LogUntrackedConversationsAsync(freeOrg);
            await repairer.DidNotReceive().LogUntrackedConversationsAsync(expiredFreeTrialPlanOrg);
            await repairer.DidNotReceive().LogUntrackedConversationsAsync(paidBusinessOrgWithNoManagedRooms);
            await repairer.DidNotReceive().LogUntrackedConversationsAsync(disabledBusiness);
            await repairer.DidNotReceive().LogUntrackedConversationsAsync(paidBusinessOrgWithNoApiToken);
            var logMessage = Assert.Single(env.GetAllLogs<ReportMissingConversationsJob>());
            Assert.Equal("Repaired conversations in 3 organizations completed in 23 hours.", logMessage.Message);
        }

        [Fact]
        public async Task ReportsCancellation()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IMissingConversationsReporter>(out var repairer)
                .Build();
            env.StopwatchFactory.Elapsed = TimeSpan.FromSeconds(1);
            CancellationTokenSource source = new CancellationTokenSource();
            var cancellationToken = source.Token;
            repairer.LogUntrackedConversationsAsync(Args.Organization)
                .Returns(Task.CompletedTask)
                .AndDoes(_ => cancellationToken.ThrowIfCancellationRequested());
            var job = env.Activate<ReportMissingConversationsJob>();

            source.Cancel();
            await job.RunAsync(cancellationToken);

            var logMessage = Assert.Single(env.GetAllLogs<ReportMissingConversationsJob>());
            Assert.Equal("Repairing conversations job cancelled after 1 second.", logMessage.Message);
        }

        [Fact]
        public async Task ReportsNumberOfOrganizationsEnsuredWhenCancelledAfterSomeSuccesses()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IMissingConversationsReporter>(out var repairer)
                .Build();
            for (int i = 1; i <= 3; i++)
            {
                var org = await env.CreateOrganizationAsync(plan: PlanType.Business,
                    name: $"Org {i}",
                    purchasedSeats: 5);
                await env.CreateRoomAsync(managedConversationsEnabled: true, botIsMember: true, org: org);
            }
            env.StopwatchFactory.Elapsed = TimeSpan.FromSeconds(2);
            CancellationTokenSource source = new CancellationTokenSource();
            var cancellationToken = source.Token;
            repairer.LogUntrackedConversationsAsync(Arg.Is<Organization>(o => o.Name != "Org 3"), Args.CancellationToken)
                .Returns(Task.CompletedTask);
            repairer.LogUntrackedConversationsAsync(Arg.Is<Organization>(o => o.Name == "Org 3"), Args.CancellationToken)
                .Returns(Task.CompletedTask)
                .AndDoes(_ => {
                    source.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                });
            var job = env.Activate<ReportMissingConversationsJob>();

            await job.RunAsync(cancellationToken);

            var logMessage = Assert.Single(env.GetAllLogs<ReportMissingConversationsJob>());
            Assert.Equal("Repairing conversations job cancelled after completing 2 of 3 organizations in 2 seconds.", logMessage.Message);
        }
    }
}
