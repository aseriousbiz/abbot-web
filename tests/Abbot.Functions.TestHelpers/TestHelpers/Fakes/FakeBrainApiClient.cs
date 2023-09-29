#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Messages;

namespace Serious.TestHelpers
{
    public class FakeBrainApiClient : IBrainApiClient
    {
        readonly Dictionary<string, string> _skillData = new();

        string? GetDataValue(string key)
        {
            return _skillData.TryGetValue(key.ToUpperInvariant(), out var value)
                ? value
                : null;
        }

        public Task<SkillDataResponse?> GetSkillDataAsync(string key)
        {
            var value = GetDataValue(key.ToUpperInvariant());
            return Task.FromResult(
                value is null
                    ? null
                    : new SkillDataResponse
                    {
                        Key = key.ToUpperInvariant(),
                        Value = value
                    });
        }

        public Task<SkillDataResponse?> GetSkillDataAsync(string key, SkillDataScope scope, string? contextId)
        {
            var value = GetDataValue(key.ToUpperInvariant());
            return Task.FromResult(
                value is null
                    ? null
                    : new SkillDataResponse
                    {
                        Key = key.ToUpperInvariant(),
                        Value = value
                    });
        }

        public Task<IReadOnlyList<string>> GetSkillDataKeysAsync()
        {
            return Task.FromResult(_skillData.Keys.ToReadOnlyList());
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllDataAsync()
        {
            return Task.FromResult(_skillData.ToReadOnlyDictionary());
        }

        public Task DeleteDataAsync(string key)
        {
            _skillData.Remove(key.ToUpperInvariant());
            return Task.CompletedTask;
        }

        public Task DeleteDataAsync(string key, SkillDataScope scope, string? contextId)
        {
            _skillData.Remove(key.ToUpperInvariant());
            return Task.CompletedTask;
        }

        public Task<SkillDataResponse?> PostDataAsync(string key, string value) =>
            PostDataAsync(key, value, SkillDataScope.Organization, null);

        public Task<SkillDataResponse?> PostDataAsync(
            string key,
            string value,
            SkillDataScope scope,
            string? contextId)
        {
            _skillData[key.ToUpperInvariant()] = value;

            return Task.FromResult((SkillDataResponse?)new SkillDataResponse
            {
                Key = key,
                Value = value
            });
        }
    }
}
