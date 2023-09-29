namespace Abbot.Web.Library.Tests;

public class ScriptGlobals
{
    public bool IsRequest { get; } = true;

    public void Reply(string reply)
    {
    }

    public object SomeType { get; } = new object();
    public Type ActualType { get; } = typeof(ScriptGlobals);
    public IntPtr IntPtrField = new IntPtr(32);
}
