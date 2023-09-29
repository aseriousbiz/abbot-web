using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Storage.FileShare;
using Serious.TestHelpers;
using Xunit;

public class AzureAssemblyClientTests
{
    public class TheDownloadAssemblyAsyncMethod
    {
        [Fact]
        public async Task UsesAssemblyFileClientToDownloadAssembly()
        {
            var now = DateTimeOffset.UtcNow;
            var assemblyStream = new MemoryStream();
            await assemblyStream.WriteStringAsync("this is a test");
            var assemblyShareFileClient = new FakeShareFileClient("assembly")
            {
                Content = assemblyStream
            };
            var symbolsShareFileClient = new FakeShareFileClient("symbols");
            var assemblyClient = new AzureAssemblyClient(assemblyShareFileClient, symbolsShareFileClient);
            Assert.True(await assemblyClient.GetDateLastAccessedAsync() < now);

            var stream = await assemblyClient.DownloadAssemblyAsync();

            var result = await stream.ReadAsStringAsync();
            Assert.Equal("this is a test", result);
            Assert.True(await assemblyClient.GetDateLastAccessedAsync() >= now);
        }
    }

    public class TheDownloadSymbolsAsyncMethod
    {
        [Fact]
        public async Task UsesSymbolsFileClientToDownloadAssembly()
        {
            var assemblyStream = new MemoryStream();
            await assemblyStream.WriteStringAsync("this is a test of symbols");
            var assemblyShareFileClient = new FakeShareFileClient("assembly");
            var symbolsShareFileClient = new FakeShareFileClient("symbols") { Content = assemblyStream };
            var assemblyClient = new AzureAssemblyClient(assemblyShareFileClient, symbolsShareFileClient);

            var stream = await assemblyClient.DownloadSymbolsAsync();

            var result = await stream.ReadAsStringAsync();
            Assert.Equal("this is a test of symbols", result);
        }
    }

    public class TheUploadAsyncMethod
    {
        [Fact]
        public async Task UsesAssemblyAndSymbolsStreamToFileShare()
        {
            var now = DateTimeOffset.UtcNow;
            var assemblyStream = new MemoryStream();
            await assemblyStream.WriteStringAsync("this is assembly");
            var symbolsStream = new MemoryStream();
            await symbolsStream.WriteStringAsync("this is symbols");
            var assemblyClient = new FakeShareFileClient("CacheKey");
            var symbolsClient = new FakeShareFileClient("CacheKey.pdb");
            var client = new AzureAssemblyClient(assemblyClient, symbolsClient);
            Assert.True(await client.GetDateLastAccessedAsync() < now);

            await client.UploadAsync(assemblyStream, symbolsStream);

            var uploadedAssembly = await assemblyClient.Content!.ReadAsStringAsync();
            var uploadedSymbols = await symbolsClient.Content!.ReadAsStringAsync();
            Assert.Equal("this is assembly", uploadedAssembly);
            Assert.Equal("this is symbols", uploadedSymbols);
            Assert.True(await client.GetDateLastAccessedAsync() >= now);

        }
    }

    public class TheGetDateLastAccessedAsyncMethod
    {
        [Fact]
        public async Task RetrievesValueFromAssemblyClient()
        {
            var now = DateTimeOffset.UtcNow;
            var assemblyClient = new FakeShareFileClient("CacheKey");
            await assemblyClient.SetMetadataAsync(new Dictionary<string, string>
            {
                { "DateLastAccessed", now.ToString("o") }
            });

            var symbolsClient = new FakeShareFileClient("CacheKey.pdb");
            var client = new AzureAssemblyClient(assemblyClient, symbolsClient);

            var dateLastAccessed = await client.GetDateLastAccessedAsync();

            Assert.Equal(now, dateLastAccessed);
        }

        [Fact]
        public async Task RetrievesMinValueIfNoMetadataValue()
        {
            var assemblyClient = new FakeShareFileClient("CacheKey");
            var symbolsClient = new FakeShareFileClient("CacheKey.pdb");
            var client = new AzureAssemblyClient(assemblyClient, symbolsClient);

            var dateLastAccessed = await client.GetDateLastAccessedAsync();

            Assert.Equal(DateTimeOffset.MinValue, dateLastAccessed);
        }

        [Fact]
        public async Task RetrievesMinValueIfMetadataValueCorrupt()
        {
            var assemblyClient = new FakeShareFileClient("CacheKey");
            await assemblyClient.SetMetadataAsync(new Dictionary<string, string> { { "DateLastAccessed", "Garbage" } });
            var symbolsClient = new FakeShareFileClient("CacheKey.pdb");
            var client = new AzureAssemblyClient(assemblyClient, symbolsClient);

            var dateLastAccessed = await client.GetDateLastAccessedAsync();

            Assert.Equal(DateTimeOffset.MinValue, dateLastAccessed);
        }
    }

    public class TheSetDateLastAccessedAsyncMethod
    {
        [Fact]
        public async Task SetsDateOnAssemblyClient()
        {
            var now = DateTimeOffset.UtcNow;
            var assemblyClient = new FakeShareFileClient("CacheKey");
            var symbolsClient = new FakeShareFileClient("CacheKey.pdb");
            var client = new AzureAssemblyClient(assemblyClient, symbolsClient);

            await client.SetDateLastAccessedAsync(now);

            Assert.Equal(now, await client.GetDateLastAccessedAsync());
            Assert.Equal(
                now.ToString("o"),
                (await assemblyClient.GetMetadataAsync())["DateLastAccessed"]);
        }
    }

    public class TheDeleteIfExistsAsyncMethod
    {
        [Fact]
        public async Task DeletesAssemblyAndSymbols()
        {
            var assemblyClient = new FakeShareFileClient("CacheKey");
            await assemblyClient.CreateAsync(123);
            var symbolsClient = new FakeShareFileClient("CacheKey.pdb");
            await symbolsClient.CreateAsync(123);
            var client = new AzureAssemblyClient(assemblyClient, symbolsClient);
            Assert.True(await client.ExistsAsync());
            Assert.True(await client.SymbolsExistAsync());
            Assert.True(await assemblyClient.ExistsAsync());
            Assert.True(await symbolsClient.ExistsAsync());

            await client.DeleteIfExistsAsync();

            Assert.False(await client.ExistsAsync());
            Assert.False(await client.SymbolsExistAsync());
            Assert.False(await assemblyClient.ExistsAsync());
            Assert.False(await symbolsClient.ExistsAsync());
        }
    }
}
