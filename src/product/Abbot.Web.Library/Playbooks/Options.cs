using System.Collections.Generic;

namespace Serious.Abbot.Playbooks;

public record Option(string Label, string Value);

public record OptionsDefinition(string? Preset, string Name, IReadOnlyList<Option> Options);
