using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeSignaler : ISignaler
    {
        readonly Dictionary<(string, string), IResult> _results = new();

        public void AddResult(string name, string arguments, IResult result)
        {
            _results.Add((name, arguments), result);
        }

        public Task<IResult> SignalAsync(string name, string arguments)
        {
            return Task.FromResult(_results[(name, arguments)]);
        }
    }
}
