using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers.Fakes;
using Serious;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Repositories;
using Serious.TestHelpers;
using Xunit;

public class DataSeedRunnerTests
{
    public class TheSeedDataAsyncMethod
    {
        private static void Arrange(out SettingsManager settingsManager, out UserRepository userRepository)
        {
            var db = new FakeAbbotContext();
            var clock = new TimeTravelClock();
            settingsManager = new SettingsManager(db, new FakeAuditLog(db, new FakeAnalyticsClient(), clock), clock);
            userRepository = new UserRepository(db, clock);
        }

        [Fact]
        public async Task RunsAllEnabledDataSeeders()
        {
            Arrange(out var settingsManager, out var userRepository);

            var seeders = new[]
            {
                new FakeSeeder
                {
                    Enabled = true
                },
                new FakeSeeder
                {
                    Enabled = false
                },
                new FakeSeeder
                {
                    Enabled = true
                }
            };
            var runner = new DataSeedRunner(seeders, settingsManager, userRepository);

            await runner.SeedDataAsync(false, default);
            await runner.SeedDataAsync(false, default);

            Assert.Equal(2, seeders[0].RunCount);
            Assert.Equal(0, seeders[1].RunCount);
            Assert.Equal(2, seeders[2].RunCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RunsOnlyBlockingOrNonBlockingSeedersAsRequested(bool runBlocking)
        {
            Arrange(out var settingsManager, out var userRepository);

            var seeders = new[]
            {
                new FakeSeeder
                {
                    Enabled = true,
                    BlockServerStartup = runBlocking,
                },
                new FakeSeeder
                {
                    Enabled = true,
                    BlockServerStartup = runBlocking,
                },
                new FakeSeeder
                {
                    Enabled = true,
                    BlockServerStartup = !runBlocking,
                }
            };
            var runner = new DataSeedRunner(seeders, settingsManager, userRepository);

            await runner.SeedDataAsync(runBlocking, default);

            Assert.Equal(1, seeders[0].RunCount);
            Assert.Equal(1, seeders[1].RunCount);
            Assert.Equal(0, seeders[2].RunCount);
        }

        [Fact]
        public async Task RunsAllOneTimeOnlyDataSeedersOnce()
        {
            Arrange(out var settingsManager, out var userRepository);

            var seeders = new[]
            {
                new FakeRunOnceSeeder
                {
                    Enabled = true,
                    Version = 1,
                },
                new FakeSeeder
                {
                    Enabled = false
                },
                new FakeSeeder
                {
                    Enabled = true
                }
            };
            var runner = new DataSeedRunner(seeders, settingsManager, userRepository);

            await runner.SeedDataAsync(false, default);
            await runner.SeedDataAsync(false, default);
            await runner.SeedDataAsync(false, default);

            Assert.Equal(1, seeders[0].RunCount);
            Assert.Equal(0, seeders[1].RunCount);
            Assert.Equal(3, seeders[2].RunCount);
        }

        [Fact]
        public async Task RunsAllOneTimeOnlyDataSeedersOnceUntilVersionIncremented()
        {
            Arrange(out var settingsManager, out var userRepository);

            var seeders = new[]
            {
                new FakeRunOnceSeeder
                {
                    Enabled = true,
                    Version = 1,
                },
                new FakeSeeder
                {
                    Enabled = false
                },
                new FakeSeeder
                {
                    Enabled = true
                }
            };
            var runner = new DataSeedRunner(seeders, settingsManager, userRepository);

            await runner.SeedDataAsync(false, default);
            await runner.SeedDataAsync(false, default);
            ((FakeRunOnceSeeder)seeders[0]).Version = 2;
            await runner.SeedDataAsync(false, default);

            Assert.Equal(2, seeders[0].RunCount);
            Assert.Equal(0, seeders[1].RunCount);
            Assert.Equal(3, seeders[2].RunCount);
        }
    }

    public class FakeSeeder : IDataSeeder
    {
        public int RunCount { get; private set; }

        public Task SeedDataAsync()
        {
            RunCount++;
            return Task.CompletedTask;
        }

        public bool Enabled { get; set; }

        public bool BlockServerStartup { get; set; }
    }

    public class FakeRunOnceSeeder : FakeSeeder, IRunOnceDataSeeder
    {
        public int Version { get; set; }
    }
}
