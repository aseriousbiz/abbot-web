using System;
using System.Reflection;
using System.Threading.Tasks;
using Serious.Abbot.Functions;

namespace Serious.Abbot.Execution;

public class CSharpAssembly : ICompiledSkill
{
    const string ScriptTypeName = "Submission#0";
    const string FactoryMethodName = "<Factory>";

    readonly MethodInfo _factoryMethod;

    public CSharpAssembly(Assembly assembly)
    {
        Name = assembly.FullName!;
        _factoryMethod = assembly
                             .GetType(ScriptTypeName)
                             ?.GetMethod(FactoryMethodName)
                         ?? throw new InvalidOperationException("Could not locate the skill factory method.");
    }

    /// <summary>
    /// The assembly name of the compiled assembly.
    /// </summary>
    public string Name { get; }

    public async Task<Exception?> RunAsync(IExtendedBot skillContext)
    {
        var globals = new ScriptGlobals(skillContext);

        var submissionArray = new object[2];
        submissionArray[0] = globals;
        var task = _factoryMethod.Invoke(
                       null,
                       new object[] { submissionArray }) as Task<object>
                   ?? throw new InvalidOperationException("The factory method did not return a Task<object>");
        try
        {
            await task;
        }
        catch (Exception e)
        {
            return e;
        }

        return null;
    }
}
