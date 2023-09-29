using System.Threading.Tasks;
using Abbot.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serious.Abbot.Functions;
using Serious.TestHelpers;
using Xunit;

public class RuntimeErrorTests
{
    public class TheCreateMethod
    {
        [Fact]
        public async Task ReturnsExceptionWithCleanStackTraceAndLineNumber()
        {
            const string code = @"
DoStuff();

void DoStuff() {
DoOtherStuff();
}

void DoOtherStuff() {
throw new InvalidOperationException(""shit broke."");
}";
            var options = CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, options);
            var scriptState = await script.RunAsync(null, _ => true);

            var error = RuntimeErrorFactory.Create(scriptState.Exception, "myskill");

            Assert.NotNull(error.StackTrace);
            Assert.Equal(@"   at DoOtherStuff() in :line 9
   at DoStuff() in :line 5
   at myskill in :line 2".NormalizeLineEndings(true),
                error.StackTrace.NormalizeLineEndings(true));
            Assert.Equal(8, error.LineStart); // 0-based
            Assert.Equal(8, error.LineEnd);   // 0-based
        }

        [Fact]
        public async Task ReturnsExceptionWithCleanStackTraceWithAsync()
        {
            const string code = @"
await DoStuff();

async Task DoStuff() {
await DoOtherStuff();
}


async Task DoOtherStuff() {
throw new InvalidOperationException(""shit broke."");
}";
            var options = CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, options);
            var scriptState = await script.RunAsync(null, _ => true);

            var error = RuntimeErrorFactory.Create(scriptState.Exception, "someskill");

            Assert.NotNull(error.StackTrace);
            Assert.Equal(@"   at DoOtherStuff() in :line 10
--- End of stack trace from previous location ---
   at DoStuff() in :line 5
--- End of stack trace from previous location ---
   at someskill in :line 2".NormalizeLineEndings(true),
                error.StackTrace.NormalizeLineEndings(true));
            Assert.Equal(9, error.LineStart); // 0-based
            Assert.Equal(9, error.LineEnd);   // 0-based
        }


        [Fact]
        public async Task ReturnsExceptionWithFromObjectMethodCleanStackTrace()
        {
            const string code = @"
await DoStuff();

async Task DoStuff() {
var stuff = new Stuff();
await ReplyAsync(await stuff.GetTextAsync());
}

public class Stuff {
    public async Task<string> GetTextAsync() {
        return await DoOtherStuff();
    }

    async Task<string> DoOtherStuff() {
        throw new InvalidOperationException(""shit broke."");
    }
}

Task ReplyAsync(string text) {
    return Task.CompletedTask;
}
";
            var options = CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, options);
            var scriptState = await script.RunAsync(null, _ => true);

            var error = RuntimeErrorFactory.Create(scriptState.Exception, "someskill");

            Assert.NotNull(error.StackTrace);
            Assert.Equal(@"   at Stuff.DoOtherStuff() in :line 15
   at Stuff.GetTextAsync() in :line 11
   at DoStuff() in :line 6
--- End of stack trace from previous location ---
   at someskill in :line 2".NormalizeLineEndings(true),
                error.StackTrace.NormalizeLineEndings(true));
        }

        [Fact]
        public async Task ReturnsExceptionWithFromPropertyCleanStackTrace()
        {
            const string code = @"

await DoStuff();

async Task DoStuff() {
  var stuff = new Stuff();
  await ReplyAsync(await stuff.GetTextAsync());
}

public class Stuff {
  public string Text {
    get {
        throw new InvalidOperationException(""shit broke."");
    }
  }

  public async Task<string> GetTextAsync() {
    return await DoOtherStuff(Text);
  }

  Task<string> DoOtherStuff(string text) {
    return Task.FromResult(text);
  }
}

Task ReplyAsync(string text) {
    return Task.CompletedTask;
}
";
            var options = CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, options);
            var scriptState = await script.RunAsync(null, _ => true);

            var error = RuntimeErrorFactory.Create(scriptState.Exception, "someskill");

            Assert.NotNull(error.StackTrace);
            Assert.Equal(@"   at Stuff.get_Text() in :line 13
   at Stuff.GetTextAsync() in :line 18
   at DoStuff() in :line 7
--- End of stack trace from previous location ---
   at someskill in :line 3".NormalizeLineEndings(true),
                error.StackTrace.NormalizeLineEndings(true));
        }

        static ScriptOptions CreateScriptOptions()
        {
            var nameSpaces = AbbotScriptOptions.NameSpaces;
            var references = AbbotScriptOptions.GetSkillCompilerAssemblyReferences();

            return ScriptOptions.Default
                .WithImports(nameSpaces)
                .WithEmitDebugInformation(true)
                .WithReferences(references)
                .WithAllowUnsafe(false);
        }
    }
}
