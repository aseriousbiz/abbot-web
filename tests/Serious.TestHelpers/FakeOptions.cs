using Microsoft.Extensions.Options;

namespace Serious.TestHelpers;

public class FakeOptions<TOptions> : IOptions<TOptions> where TOptions : class, new()
{
    public FakeOptions(TOptions value)
    {
        Value = value;
    }

    public TOptions Value { get; }
}
