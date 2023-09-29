using Serious.Abbot.Storage.FileShare;

namespace Serious.TestHelpers
{
    public class FakeAssemblyClient : AzureAssemblyClient
    {
        public FakeAssemblyClient(
            IShareFileClient assemblyFileClient,
            IShareFileClient assemblySymbolsFileClient) : base(assemblyFileClient, assemblySymbolsFileClient)
        {
            FakeAssemblyFileClient = (FakeShareFileClient)assemblyFileClient;
            FakeAssemblySymbolsFileClient = (FakeShareFileClient)assemblySymbolsFileClient;
        }

        public FakeShareFileClient FakeAssemblyFileClient { get; }
        public FakeShareFileClient FakeAssemblySymbolsFileClient { get; }
    }
}
