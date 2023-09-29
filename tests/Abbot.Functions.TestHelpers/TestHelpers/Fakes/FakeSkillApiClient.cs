using Serious.Abbot.Execution;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;

namespace Serious.TestHelpers
{
    public class FakeSkillApiClient : ISkillApiClient
    {
        readonly int _skillId;

        public FakeSkillApiClient(int skillId)
        {
            _skillId = skillId;
        }

        readonly Dictionary<(Uri, HttpMethod), List<object>> _sentJson = new();
        readonly Dictionary<(string, bool), ICompiledSkill> _assemblyDownloads = new();
        readonly HashSet<(string, bool)> _badFormatExceptions = new();
        readonly Dictionary<(Uri, HttpMethod), object> _responses = new();

        public void AddResponse(Uri url, object o)
        {
            _responses.Add((url, HttpMethod.Get), o);
            _responses.Add((url, HttpMethod.Post), o);
            _responses.Add((url, HttpMethod.Delete), o);
            _responses.Add((url, HttpMethod.Put), o);
        }

        public void AddResponse(Uri url, HttpMethod method, object o)
        {
            _responses.Add((url, method), o);
        }

        public Uri BaseApiUrl => new Uri($"https://ab.bot/api/skills/{_skillId}");

        public Task<TResponse?> SendAsync<TResponse>(Uri url, HttpMethod method) where TResponse : class
        {
            _responses.TryGetValue((url, method), out var response);
            return Task.FromResult(response as TResponse);
        }

        public Task<AbbotResponse> SendApiAsync(Uri url, HttpMethod method)
        {
            throw new NotImplementedException();
        }

        public Task<AbbotResponse<TResponse>> SendApiAsync<TResponse, TResponseContent>(Uri url, HttpMethod method)
            where TResponse : class
            where TResponseContent : class, TResponse
        {
            throw new NotImplementedException();
        }

        public Task<AbbotResponse<TResponse>> SendApiAsync<TRequestContent, TResponse, TResponseContent>(Uri url, HttpMethod method,
            TRequestContent requestContent) where TResponse : class where TResponseContent : class, TResponse
        {
            throw new NotImplementedException();
        }

        public Task<HttpResponseMessage> SendAsync(Uri url, HttpMethod method)
        {
            _responses.TryGetValue((url, method), out var response);

            if (response is Func<HttpResponseMessage> func)
            {
                response = func();
            }
            else
            {
                response = response as HttpResponseMessage;
            }

            return Task.FromResult((HttpResponseMessage)response!);
        }

        public Task<TResponseContent?> SendJsonAsync<TRequestContent, TResponseContent>(
            Uri url,
            HttpMethod method,
            TRequestContent data) where TResponseContent : class
        {
            if (_sentJson.TryGetValue((url, method), out var existing))
            {
                existing.Add(data!);
            }
            else
            {
                _sentJson.Add((url, method), new List<object> { data! });
            }

            _responses.TryGetValue((url, method), out var response);
            return response switch
            {
                Func<Task<TResponseContent?>> func => func(),
                Func<TResponseContent?> func2 => Task.FromResult(func2()),
                _ => Task.FromResult(response as TResponseContent)
            };
        }

        public Task<ICompiledSkill> DownloadCompiledSkillAsync(
            ICompiledSkillIdentifier skillAssemblyIdentifier,
            bool recompile)
        {
            return _badFormatExceptions.Contains((GetCacheKey(skillAssemblyIdentifier), recompile))
                ? Task.FromException<ICompiledSkill>(new BadImageFormatException())
                : Task.FromResult(_assemblyDownloads[(GetCacheKey(skillAssemblyIdentifier), recompile)]);
        }

        public void AddAssemblyDownload(
            ICompiledSkillIdentifier skillAssemblyIdentifier,
            ICompiledSkill skillAssembly,
            bool recompile = false)
        {
            _assemblyDownloads.Add((GetCacheKey(skillAssemblyIdentifier), recompile), skillAssembly);
        }

        public void AddBadFormatException(ICompiledSkillIdentifier skillAssemblyIdentifier, bool recompile)
        {
            _badFormatExceptions.Add((GetCacheKey(skillAssemblyIdentifier), recompile));
        }

        static string GetCacheKey(ICompiledSkillIdentifier skillAssemblyIdentifier)
        {
            return $"{skillAssemblyIdentifier.PlatformType}:{skillAssemblyIdentifier.PlatformId}:{skillAssemblyIdentifier.CacheKey}";
        }

        public Dictionary<(Uri, HttpMethod), List<object>> SentJson => _sentJson;
    }
}
