using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeSecrets : ISecrets
    {
        readonly Dictionary<string, string> _secrets = new();

        public void AddSecret(string name, string value)
        {
            _secrets.Add(name, value);
        }

        public Task<string> GetAsync(string name)
        {
            _secrets.TryGetValue(name, out var secret);
            return Task.FromResult(secret!);
        }
    }
}
