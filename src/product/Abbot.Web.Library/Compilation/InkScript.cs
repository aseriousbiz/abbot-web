using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ink;
using Ink.Runtime;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Compilation;

public class InkScript : SkillScript
{
    readonly List<ICompilationError> _errors = new();
    readonly List<ICompilationError> _warnings = new();

    /// <summary>
    /// External function definitions, to be bound at runtime.
    /// This is injected here so as not to clutter the user ink script.
    /// </summary>
    const string Header = @"EXTERNAL signal(name, arguments)";

    public Story? Story { get; private set; }

    public InkScript(string code) : base(CodeLanguage.Ink, code)
    {
    }

    static InkScript()
    {
        // Alas, the Ink Compiler is not thread-safe.
        // Compiler depends on InkParser, which has code that mutates static state: https://github.com/inkle/ink/blob/875543cec183bd24c1d415f83a85e44a4c962553/compiler/InkParser/InkParser_CharacterRanges.cs
        // It does it to lazy-load some values though, and if we eager-load those values once, we can avoid the issue

        // > The runtime calls a static constructor no more than once in a single application domain.
        // > That call is made in a locked region based on the specific type of the class.
        // > No additional locking mechanisms are needed in the body of a static constructor.
        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-constructors

        foreach (var range in InkParser.ListAllCharacterRanges())
        {
            range.ToCharacterSet();
        }
    }

    public override ImmutableArray<ICompilationError> Compile()
    {
        var options = new Compiler.Options
        {
            errorHandler = ErrorHandler
        };
        _errors.Clear();
        _warnings.Clear();
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        sb.AppendLine(Code);

        var compiler = new Compiler(sb.ToString(), options);
        Story = compiler.Compile();

        return _errors.ToImmutableArray();
    }

    public override async Task GetCompiledStreamAsync(Stream assemblyStream, Stream? symbolsStream)
    {
        if (Story is not null)
        {
            var sb = new StringBuilder(Story.ToJson());
            await using var writer = new StreamWriter(assemblyStream, leaveOpen: true);
            await writer.WriteAsync(sb.ToString());
            await writer.FlushAsync();
        }
    }

    public override Task<ImmutableArray<ICompilationError>> AnalyzeAsync(IScriptVerifier verifier) =>
        Task.FromResult(_errors.ToImmutableArray());

    void ErrorHandler(string message, ErrorType type, int line, int posInline)
    {
        if (type == ErrorType.Error)
            _errors.Add(CompilationError.Create(type, message, line, posInline));
        else
            _warnings.Add(CompilationError.Create(type, message, line, posInline));
    }
}
