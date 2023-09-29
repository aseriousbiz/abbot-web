using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Serious.Abbot.Compilation;
using Serious.Abbot.Scripting;
using Xunit;

public class ScriptVerifierTests
{
    public class TheRunAnalyzersAsyncMethod
    {
        public class TestScriptGlobals
        {
            public List<string> List { get; } = new();
        }

        [Fact]
        public async Task TestEmitAndLoad()
        {
            const string code = "List.Add(\"test\");";
            var options = SkillCompiler.CreateScriptOptions();

            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(TestScriptGlobals), options: options);
            using var ms = new MemoryStream();
            script.GetCompilation().Emit(ms);
            ms.Position = 0;

            var context = new ScriptHelper.CollectibleAssemblyLoadContext();
            var assembly = context.LoadFromStream(ms);
            var type = assembly.GetType("Submission#0");
            var factory = type?.GetMethod("<Factory>");
            if (factory is null)
            {
                return;
            }

            var globals = new TestScriptGlobals();
            var submissionArray = new object[2];
            submissionArray[0] = globals;
            var task = (Task<object>)factory.Invoke(null, new object[] { submissionArray })!;
            await task;
            Assert.Equal("test", globals.List.First());
        }

        [Theory]
        [InlineData(@"await Bot.ReplyAsync(Environment.CommandLine);")]
        [InlineData(@"await Bot.ReplyAsync(Environment.GetEnvironmentVariable(""test""));")]
        [InlineData(@"if (Bot.IsRequest) {
    await Bot.ReplyAsync(""Http Triggered"");
}
else {
    var env = Environment.CommandLine;
    await Bot.ReplyAsync(env);
}")]
        [InlineData(@"if (Bot.IsRequest) {
    await Bot.ReplyAsync(""Http Triggered"");
}
else {
    var env = Environment.GetEnvironmentVariable(""test"");
    await Bot.ReplyAsync(env);
}")]
        public async Task ReturnsCompilationErrorsForForbiddenCode(string code)
        {
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var errors = await scriptVerifier.RunAnalyzersAsync(script.GetCompilation());

            Assert.Single(errors);
            Assert.Equal(
                "Access to System.Environment is not allowed in an Abbot skill",
                errors[0].Description);
        }

        [Fact]
        public async Task DoesNotAllowDllImportAttribute()
        {
            const string code = @"
using System.Runtime.InteropServices;
[DllImport(""user32.dll"", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);
MessageBox(IntPtr.Zero, ""Command-line message box"", ""Attention!"", 0);";
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var error = (await scriptVerifier.RunAnalyzersAsync(script.GetCompilation()))
                .First(e => e.ErrorId == ForbiddenAccessAnalyzer.AttributeDiagnosticId);

            Assert.Equal(
                "Attribute 'DllImport' is not allowed in an Abbot skill",
                error.Description);
        }

        [Fact]
        public async Task PreventAccessToReflection()
        {
            const string code = @"
Type environment = Type.GetType(""System.Environment"");
var method = environment.GetMethod(""GetEnvironmentVariable"", new[]{typeof(string)});
var result = method.Invoke(null, new[] {""USER""});
await Bot.ReplyAsync(result as string);";
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var errors = (await scriptVerifier.RunAnalyzersAsync(script.GetCompilation()))
                .ToList();

            Assert.True(errors.Count >= 3);
            Assert.Contains(errors, e => e.Description == "Access to System.Type is not allowed in an Abbot skill");
            Assert.Contains(errors, e => e.Description ==
                "Access to types in the System.Reflection namespace are not allowed in an Abbot skill");
        }

        [Theory]
        [InlineData(@"var directory = new DirectoryInfo(""foo"");")]
        [InlineData(@"Directory.CreateDirectory(""foo"");")]
        [InlineData(@"var file = new FileInfo(""foo"");")]
        [InlineData(@"File.Create(""foo"");")]
        [InlineData(@"new StreamWriter(""some-path"");")]
        public async Task PreventAccessToFileSystem(string badCode)
        {
            var code = @$"
using System.IO;
{badCode}";
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var errors = await scriptVerifier.RunAnalyzersAsync(script.GetCompilation());

            Assert.Contains(errors,
                e => e.Description == "Access to types in the System.IO namespace are not allowed in an Abbot skill");
        }

        [Theory]
        [InlineData(@"var ptr = new IntPtr(32);", "nint")]
        [InlineData(@"Type.GetType(""name"");", "System.Type")]
        public async Task PreventAccessToDisallowedTypes(string badCode, string badType)
        {
            var code = @$"
using System;
{badCode}";
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var errors = await scriptVerifier.RunAnalyzersAsync(script.GetCompilation());

            Assert.Equal(
                $"Access to {badType} is not allowed in an Abbot skill",
                errors.Single().Description);
        }

        [Theory]
        [InlineData("Marshal.AllocHGlobal(100)", "System.Runtime.InteropServices")]
        [InlineData("Unsafe.As<string>(2);", "System.Runtime.CompilerServices")]
        [InlineData("Label label; label.Equals(null);", "System.Reflection.Emit")]
        [InlineData("Debug.Indent();", "System.Diagnostics")]
        [InlineData("var attr = new EventAttribute(1);", "System.Diagnostics.Tracing")]
        [InlineData("var builder = new BlobBuilder();", "System.Reflection.Metadata")]
        [InlineData("MemoryMappedFile.CreateFromFile(\"test\");", "System.IO.MemoryMappedFiles")]
        [InlineData("ZipFile.CreateFromDirectory(\"x\", \"y\");", "System.IO.Compression")]
        public async Task PreventAccessToDisallowedNamespaces(string badCode, string badNamespace)
        {
            var code = @$"
using {badNamespace};
{badCode}";
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var errors = await scriptVerifier.RunAnalyzersAsync(script.GetCompilation());

            Assert.Contains(errors,
                e => e.Description == $"Access to types in the {badNamespace} namespace are not allowed in an Abbot skill");
        }

        [Fact]
        public async Task AllowsAccessToStringWriter()
        {
            const string code = @"
var writer = new System.IO.StringWriter();";
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var errors = await scriptVerifier.RunAnalyzersAsync(script.GetCompilation());

            Assert.Empty(errors);
        }

        [Fact]
        public async Task BlocksCallingMethodOnForbiddenDerivedType()
        {
            const string code = @"
using System.IO;

public class MyStream : Stream
{
    public override void Flush()
    {
        throw new System.NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new System.NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new System.NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new System.NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new System.NotImplementedException();
    }

    public override bool CanRead { get; }
    public override bool CanSeek { get; }
    public override bool CanWrite { get; }
    public override long Length { get; }
    public override long Position { get; set; }
}
var stream = new MyStream();
await stream.FlushAsync();
";
            var options = SkillCompiler.CreateScriptOptions();
            var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals), options: options);
            var scriptVerifier = new ScriptVerifier();

            var errors = (await scriptVerifier.RunAnalyzersAsync(script.GetCompilation()))
                .Where(e => e.ErrorId == ForbiddenAccessAnalyzer.NamespaceTypeDiagnosticId)
                .ToList();

            Assert.Equal(
                "Access to types in the System.IO namespace are not allowed in an Abbot skill",
                errors.First().Description);
        }
    }
}
