using System;
using System.Threading.Tasks;
using Serious.Abbot.Functions;

namespace Serious.Abbot.Execution;

/// <summary>
/// An executable skill assembly.
/// </summary>
public interface ICompiledSkill
{
    /// <summary>
    /// An assembly made by compiling a skill's code.
    /// </summary>
    /// <param name="skillContext">The context passed to a compiled skill.</param>
    Task<Exception?> RunAsync(IExtendedBot skillContext);

    /// <summary>
    /// The assembly name of the compiled assembly.
    /// </summary>
    string Name { get; }
}
