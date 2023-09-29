namespace Serious.Abbot.Security;

public static class AbbotSchema
{
    public const string SchemaUri = "https://schemas.ab.bot/";

    public static string GetSchemaUri(string name) =>
        $"{SchemaUri}{name}";

    public static string GetProblemUri(string name) =>
        $"{SchemaUri}problems/{name}";
}
