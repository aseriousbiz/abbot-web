using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Functions.Cache;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Messages;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Messages;
using Serious.Logging;

namespace Serious.Abbot.Functions.DotNet;

public class SkillRunner
{
    readonly ICompiledSkillRunner _compiledSkillRunner;
    readonly ISkillContextAccessor _contextAccessor;
    readonly ICompilationCache _compilationCache;

    static readonly AssemblyBuildMetadata BuildMetadata = typeof(SkillRunner).Assembly.GetBuildMetadata();

    public SkillRunner(
        ICompiledSkillRunner compiledSkillRunner,
        ISkillContextAccessor contextAccessor,
        ICompilationCache compilationCache,
        ILoggerFactory loggerFactory)
    {
        _compiledSkillRunner = compiledSkillRunner;
        _contextAccessor = contextAccessor;
        _compilationCache = compilationCache;
        ApplicationLoggerFactory.Configure(loggerFactory);
    }

    // I don't know if Azure Functions works if the method is static so we'll keep it non-static for now.
#pragma warning disable CA1822
    [Function("Status")]
    public async Task<HttpResponseData> RunStatusAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var runtimePath = Path.GetDirectoryName(typeof(string).Assembly.Location).Require();

        // CS0436 is the warning that there's a conflict on the name "ThisAssembly"
        // But, if suppressed, the default behavior is to use the one defined in this assembly, which is what we want.
        var version = BuildMetadata.Version;
        var commitId = BuildMetadata.CommitId;

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync($"OK. Running {version} (Commit: {commitId}) on {RuntimeInformation.FrameworkDescription} {RuntimeInformation.RuntimeIdentifier} in {runtimePath}");
        return response;
    }
#pragma warning restore CA1822

    [Function("SkillRunner")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "GET", "POST", Route = null)] HttpRequestData request)
    {
        var traceParent = request.GetTraceParent();
        var traceState = request.GetTraceState();

        using var activity = ActivityHelper.CreateAndStart<SkillRunner>(traceParent, traceState);
        return await RunSkillAsync(request);
    }

    [Function("SkillRunnerSecured")]
    public async Task<HttpResponseData> RunLegacyAsync(
        [HttpTrigger(AuthorizationLevel.Function, "GET", "POST", Route = null)] HttpRequestData request)
    {
        var traceParent = request.GetTraceParent();
        var traceState = request.GetTraceState();

        using var activity = ActivityHelper.CreateAndStart<SkillRunner>(traceParent, traceState);
        return await RunSkillAsync(request);
    }

    async Task<HttpResponseData> RunSkillAsync(HttpRequestData request)
    {
        var logger = ApplicationLoggerFactory.CreateLogger<SkillRunner>();
        var assemblyVersion = ReflectionExtensions.GetAssemblyVersion(GetType());
        var csharpVersion = ReflectionExtensions.GetAssemblyVersion(typeof(CSharpScript));
        if (request.Method.Equals("GET", StringComparison.Ordinal))
        {
            return await request.CreatePlainTextResponseAsync(
                $"This skill only responds to POST requests. (`abbot-skills-csharp` v{assemblyVersion} running C# {csharpVersion})");
        }

        var message = await request.ReadAsSkillMessageAsync();
        var (skillInfo, runnerInfo) = message;

        logger.ReceivedSkillRunRequest(
            skillInfo.PlatformId,
            skillInfo.From.Id,
            skillInfo.Room.Id,
            skillInfo.Message?.MessageId,
            skillInfo.Message?.ThreadId,
            runnerInfo.SkillId,
            skillInfo.SkillName,
            runnerInfo.CacheKey,
            skillInfo.IsChat,
            skillInfo.IsInteraction,
            skillInfo.IsRequest,
            skillInfo.IsSignal,
            runnerInfo.Scope.ToString(),
            runnerInfo.ContextId);

        var skillApiToken = request.GetSingleHeaderValue(CommonConstants.SkillApiTokenHeaderName);
        if (string.IsNullOrEmpty(skillApiToken))
        {
            throw new InvalidOperationException("The API Token is null or empty");
        }

        var skillContext = new SkillContext(message, skillApiToken);

        // This next line has to run before we reach into any of the other injected classes.
        _contextAccessor.SkillContext = skillContext;

        var compiledSkill = await _compilationCache.GetCompiledSkillAsync(new CompiledSkillIdentifier(message));

        // This line has to run before we call the brain serializer (aka before we run the skill).
        skillContext.SetAssemblyName(compiledSkill.Name);

        // Execution path.
        CleanEnvironmentVariables();
        var result = await _compiledSkillRunner.RunAndGetActionResultAsync(compiledSkill);
        var statusCode = result.StatusCode.HasValue
            ? (HttpStatusCode)result.StatusCode
            : HttpStatusCode.OK;

        var response = request.CreateResponse();
        await response.WriteAsJsonAsync(result.Value, AbbotMediaTypes.ApplicationJsonV1.MediaType!, statusCode);
        return response;
    }

    static void CleanEnvironmentVariables()
    {
        // Yeah, we need to do something better than this, but for now, it works.
        Environment.SetEnvironmentVariable("APPSETTING_WEBSITE_CONTENTAZUREFILECONNECTIONSTRING", null);
        Environment.SetEnvironmentVariable("APPSETTING_AzureWebJobsStorage", null);
        Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME",
            $"https://ab.bot/"); // I don't think the value matters here.

        Environment.SetEnvironmentVariable("APPSETTING_MACHINEKEY_DecryptionKey", null);
        Environment.SetEnvironmentVariable("WEBSITE_AUTH_SIGNING_KEY", null);
        Environment.SetEnvironmentVariable("MACHINEKEY_DecryptionKey", null);
        Environment.SetEnvironmentVariable("GPG_KEY", null);
        Environment.SetEnvironmentVariable("WEBSITE_AUTH_ENCRYPTION_KEY", null);
        Environment.SetEnvironmentVariable("AzureWebJobsStorage", null);
        Environment.SetEnvironmentVariable("WEBSITE_CONTENTAZUREFILECONNECTIONSTRING", null);
    }
}

static partial class SkillRunnerLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message =
            "Received request to run skill {SkillName} using code {CacheKey} (" +
            "MessagePlatformId={MessagePlatformId}, " +
            "MessagePlatformUserId={MessagePlatformUserId}, " +
            "MessagePlatformRoomId={MessagePlatformRoomId}, " +
            "MessageId={MessageId}, " +
            "ThreadId={ThreadId}, " +
            "SkillId={SkillId}, " +
            "IsChat={IsChat}, " +
            "IsInteraction={IsInteraction}, " +
            "IsRequest={IsRequest}, " +
            "IsSignal={IsSignal}, " +
            "Scope={Scope}, " +
            "ContextId={ContextId}" +
            ")")]
    public static partial void ReceivedSkillRunRequest(this ILogger logger,
        string messagePlatformId,
        string messagePlatformUserId,
        string messagePlatformRoomId,
        string? messageId,
        string? threadId,
        int skillId,
        string skillName,
        string cacheKey,
        bool isChat,
        bool isInteraction,
        bool isRequest,
        bool isSignal,
        string scope,
        string? contextId);
}
