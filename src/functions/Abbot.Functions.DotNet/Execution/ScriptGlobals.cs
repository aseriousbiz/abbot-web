using Serious.Abbot.Scripting;

namespace Serious.Abbot.Execution;

public class ScriptGlobals : IScriptGlobals
{
    public ScriptGlobals(IBot context)
    {
        Bot = context;
    }

    public IBot Bot { get; }
}
