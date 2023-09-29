using System.Linq;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Serious.Slack;

namespace Serious.Abbot.Integrations;

public class ConversationMessageToHtmlFormatter
{
    readonly BlockKitToHtmlFormatter _blockKitToHtmlFormatter;
    readonly IFilesApiClient _filesApiClient;

    public ConversationMessageToHtmlFormatter(
        BlockKitToHtmlFormatter blockKitToHtmlFormatter,
        IFilesApiClient filesApiClient)
    {
        _blockKitToHtmlFormatter = blockKitToHtmlFormatter;
        _filesApiClient = filesApiClient;
    }

    public async Task<string> FormatMessageAsHtmlAsync(ConversationMessage message, Organization organization)
    {
        var userUrl = message.From.FormatPlatformUrl();
        var messageUrl = message.GetMessageUrl();

        var text = message.Blocks.Count > 0
            ? await _blockKitToHtmlFormatter.FormatBlocksAsHtmlAsync(message.Blocks, message.Organization)
            : message.Text;
        var body = $@"
<strong><a href=""{messageUrl}"">New reply</a> from <a href=""{userUrl}"">{message.From.DisplayName}</a> in Slack:</strong><br />
{text}";

        if (message.Files is { Count: > 0 } files)
        {
            var apiToken = organization.RequireAndRevealApiToken();
            var uploadedFiles = await Task.WhenAll(files.Select(f => GetUploadedFile(apiToken, f.Id)));
            var filesHtml = uploadedFiles
                .WhereNotNull()
                .Select(FormatFileAttachment)
                .ToList();
            if (filesHtml.Any())
            {
                body += $"\n<hr />\n<strong>Message Attachments:</strong>\n<ol>\n{string.Join("\n", filesHtml)}\n</ol>\n";
            }
        }

        return body;
    }

    async Task<UploadedFile?> GetUploadedFile(string apiToken, string file)
    {
        return (await _filesApiClient.GetFileInfoAsync(apiToken, file)).Body;
    }

    static string FormatFileAttachment(UploadedFile uploadedFile)
    {
        return $@"    <li><a href=""{uploadedFile.Permalink}"">{uploadedFile.Name}</a></li>";
    }
}
