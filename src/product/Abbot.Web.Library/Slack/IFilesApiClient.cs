using System.Threading.Tasks;
using Refit;

namespace Serious.Slack;

/// <summary>
/// Client for managing files within Slack.
/// </summary>
public interface IFilesApiClient
{
    /// <summary>
    /// Gets information about a file.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/files.info"/> for more info.
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="file">The Id of the file.</param>
    /// <returns></returns>
    [Get("/files.info")]
    Task<FileResponse> GetFileInfoAsync([Authorize] string accessToken, string file);

    /// <summary>
    /// Uploads or creates a file.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/files.upload" /> for more info.
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="file">The file to upload.</param>
    /// <param name="filename">The name of the file.</param>
    /// <param name="filetype">The type of file.</param>
    /// <param name="channels">Comma-separated list of channel names or IDs where the file will be shared.</param>
    /// <param name="initialComment">The message text introducing the file in specified channels.</param>
    /// <param name="threadTimestamp">Another message's ts value to upload this file as a reply. Never use a reply's ts value; use its parent instead.</param>
    [Multipart]
    [Post("/files.upload")]
    Task<FileResponse> UploadFileAsync(
        [Authorize] string accessToken,
        MultipartItem file,
        string? filename,
        string? filetype,
        string? channels,
        [AliasAs("initial_comment")] string? initialComment,
        [AliasAs("thread_ts")] string? threadTimestamp);

    /// <summary>
    /// Enables a file for public/external sharing.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/methods/files.sharedPublicURL" /> for more info.
    /// </remarks>
    /// <param name="accessToken">The slack api access token.</param>
    /// <param name="file">The id of the file to share (ex. F1234567890).</param>
    [Multipart]
    [Post("/files.sharedPublicURL")]
    Task<FileResponse> EnableFilePublicUrlAsync([Authorize] string accessToken, string file);

}
