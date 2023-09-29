using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Serious.Abbot.AI.Commands;

/// <summary>
/// Marks a type as a command payload that can be registered with the <see cref="CommandRegistry"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// The symbolic name of the command.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A description, for use by the Language Model, describing how and why to use this command.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// An example of the command, in JSON format.
    /// Must be a single JSON object that does not include the `command` property.
    /// </summary>
    [StringSyntax(StringSyntaxAttribute.Json)]
    public string? Exemplar { get; set; }

    /// <summary>
    /// Marks the type as a command payload that can be registered with the <see cref="CommandRegistry"/>.
    /// </summary>
    /// <param name="name">The symbolic name of the command.</param>
    /// <param name="description">A description, for use by the Language Model, describing how and why to use this command.</param>
    public CommandAttribute(
        string name,
        string description)
    {
        // TODO: If this gets more complicated, we have another option
        // Instead of having all the values be in the attribute, we can have the attribute refer to a "Descriptor" type.
        // Kinda like Mass Transit consumer definitions.

        Name = name;
        Description = description;
    }
}

/// <summary>
/// Describes a command.
/// </summary>
/// <param name="Name">The symbolic name of the command.</param>
/// <param name="Description">A description, for use by the Language Model, describing how and why to use this command.</param>
/// <param name="Exemplar">An example of the command, in JSON format.</param>
/// <param name="Type">The .NET type of the command.</param>
public readonly record struct CommandDescriptor(
    string Name,
    string Description,
    JObject Exemplar,
    Type Type)
{
    public static bool TryCreate(Type type, out CommandDescriptor descriptor)
    {
        var attribute = type.GetCustomAttribute<CommandAttribute>();
        if (attribute is null)
        {
            descriptor = default;
            return false;
        }

        var exemplar = ReadExemplar(attribute);
        descriptor = new(attribute.Name, attribute.Description, exemplar, type);
        return true;
    }

    static JObject ReadExemplar(CommandAttribute attribute)
    {
        JObject exemplar;
        if (attribute.Exemplar is { Length: > 0 })
        {
            var exemplarToken = JToken.Parse(attribute.Exemplar);
            Expect.True(exemplarToken.Type == JTokenType.Object, "Exemplar must be a JSON object");
            exemplar = (JObject)exemplarToken;
            if (exemplar.Property("command", StringComparison.Ordinal) is null)
            {
                exemplar.AddFirst(new JProperty("command", attribute.Name));
            }
        }
        else
        {
            exemplar = new JObject(new JProperty("command", attribute.Name));
        }

        return exemplar;
    }
}

/// <summary>
/// Provides a registry of <see cref="CommandDescriptor"/>s in the application.
/// </summary>
public class CommandRegistry
{
    // Map of the command type value to the .NET type to deserialize to.
    readonly ConcurrentDictionary<string, CommandDescriptor> _commandMap = new();

    /// <summary>
    /// Registers a command in the registry.
    /// </summary>
    /// <param name="command">The command to register.</param>
    public void RegisterCommand(CommandDescriptor command)
    {
        _commandMap.AddOrUpdate(
            command.Name,
            _ => command,
            (_, existing) => throw new InvalidOperationException($"Command '{command.Name}' already registered by '{existing.Type.FullName}'"));
    }

    /// <summary>
    /// Registers all the <see cref="CommandDescriptor"/>s found by scanning the provided types.
    /// </summary>
    /// <param name="types">The types to scan.</param>
    public void RegisterCommands(IEnumerable<Type> types)
    {
        bool TypeFilter(Type t) =>
            t != typeof(UnknownCommand) && !t.IsAbstract && t.IsAssignableTo(typeof(Command));

        // Find the commands among these types.
        foreach (var commandType in types.Where(TypeFilter))
        {
            if (CommandDescriptor.TryCreate(commandType, out var descriptor))
            {
                RegisterCommand(descriptor);
            }
            else
            {
                throw new UnreachableException(
                    $"Command type {commandType.FullName} is missing the [Command] attribute");
            }
        }
    }

    /// <summary>
    /// Attempts to find a command with the provided name.
    /// </summary>
    /// <param name="name">The name of the command to find.</param>
    /// <param name="command">The <see cref="CommandDescriptor"/> of the matched command. Only valid if <c>true</c> is returned.</param>
    /// <returns>A boolean indicating if a command matching <paramref name="name"/> could be found.</returns>
    public bool TryResolveCommand(string name, out CommandDescriptor command)
    {
        if (_commandMap.TryGetValue(name, out var c))
        {
            command = c;
            return true;
        }

        command = default;
        return false;
    }

    /// <summary>
    /// Gets a sorted list of all commands in the registry.
    /// </summary>
    public IEnumerable<CommandDescriptor> GetAllCommands()
    {
        return _commandMap.Values.OrderBy(c => c.Name).ToList();
    }
}
