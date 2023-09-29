using System.Reflection;
using System.Threading.Tasks;
using Serious;
using Serious.Abbot.Playbooks;

static class EmbeddedResourceHelper
{
    /// <param name="filename">File name without <c>.json</c> extension.</param>
    /// <returns></returns>
    public static async Task<PlaybookDefinition> ReadPlaybookDefinitionResource(string filename)
        => PlaybookFormat.Deserialize(
            await Assembly.GetExecutingAssembly().ReadResourceAsync("Playbooks/TestDefinitions", $"{filename}.json"));

    public static async Task<string> ReadSlackChannelDataResource(string filename)
        => (await Assembly.GetExecutingAssembly().ReadResourceAsync("SlackChannelData", filename)).Trim();

    public static async Task<string> ReadSerializationResource(string filename) =>
        (await Assembly.GetExecutingAssembly().ReadResourceAsync("Serialization", filename)).Trim();
}
