using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Abbot.Web.Library.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Scripting;
using Serious.Abbot.Compilation;

public class ForbiddenAccessAnalyzerTests
{
    static readonly bool EmitDebugInformation = true;

    [Theory]
    [InlineData(@"if (IsRequest) {
    Reply(""test"");
}
else {
    var env = Environment.CommandLine;
    Reply(env);
}", "System.Environment")]
    [InlineData(@"if (IsRequest) {
    Reply(""test"");
}
else {
    var env = Environment.GetEnvironmentVariable(""test"");
    Reply(env);
}", "System.Environment")]
    [InlineData(@"var args = Environment.CommandLine;", "System.Environment")]
    [InlineData(@"Console.WriteLine(""test"");", "System.Console")]
    [InlineData(@"var ptr = new IntPtr(32);", "nint")]
    [InlineData(@"var type = (Type)SomeType;", "System.Type")]
    [InlineData(@"var type = (IntPtr)SomeType;", "nint")]
    [InlineData(@"var type = SomeType as Type;", "System.Type")]
    [InlineData(@"var type = Type.GetType(""name"");", "System.Type")]
    [InlineData(@"var pointers = new IntPtr[4];", "nint")]
    [InlineData(@"var pointers = new[] { new IntPtr(32) };", "nint")]
    [InlineData(@"var x = ActualType.Name;", "System.Type")]
    [InlineData(@"var type = typeof(string);", "System.Type")]
    [InlineData(@"var field = IntPtrField.ToString();", "nint")]
    public async Task ReturnsErrorsForForbiddenTypes(string code, string expectedForbiddenType)
    {
        var options = ScriptOptions.Default
            .WithImports("System")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences(GetReferences())
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        var diagnostic = diagnosticResults.First(d => d.Id == ForbiddenAccessAnalyzer.TypeDiagnosticId);
        Assert.Equal(
            $"Access to {expectedForbiddenType} is not allowed in an Abbot skill",
            diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("public static void DoStuff() { int x = 32; Test(__makeref(x));}public static void Test(TypedReference i) {}")]
    [InlineData("int x = 32; Test(__reftype(__makeref(x))); public static void Test(Type t) {}")]
    [InlineData("int x = 32; Test(__refvalue(__makeref(x), int)); public static void Test(int t) {}")]
    public async Task ReturnsErrorsForTheDarkArtsOfCSharp(string code)
    {
        var options = ScriptOptions.Default
            .WithImports("System")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences(GetReferences())
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        var diagnostic = diagnosticResults.First(d => d.Id == ForbiddenAccessAnalyzer.DarkArtsId);
        Assert.Equal(
            $"The dark arts of C# are not allowed in an Abbot skill",
            diagnostic.GetMessage());
    }

    [Theory]
    [InlineData(@"var type = (Type)SomeType; var name = type.Name;", "System.Type")]
    [InlineData(@"var type = SomeType as Type; var name = type.Name;", "System.Type")]
    public async Task ReturnsMultipleErrorsForCastAndAccess(string code, string expectedForbiddenType)
    {
        var options = ScriptOptions.Default
            .WithImports("System")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences(GetReferences())
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        Assert.True(diagnosticResults.Length > 1);
        var diagnostic = diagnosticResults.First();
        Assert.NotNull(diagnostic);
        Assert.Equal(
            $"Access to {expectedForbiddenType} is not allowed in an Abbot skill",
            diagnostic.GetMessage());
        Assert.Equal(ForbiddenAccessAnalyzer.TypeDiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("var myProcess = new Process();", "System.Diagnostics")]
    [InlineData(@"Process.Start(""foo.exe"");", "System.Diagnostics")]
    [InlineData(@"File.Exists(""foo.exe"");", "System.IO")]
    [InlineData(@"var reader = new StreamReader(""foo.exe"");", "System.IO")]
    [InlineData(@"var write = new StreamWriter(""some-path"");", "System.IO")]
    [InlineData(@"new StreamWriter(""some-path"");", "System.IO")]
    [InlineData(@"new StreamWriter(""some-path"").Flush();", "System.IO")]
    [InlineData(@"
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
", "System.IO")]
    public async Task ReturnsErrorsForForbiddenNamespaces(string code, string expectedForbiddenNamespace)
    {
        var options = ScriptOptions.Default
            .WithImports("System", "System.Diagnostics", "System.IO")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences("System", "System.Runtime.Extensions")
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        var diagnostic = diagnosticResults.First(d => d.Id == ForbiddenAccessAnalyzer.NamespaceTypeDiagnosticId);
        Assert.Equal(
            $"Access to types in the {expectedForbiddenNamespace} namespace are not allowed in an Abbot skill",
            diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("using static System.IO.File;", "System.IO")]
    [InlineData("using File = System.IO.File;", "System.IO")]
    [InlineData("using System.Diagnostics;", "System.Diagnostics")]
    [InlineData("using System.IO;", "System.IO")]
    [InlineData("using System.IO.Compression;", "System.IO.Compression")]
    [InlineData("using System.IO.MemoryMappedFiles;", "System.IO.MemoryMappedFiles")]
    [InlineData("using System.Reflection;", "System.Reflection")]
    [InlineData("using System.Reflection.Emit;", "System.Reflection.Emit")]
    [InlineData("using System.Reflection.Metadata;", "System.Reflection.Metadata")]
    [InlineData("using System.Runtime.CompilerServices;", "System.Runtime.CompilerServices")]
    [InlineData("using System.Runtime.InteropServices;", "System.Runtime.InteropServices")]
    [InlineData("using System.Security;", "System.Security")]
    [InlineData("using System.Security.AccessControl;", "System.Security.AccessControl")]
    [InlineData("using Microsoft.CodeAnalysis.CSharp;", "Microsoft.CodeAnalysis.CSharp")]
    [InlineData("using Microsoft.CodeAnalysis.CSharp.Scripting;", "Microsoft.CodeAnalysis.CSharp.Scripting")]
    [InlineData("using Microsoft.Win32.Registry;", "Microsoft.Win32.Registry")]
    public async Task ReturnsErrorsForForbiddenNamespacesInUsingDirectives(string code, string expectedForbiddenNamespace)
    {
        var options = ScriptOptions.Default
            .WithImports("System")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences("System", "System.Runtime.Extensions")
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = (await compilationWithAnalyzers.GetAllDiagnosticsAsync())
            .Where(e => e.Id == ForbiddenAccessAnalyzer.NamespaceDiagnosticId);

        var diagnostic = Assert.Single(diagnosticResults);
        Assert.NotNull(diagnostic);
        Assert.Equal(
            $"Importing the namespace {expectedForbiddenNamespace} is not allowed in an Abbot skill",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task DoesNotReturnsErrorsForTypesInForbiddenNamespacesThatAreAllowed()
    {
        const string code = @"var writer = new StringWriter();";
        var options = ScriptOptions.Default
            .WithImports("System.IO")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences("System.Runtime.Extensions")
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        Assert.Empty(diagnosticResults);
    }

    [Fact]
    public async Task DoesNotReturnsErrorsForAllowedTypes()
    {
        const string code = @"if (IsRequest) {
    Reply(""test"");
}
else {
    var rnd = new Random();
    Reply(rnd.Next(1).ToString());
}";
        var options = ScriptOptions.Default
            .WithImports("System")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences(GetReferences())
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        Assert.Empty(diagnosticResults);
    }

    [Theory]
    [InlineData("""
[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);
MessageBox(IntPtr.Zero, "Command-line message box", "Attention!", 0);"
""", "DllImport", false)]
    [InlineData("""
[SuppressUnmanagedCodeSecurity]
public static void Foo() {}
""", "SuppressUnmanagedCodeSecurity", false)]
    [InlineData("""
[Newtonsoft.Json.JsonProperty("Foo")]
public string Foo { get; set; }
""", "Newtonsoft.Json.JsonProperty", true)]
    public async Task DoesNotAllowAttributesInCode(string code, string attributeName, bool allowed)
    {
        var options = ScriptOptions.Default
            .WithImports("System", "System.Runtime.InteropServices", "System.Security")
            .WithEmitDebugInformation(EmitDebugInformation)
            .WithReferences("System.Runtime.Extensions", "System.Console", "Newtonsoft.Json")
            .WithAllowUnsafe(false);
        var script = CSharpScript.Create<dynamic>(code, globalsType: typeof(ScriptGlobals), options: options);
        var compilation = script.GetCompilation();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ForbiddenAccessAnalyzer());
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            CancellationToken.None);

        var diagnosticResults = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

        if (allowed)
        {
            Assert.Empty(diagnosticResults);
        }
        else
        {
            var diagnostic = diagnosticResults.Single(d => d.Id == ForbiddenAccessAnalyzer.AttributeDiagnosticId);
            Assert.NotNull(diagnostic);
            Assert.Equal($"Attribute '{attributeName}' is not allowed in an Abbot skill", diagnostic.GetMessage());
        }
    }

    static IEnumerable<MetadataReference> GetReferences()
    {
        return GetSystemAssemblyPaths()
            .Select(path => MetadataReference.CreateFromFile(path));
    }

    static IEnumerable<string> GetSystemAssemblyPaths()
    {
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)
                           ?? throw new InvalidOperationException("Could not find the assembly for object");
        yield return Path.Combine(assemblyPath, "mscorlib.dll");
        yield return Path.Combine(assemblyPath, "System.dll");
        yield return Path.Combine(assemblyPath, "System.Core.dll");
        yield return Path.Combine(assemblyPath, "System.Runtime.dll");
        yield return Path.Combine(assemblyPath, "System.Runtime.Extensions.dll");
        yield return typeof(Console).Assembly.Location;
    }

}
