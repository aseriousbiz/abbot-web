using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class TriggerCategoryMessage : ApiMessage<TriggerCategory>
{
    [property: JsonProperty("trigger_category")]
    [property: JsonPropertyName("trigger_category")]
    public override TriggerCategory? Body { get; set; }
}

public class TriggerCategory
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("position")]
    [JsonPropertyName("position")]
    public int? Position { get; set; }
}

public class TriggerMessage : ApiMessage<Trigger>
{
    [property: JsonProperty("trigger")]
    [property: JsonPropertyName("trigger")]
    public override Trigger? Body { get; set; }
}

public class Trigger
{
    [JsonProperty("actions")]
    [JsonPropertyName("actions")]
    public IList<Action> Actions { get; set; } = new List<Action>();

    [JsonProperty("active")]
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonProperty("category_id")]
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; set; }

    [JsonProperty("conditions")]
    [JsonPropertyName("conditions")]
    public Conditions Conditions { get; set; } = new();

    [JsonProperty("description")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public System.Uri Url { get; set; } = null!;
}

public class Action
{
    [JsonProperty("field")]
    [JsonPropertyName("field")]
    public string Field { get; set; } = null!;

    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public object Value { get; set; } = null!;
}

public class Conditions
{
    [JsonProperty("all")]
    [JsonPropertyName("all")]
    public IList<Condition> All { get; set; } = new List<Condition>();

    [JsonProperty("any")]
    [JsonPropertyName("any")]
    public IList<Condition> Any { get; set; } = new List<Condition>();
}

public class Condition
{
    [JsonProperty("field")]
    [JsonPropertyName("field")]
    public string Field { get; set; } = null!;

    [JsonProperty("operator")]
    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
