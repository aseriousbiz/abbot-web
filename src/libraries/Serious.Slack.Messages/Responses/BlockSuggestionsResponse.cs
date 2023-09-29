using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;

namespace Serious.Slack;

/// <summary>
/// Base type for a Block Suggestions Response. This is used when implementing an external data source for a
/// <see cref="MultiExternalSelectMenu"/>.
/// </summary>
public abstract record BlockSuggestionsResponse;

/// <summary>
/// A Block Suggestions Response containing an array of option groups.
/// </summary>
/// <param name="OptionGroups"></param>
public record OptionGroupBlockSuggestionsResponse(
    [property: JsonPropertyName("options")]
    [property: JsonProperty("options")]
    IEnumerable<OptionGroup> OptionGroups) : BlockSuggestionsResponse;

/// <summary>
/// A Block Suggestions Response containing an array of options.
/// </summary>
/// <param name="Options"></param>
public record OptionsBlockSuggestionsResponse(
    [property: JsonPropertyName("options")]
    [property: JsonProperty("options")]
    IEnumerable<Option> Options) : BlockSuggestionsResponse;
