using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;
using Serious.Abbot.Storage;

namespace Serious.TestHelpers
{
    public class FakeBotUtilities : IUtilities
    {
        readonly IBrainSerializer? _serializer;
        public FakeBotUtilities(IBrainSerializer? serializer = null)
        {
            _serializer = serializer;
        }

        public Random CreateRandom()
        {
            return new Random();
        }

        public T GetRandomElement<T>(IReadOnlyList<T> list)
        {
            return list.First();
        }

        public Task<ILocation?> GetGeocodeAsync(string address, bool includeTimezone = false)
        {
            return Task.FromResult((ILocation?)null);
        }

        public bool TryParseSlackUrl(string url, out IMessageTarget? conversation)
        {
            conversation = null;
            return false;
        }

        public bool TryParseSlackUrl(Uri url, out IMessageTarget? conversation)
        {
            conversation = null;
            return false;
        }

        public string Serialize(object? value, bool withTypes = false)
        {
            return value is null ? string.Empty : _serializer!.SerializeObject(value, withTypes);
        }

        public T? Deserialize<T>(string? value)
        {
            return value is null ? default : _serializer!.Deserialize<T>(value);
        }
    }
}
