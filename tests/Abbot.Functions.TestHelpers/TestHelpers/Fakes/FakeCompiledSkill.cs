using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions;

namespace Serious.TestHelpers
{
    public class FakeSkillAssembly : ICompiledSkill
    {
        readonly IReadOnlyList<string> _messages;

        public FakeSkillAssembly() : this(Array.Empty<string>())
        {
        }

        public FakeSkillAssembly(IReadOnlyList<string> messages)
        {
            _messages = messages;
        }

        public async Task<Exception?> RunAsync(IExtendedBot skillContext)
        {
            foreach (var message in _messages)
            {
                await skillContext.ReplyAsync(message);
            }

            return null;
        }

        public string Name => "whatever";
    }
}
