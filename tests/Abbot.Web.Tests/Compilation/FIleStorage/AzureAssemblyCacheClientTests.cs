using System.Threading.Tasks;
using Serious.Abbot.Scripting;
using Serious.Abbot.Storage.FileShare;
using Serious.TestHelpers;
using Xunit;

public class AzureAssemblyCacheClientTests
{
    public class TheGetOrCreateAssemblyCacheAsyncMethod
    {
        [Fact]
        public async Task ReturnsDirectoryIfAlreadyExists()
        {
            var skillIdentifier = new FakeOrganizationIdentifier("T12345", PlatformType.Slack);
            var shareClient = new FakeShareClient();
            await shareClient.CreateIfNotExistsAsync();
            await shareClient.GetDirectoryClient("Slack-T12345").CreateIfNotExistsAsync();
            var assemblyFileShare = new AzureAssemblyCacheClient(shareClient);

            var cache = await assemblyFileShare.GetOrCreateAssemblyCacheAsync(skillIdentifier);

            Assert.NotNull(cache);
        }

        [Fact]
        public async Task CreatesDirectoryIfNotExists()
        {
            var skillIdentifier = new FakeOrganizationIdentifier("T12345", PlatformType.Slack);
            var shareClient = new FakeShareClient();
            await shareClient.CreateIfNotExistsAsync();
            var directory = shareClient.GetDirectoryClient("Slack-T12345");
            var assemblyFileShare = new AzureAssemblyCacheClient(shareClient);

            var cache = await assemblyFileShare.GetOrCreateAssemblyCacheAsync(skillIdentifier);

            Assert.NotNull(cache);
            Assert.True(await directory.ExistsAsync());
        }

        [Fact]
        public async Task CreatesShareAndDirectoryIfNeitherExist()
        {
            var skillIdentifier = new FakeOrganizationIdentifier("T12345", PlatformType.Slack);
            var shareClient = new FakeShareClient();
            var directory = shareClient.GetDirectoryClient("Slack-T12345");
            var assemblyFileShare = new AzureAssemblyCacheClient(shareClient);

            var cache = await assemblyFileShare.GetOrCreateAssemblyCacheAsync(skillIdentifier);

            Assert.NotNull(cache);
            Assert.True(await shareClient.ExistsAsync());
            Assert.True(await directory.ExistsAsync());
        }
    }

    public class TheGetAssemblyCacheAsyncMethod
    {
        [Fact]
        public async Task ReturnsDirectoryIfItExists()
        {
            var skillIdentifier = new FakeOrganizationIdentifier("T12345", PlatformType.Slack);
            var shareClient = new FakeShareClient();
            await shareClient.CreateIfNotExistsAsync();
            await shareClient.GetDirectoryClient("Slack-T12345").CreateIfNotExistsAsync();
            var assemblyFileShare = new AzureAssemblyCacheClient(shareClient);

            var cache = await assemblyFileShare.GetAssemblyCacheAsync(skillIdentifier);

            Assert.NotNull(cache);
        }

        [Fact]
        public async Task ReturnsNullIfDirectoryDoesNotExist()
        {
            var skillIdentifier = new FakeOrganizationIdentifier("T12345", PlatformType.Slack);
            var shareClient = new FakeShareClient();
            await shareClient.CreateIfNotExistsAsync();
            var assemblyFileShare = new AzureAssemblyCacheClient(shareClient);

            var cache = await assemblyFileShare.GetAssemblyCacheAsync(skillIdentifier);

            Assert.Null(cache);
        }

        [Fact]
        public async Task ReturnsNullIfShareDoesNotExist()
        {
            var skillIdentifier = new FakeOrganizationIdentifier("T12345", PlatformType.Slack);
            var shareClient = new FakeShareClient();
            var assemblyFileShare = new AzureAssemblyCacheClient(shareClient);

            var cache = await assemblyFileShare.GetAssemblyCacheAsync(skillIdentifier);

            Assert.Null(cache);
        }
    }
}
