using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Serious.Abbot.Compilation;

namespace Serious.TestHelpers
{
    public class FakeScriptVerifier : IScriptVerifier
    {
        readonly List<ICompilationError> _compilationErrors = new List<ICompilationError>();

        public void AddCompilationError(ICompilationError compilationError)
        {
            _compilationErrors.Add(compilationError);
        }

        public Task<ImmutableArray<ICompilationError>> RunAnalyzersAsync(Compilation compilation)
        {
            if (ThrowException)
            {
                return Task.FromException<ImmutableArray<ICompilationError>>(new InvalidOperationException("Shit went down."));
            }

            return Task.FromResult(_compilationErrors.ToImmutableArray());
        }

        public bool ThrowException { get; set; }
    }
}
