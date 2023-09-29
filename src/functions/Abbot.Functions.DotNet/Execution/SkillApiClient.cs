using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Reflection;
using System.Threading.Tasks;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Messages;
using Serious.Abbot.Serialization;
using Serious.Runtime;

namespace Serious.Abbot.Functions.Storage;

/// <summary>
/// Api Client for the skill runner APIs hosted on abbot-web. This class understands the authentication
/// mechanism when calling a skill runner API.
/// </summary>
public sealed class SkillApiClient : ISkillApiClient
{
    readonly HttpClient _httpClient;
    readonly IEnvironment _environment;
    readonly ISkillContextAccessor _skillContextAccessor;

    /// <summary>
    /// Constructs an instance of <see cref="SkillApiClient"/>.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to make a request to the Abbot skill runner APIs.</param>
    /// <param name="environment">An <see cref="IEnvironment"/> used to access environment variables.</param>
    /// <param name="skillContextAccessor">A <see cref="ISkillContextAccessor"/> used to access the current <see cref="SkillContext"/>.</param>
    public SkillApiClient(
        HttpClient httpClient,
        IEnvironment environment,
        ISkillContextAccessor skillContextAccessor)
    {
        _httpClient = httpClient;
        _environment = environment;
        _skillContextAccessor = skillContextAccessor;
    }

    /// <summary>
    /// Calls the api/skills/{id}/compilation api to download a compiled skill and
    /// symbols (if appropriate) and returns it.
    /// </summary>
    /// <param name="skillIdentifier">Uniquely identifies a skill assembly.</param>
    /// <param name="recompile">Whether to force recompile</param>
    public async Task<ICompiledSkill> DownloadCompiledSkillAsync(
        ICompiledSkillIdentifier skillIdentifier,
        bool recompile)
    {
        using var container = new DisposableContainer();

        var compilationUrl = CompilationUri;
        var compilationRequestBody = new CompilationRequest(skillIdentifier)
        {
            Type = recompile
                ? CompilationRequestType.Recompile
                : CompilationRequestType.Cached
        };

        var compilationTask = PrepareRequest(compilationUrl, compilationRequestBody, container);
        var symbolsTask = skillIdentifier.Language == CodeLanguage.CSharp
            ? PrepareRequest(compilationUrl, compilationRequestBody.ToSymbolsRequest(), container)
            : Task.FromResult<HttpResponseMessage>(null!);

        var (compilationResponse, (symbolsResponse, _)) = await Task.WhenAll(compilationTask, symbolsTask);

        if (!compilationResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"HTTP Exception ({compilationResponse.StatusCode}) "
                + $"trying to download assembly for Skill: {skillIdentifier.SkillName} "
                + $"({skillIdentifier.SkillId}) from `{compilationUrl}`.");
        }

        var assemblyStream = await compilationResponse.Content.ReadAsStreamAsync();

        ICompiledSkill result;

        switch (skillIdentifier.Language)
        {
            case CodeLanguage.CSharp:
                {
                    var symbolsStream = Stream.Null;
                    if (symbolsResponse.IsSuccessStatusCode)
                    {
                        symbolsStream = await symbolsResponse.Content.ReadAsStreamAsync();
                    }

                    var assembly = LoadAssembly(assemblyStream, symbolsStream);
                    result = new CSharpAssembly(assembly);

                    break;
                }
            case CodeLanguage.Ink:
                {
                    using var reader = new StreamReader(assemblyStream);
                    string json = await reader.ReadToEndAsync();
                    result = new CompiledInkScript(skillIdentifier.SkillName, json);
                    break;
                }
            default:
                throw new InvalidOperationException($"Language {skillIdentifier.Language} not supported");
        }

