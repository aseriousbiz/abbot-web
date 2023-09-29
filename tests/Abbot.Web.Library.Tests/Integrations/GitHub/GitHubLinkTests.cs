using Serious.Abbot.Integrations.GitHub;

public class GitHubLinkTests
{
    [Fact]
    public void SerializesIssueLink()
    {
        var link = new GitHubIssueLink("abbot", "test", 123);

        var json = link.ToJson();

        Assert.Equal(
            """
            {"Owner":"abbot","Repo":"test","Number":123,"ApiUrl":"https://api.github.com/repos/abbot/test/issues/123","WebUrl":"https://github.com/abbot/test/issues/123","IntegrationType":"GitHub"}
            """,
            json);
    }
}
