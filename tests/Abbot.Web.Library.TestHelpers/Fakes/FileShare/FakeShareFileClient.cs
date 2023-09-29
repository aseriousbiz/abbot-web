using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Serious.Abbot.Storage.FileShare;
using Xunit;

namespace Serious.TestHelpers
{
    public class FakeShareFileClient : IShareFileClient
    {
        IDictionary<string, string> _metadata = new Dictionary<string, string>();

        public FakeShareFileClient(string name)
        {
            Name = name;
        }

        public bool Exists { get; set; }

        public Exception? ThrowOnCreate { get; set; }

        public Stream? Content { get; set; }

        public string Name { get; }

        public Task CreateAsync(long maxSize)
        {
            if (ThrowOnCreate is not null)
            {
                throw ThrowOnCreate;
            }
            Exists = true;
            return Task.CompletedTask;
        }

        public Task UploadRangeAsync(HttpRange range, Stream content)
        {
            // Ensure we're reading from the beginning
            Assert.Equal(0, content.Position);

            // Copy this because the source stream will be disposed later.
            Content = new MemoryStream();
            return content.CopyToAsync(Content);
        }

        public Task<Stream> DownloadAsync()
        {
            return Task.FromResult(Content ?? new MemoryStream());
        }

        public Task<bool> ExistsAsync()
        {
            return Task.FromResult(Exists);
        }

        public Task<bool> DeleteIfExistsAsync()
        {
            bool exists = Exists;
            Exists = false;
            return Task.FromResult(exists);
        }

        public Task<IDictionary<string, string>> GetMetadataAsync()
        {
            return Task.FromResult(_metadata);
        }

        public Task SetMetadataAsync(IDictionary<string, string> metadata)
        {
            _metadata = metadata;
            return Task.CompletedTask;
        }
    }
}