        return result;
    }

    Task<HttpResponseMessage> PrepareRequest(Uri requestUrl, CompilationRequest requestBody, DisposableContainer disposable)
    {
        var request = PrepareRequest(requestUrl, HttpMethod.Post, SkillContext.SkillRunnerInfo.Timestamp).RegisterForDispose(disposable);
        request.AddJsonContent(requestBody);
        return _httpClient.SendAsync(request, _environment.CancellationToken);
    }

    static Assembly LoadAssembly(Stream assembly, Stream assemblySymbols)
    {
        var context = new CollectibleAssemblyLoadContext();
        assembly.Position = 0;
        if (assemblySymbols != Stream.Null)
        {
            assemblySymbols.Position = 0;
            return context.LoadFromStream(assembly, assemblySymbols);
        }
        return context.LoadFromStream(assembly);
    }

    /// <inheritdoc />
    public async Task<AbbotResponse<TResponse>> SendApiAsync<TResponse, TResponseContent>(Uri url, HttpMethod method)
        where TResponse : class
        where TResponseContent : class, TResponse
    {
        var response = await SendAsync(url, method);
        var responseJson = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            var payload = AbbotJsonFormat.Default.Deserialize<TResponseContent>(responseJson).Require();

            return AbbotResponse.Success<TResponse>(
                (int)response.StatusCode,
                payload);
        }

        var problem = AbbotJsonFormat.Default.Deserialize<ProblemDetails?>(responseJson)
            ?? new ProblemDetails();
        if (problem.Status is 0)
        {
            problem.Status = (int)response.StatusCode;
        }
        return AbbotResponse.Error<TResponse>(problem);
    }

    public async Task<AbbotResponse<TResponse>> SendApiAsync<TRequestContent, TResponse, TResponseContent>(
        Uri url,
        HttpMethod method,
        TRequestContent requestContent) where TResponse : class where TResponseContent : class, TResponse
    {
        using var request = PrepareRequest(url, method, SkillContext.SkillRunnerInfo.Timestamp);
        request.AddJsonContent(requestContent);
        var response = await _httpClient.SendAsync(request, _environment.CancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var responseBodyJson = await response.Content.ReadAsStringAsync();
            var payload = AbbotJsonFormat.Default.Deserialize<TResponseContent>(responseBodyJson).Require();

            return AbbotResponse.Success<TResponse>(
                (int)response.StatusCode,
                payload);
        }

        var problem = AbbotJsonFormat.Default.Deserialize<ProblemDetails>(
            await response.Content.ReadAsStringAsync()).Require();
        if (problem.Status is 0)
        {
            problem.Status = (int)response.StatusCode;
        }
        return AbbotResponse.Error<TResponse>(problem);
    }

    /// <inheritdoc />
    public async Task<AbbotResponse> SendApiAsync(Uri url, HttpMethod method)
    {
        var response = await SendAsync(url, method);
        if (response.IsSuccessStatusCode)
        {
            return AbbotResponse.Success(
                (int)response.StatusCode);
        }

        var problem = AbbotJsonFormat.Default.Deserialize<ProblemDetails>(
            await response.Content.ReadAsStringAsync()).Require();
        if (problem.Status is 0)
        {
            problem.Status = (int)response.StatusCode;
        }
        return AbbotResponse.Error(problem);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendAsync(Uri url, HttpMethod method)
    {
        using var request = PrepareRequest(url, method, SkillContext.SkillRunnerInfo.Timestamp);
        return await _httpClient.SendAsync(request, _environment.CancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResponse?> SendAsync<TResponse>(Uri url, HttpMethod method) where TResponse : class
    {
        var response = await SendAsync(url, method);
        return response.IsSuccessStatusCode
            ? await ReadAsAsync<TResponse>(response.Content)
            : null;
    }

    /// <inheritdoc />
    public async Task<TResponseContent?> SendJsonAsync<TRequestContent, TResponseContent>(
        Uri url,
        HttpMethod method,
        TRequestContent data)
        where TResponseContent : class
    {
        using var request = PrepareRequest(url, method, SkillContext.SkillRunnerInfo.Timestamp);
        request.AddJsonContent(data);
        var response = await _httpClient.SendAsync(request, _environment.CancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadAsAsync<TResponseContent>(response.Content);
    }

    public static async Task<TResponseContent> ReadAsAsync<TResponseContent>(HttpContent httpContent)
    {
        var formatter = new JsonMediaTypeFormatter
        {
            SerializerSettings = AbbotJsonFormat.NewtonsoftJson.SerializerSettings,
        };
        return await httpContent.ReadAsAsync<TResponseContent>(new[] { formatter });
    }

    Uri CompilationUri => BaseApiUrl.Append("/compilation");

    public Uri BaseApiUrl => _environment.GetSkillApiUrl(SkillContext.SkillRunnerInfo.SkillId);

    SkillContext SkillContext => _skillContextAccessor.SkillContext
                                 ?? throw new InvalidOperationException($"The {nameof(SkillContext)} needs to be set for this request.");

    HttpRequestMessage PrepareRequest(Uri uri, HttpMethod method, long timestamp)
    {
        var request = new HttpRequestMessage
        {
            Method = method,
            RequestUri = uri,
            Headers =
            {
                { HttpRequestHeader.Accept.ToString(), "application/json" },
                { HttpRequestHeader.Authorization.ToString(), $"Bearer {SkillContext.ApiKey}"},
            }
        };

        return request;
    }
}

static class Extensions
{
    public static void Deconstruct<T>(this T[] arr, out T first, out Span<T> rest) =>
        new Span<T>(arr).Deconstruct(out first, out rest);

    public static void Deconstruct<T>(this Span<T> span, out T first, out Span<T> rest)
    {
        first = span[0];
        rest = span.Length > 0
            ? span[1..]
            : span;
    }

    public static T RegisterForDispose<T>(this T obj, DisposableContainer container) where T : IDisposable
    {
        container.RegisterForDispose(obj);
        return obj;
    }
}

/// <summary>
/// Simple disposable container that can keep things alive until the container goes out of scope
/// </summary>
class DisposableContainer : IDisposable
{
    readonly List<IDisposable> _disposables = new();

    bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
            return;

        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _disposables.Clear();
        _disposed = true;
    }

    public void RegisterForDispose(IDisposable disposable) => _disposables.Add(disposable);
}
