using System.Threading;
using NSubstitute;

namespace Serious.TestHelpers
{
    public static class Args
    {
        public static ref CancellationToken CancellationToken => ref Arg.Any<CancellationToken>();
        public static ref string String => ref Arg.Any<string>();
        public static ref int Int32 => ref Arg.Any<int>();
        public static ref bool Boolean => ref Arg.Any<bool>();
    }
}
