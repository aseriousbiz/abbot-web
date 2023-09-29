using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Serious.TestHelpers;

public class FakeMemoryCache : MemoryCache
{
    public FakeMemoryCache() : base(Options.Create(new MemoryCacheOptions()))
    {
    }
}
