using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Serious.TestHelpers
{
    public class FakeEnvironment : IEnvironment, IEnumerable<KeyValuePair<string, string>>
    {
        readonly Dictionary<string, string> _environmentVariables = new();

        public void Add(string key, string value)
        {
            _environmentVariables.Add(key, value);
        }

        public string? this[string key]
        {
            get => GetEnvironmentVariable(key);
            set {
                if (value is not null)
                {
                    _environmentVariables[key] = value;
                }
                else
                {
                    _environmentVariables.Remove(key);
                }
            }
        }

        public string? GetEnvironmentVariable(string key)
        {
            return _environmentVariables.TryGetValue(key, out var value)
                ? value
                : null;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _environmentVariables.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource { get; } = new();
    }
}
