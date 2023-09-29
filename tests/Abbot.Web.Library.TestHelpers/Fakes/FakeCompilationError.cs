using Serious.Abbot.Compilation;

namespace Serious.TestHelpers
{
    public class FakeCompilationError : ICompilationError
    {
        public string ErrorId { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public int LineStart { get; set; }
        public int LineEnd { get; set; }
        public int SpanStart { get; set; }
        public int SpanEnd { get; set; }
    }
}
