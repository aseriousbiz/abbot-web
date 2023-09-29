using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;
using Serious.TestHelpers;
using Xunit;

public class SkillCompilerTests
{
    public class TheCompileMethod
    {
        [Fact]
        public async Task ReturnsCompilationErrorsForBadCode()
        {
            var compiler = new SkillCompiler(new FakeScriptVerifier());

            var compilationResult = await compiler.CompileAsync(CodeLanguage.CSharp, "a;");

            Assert.Equal(2, compilationResult.CompilationErrors.Count);
            Assert.Equal(
                "The name 'a' does not exist in the current context",
                compilationResult.CompilationErrors[0].Description);
            Assert.Equal(
                "Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement",
                compilationResult.CompilationErrors[1].Description);
        }

        [Fact]
        public async Task ReturnsCompilationErrorsForForbiddenCode()
        {
            const string code = "// some code";
            var verifier = new FakeScriptVerifier();
            verifier.AddCompilationError(CompilationError.Create("Shit happened."));
            var compiler = new SkillCompiler(verifier);

            var compilationResult = await compiler.CompileAsync(CodeLanguage.CSharp, code);

            Assert.Equal(1, compilationResult.CompilationErrors.Count);
            Assert.Equal(
                "Shit happened.",
                compilationResult.CompilationErrors[0].Description);
        }

        [Fact]
        public async Task ReturnsGenericErrorIfCompilerCrashes()
        {
            const string code = "// some code";
            var verifier = new FakeScriptVerifier { ThrowException = true };
            var compiler = new SkillCompiler(verifier);

            var compilationResult = await compiler.CompileAsync(CodeLanguage.CSharp, code);

            Assert.Equal(1, compilationResult.CompilationErrors.Count);
            Assert.Equal("The code crashed the compiler.",
                compilationResult.CompilationErrors[0].Description);
        }

        [Theory]
        [InlineData("unsafe static void FastCopy ( byte* ps, byte* pd, int count ) {}")]
        [InlineData("unsafe { int x = 10; int* ptr; ptr = &x; }")]
        public async Task BlocksUnsafeCode(string code)
        {
            var compiler = new SkillCompiler(new FakeScriptVerifier());

            var compilationResult = await compiler.CompileAsync(CodeLanguage.CSharp, code);

            Assert.Equal(
                "Unsafe code may only appear if compiling with /unsafe",
                compilationResult.CompilationErrors.Single().Description);
        }

        [Fact]
        public async Task BlocksAccessToCSharpScript()
        {
            const string code =
                @"
using Microsoft.CodeAnalysis.CSharp.Scripting;
await Bot.ReplyAsync(await CSharpScript.EvaluateAsync<string>(""System.Environment.GetEnvironmentVariable(\""USER\"")""));";
            var compiler = new SkillCompiler(new FakeScriptVerifier());

            var compilationResult = await compiler.CompileAsync(CodeLanguage.CSharp, code);

            var errors = compilationResult.CompilationErrors;
            Assert.True(errors.Count > 0);
        }

        [Fact]
        public async Task SupportsCSharpEight()
        {
            const string code =
                @"
var args = Bot.Arguments.Value;
args ??= ""Default Value"";";
            var compiler = new SkillCompiler(new FakeScriptVerifier());

            var compilationResult = await compiler.CompileAsync(CodeLanguage.CSharp, code);

            var errors = compilationResult.CompilationErrors;
            Assert.Empty(errors);
        }

        [Fact]
        public async Task SupportsCSharpNine()
        {
            const string code =
                @"
public record Person
{
    public string LastName { get; }
    public string FirstName { get; }

    public Person(string first, string last) => (FirstName, LastName) = (first, last);
}
var person = new Person(""Bugs"", ""Bunny"");
await Bot.ReplyAsync(person.FirstName);";
            var compiler = new SkillCompiler(new FakeScriptVerifier());

            var compilationResult = await compiler.CompileAsync(CodeLanguage.CSharp, code);

            var errors = compilationResult.CompilationErrors;
            Assert.Empty(errors);
        }
    }
}
