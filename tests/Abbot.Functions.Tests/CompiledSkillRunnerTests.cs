using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Cryptography;
using Serious.TestHelpers;
using Xunit;

public class CompiledSkillRunnerTests
{
    const string CodeCacheKeyHashSeed = "5t4CTimhnndgNQ==";

    [Fact]
    public async Task ReturnsNothingWhenScriptDoesNotRespondButWasCalledWithPattern()
    {
        const string code = "void DoNothing() {}";
        var cacheKey = code.ComputeHMACSHA256FileName(CodeCacheKeyHashSeed);
        var bot = new FakeBot
        {
            IsChat = true,
            Pattern = new PatternMessage()
        };
        var runner = new CompiledSkillRunner(bot);
        var compilationCache = new FakeCompilationCache();
        var compiledSkill = TestSkillCompiler.CompileAsync(code);
        compilationCache.Add(cacheKey, compiledSkill);

        var result = await runner.RunAndGetActionResultAsync(compiledSkill);

        var response = Assert.IsType<SkillRunResponse>(result.Value);
        Assert.NotNull(response.Replies);
        Assert.Empty(response.Replies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoesNotReturnMessageWhenScriptDoesNotRespond(bool isChat)
    {
        var code = "void DoNothing() {}";
        var cacheKey = code.ComputeHMACSHA256FileName(CodeCacheKeyHashSeed);
        var bot = new FakeBot { IsChat = isChat };
        var runner = new CompiledSkillRunner(bot);
        var compilationCache = new FakeCompilationCache();
        var compiledSkill = TestSkillCompiler.CompileAsync(code);
        compilationCache.Add(cacheKey, compiledSkill);

        var result = await runner.RunAndGetActionResultAsync(compiledSkill);

        var response = Assert.IsType<SkillRunResponse>(result.Value);
        Assert.NotNull(response.Replies);
        Assert.Empty(response.Replies);
    }

    [Fact]
    public async Task ExecutesSkillAndReturnsResult()
    {
        var bot = new FakeBot
        {
            IsChat = true,
            Arguments = new Arguments("Debate"),
            From = new PlatformUser("U001", "Some Username", "Real Name", null, null, null)
        };
        const string code = @"
var from = Bot.From;
await Bot.ReplyAsync($""This message with arguments {Bot.Arguments.Value} came from {from}."");";
        var cacheKey = code.ComputeHMACSHA256FileName(CodeCacheKeyHashSeed);
        var compilationCache = new FakeCompilationCache();
        var compiledSkill = TestSkillCompiler.CompileAsync(code);
        compilationCache.Add(cacheKey, compiledSkill);
        var runner = new CompiledSkillRunner(bot);

        var result = await runner.RunAndGetActionResultAsync(compiledSkill);

        var response = Assert.IsType<SkillRunResponse>(result.Value);
        Assert.NotNull(response.Replies);
        var reply = Assert.Single(response.Replies);
        Assert.Equal(
            "This message with arguments Debate came from <@U001>.",
            reply);
    }

    [Fact]
    public async Task ExecutesSkillThatThrowsAndReturnsExceptionMessage()
    {
        var bot = new FakeBot
        {
            IsChat = true,
            Arguments = new Arguments("Debate"),
            From = new PlatformUser("U001", "Some Username", "Real Name", null, null, null)
        };
        const string code = @"DoStuff();
static void DoStuff() {
    throw new InvalidOperationException(""Oh no, shit broke."");
}";
        var cacheKey = code.ComputeHMACSHA256FileName(CodeCacheKeyHashSeed);
        var compilationCache = new FakeCompilationCache();
        var compiledSkill = TestSkillCompiler.CompileAsync(code);
        compilationCache.Add(cacheKey, compiledSkill);
        var runner = new CompiledSkillRunner(bot);

        var result = await runner.RunAndGetActionResultAsync(compiledSkill);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var response = Assert.IsType<SkillRunResponse>(result.Value);
        Assert.NotNull(response.Replies);
        var errors = response.Errors;
        Assert.NotNull(errors);
        var error = Assert.Single(errors);
        Assert.StartsWith(
            "System.InvalidOperationException: Oh no, shit broke",
            error.Description);
        Assert.Contains("at DoStuff() in :line 3", error.StackTrace!);
        Assert.Empty(response.Replies);
    }

    [Fact]
    public async Task SavesBrainWritesToApi()
    {
        var botBrain = new FakeBotBrain();
        var bot = new FakeBot(brain: botBrain)
        {
            IsChat = true,
            Arguments = new Arguments("Debate"),
            From = new PlatformUser("U001", "Some Username", "Real Name", null, null, null)
        };
        var code = "await Bot.Brain.WriteAsync(\"the key to\", \"success\");" +
            "await Bot.ReplyAsync(\"Done\");";
        var cacheKey = code.ComputeHMACSHA256FileName(CodeCacheKeyHashSeed);
        var compilationCache = new FakeCompilationCache();
        var compiledSkill = TestSkillCompiler.CompileAsync(code);
        compilationCache.Add(cacheKey, compiledSkill);
        var runner = new CompiledSkillRunner(bot);

        var result = await runner.RunAndGetActionResultAsync(compiledSkill);

        var response = Assert.IsType<SkillRunResponse>(result.Value);
        Assert.NotNull(response.Replies);
        var reply = Assert.Single(response.Replies);
        Assert.Equal("Done", reply);
        var data = await botBrain.GetAsync("THE KEY TO");
        Assert.NotNull(data);
        Assert.Equal("success", (string)data!);
    }
}
