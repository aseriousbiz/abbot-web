using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about a file uploaded to Slack. This can be retrieved by calling the <c>files.upload</c> Slack API.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/methods/files.upload" /> for more info.
/// </remarks>
public record UploadedFile : EntityBase
{
    /// <summary>
    /// Unix timestamp representing when the file was created
    /// </summary>
    [JsonProperty("created")]
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>
    /// The file name. Can be null for unnamed files.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The file title.
    /// </summary>
    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    [JsonProperty("size")]
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// The file's mime type.
    /// </summary>
    [JsonProperty("mimetype")]
    [JsonPropertyName("mimetype")]
    public string MimeType { get; init; } = null!;

    /// <summary>
    /// The file type.
    /// </summary>
    [JsonProperty("filetype")]
    [JsonPropertyName("filetype")]
    public string FileType { get; init; } = null!;

    /// <summary>
    /// The human friendly name for the file type.
    /// </summary>
    [JsonProperty("pretty_type")]
    [JsonPropertyName("pretty_type")]
    public string PrettyType { get; init; } = null!;

    /// <summary>
    /// The user that uploaded or created the file.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public string User { get; init; } = null!;

    /// <summary>
    /// The user that uploaded or created the file.
    /// </summary>
    [JsonProperty("user_team")]
    [JsonPropertyName("user_team")]
    public string UserTeam { get; init; } = null!;

    /// <summary>
    /// Contains one of hosted, external, snippet or post.
    /// </summary>
    [JsonProperty("mode")]
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = null!;

    /// <summary>
    /// Whether or not the file is stored in editable mode
    /// </summary>
    [JsonProperty("editable")]
    [JsonPropertyName("editable")]
    public bool Editable { get; init; }

    /// <summary>
    /// Whether the master copy of a file is stored within the system or not.
    /// </summary>
    [JsonProperty("is_external")]
    [JsonPropertyName("is_external")]
    public bool IsExternal { get; init; }

    /// <summary>
    /// Whether the file is public or not.
    /// </summary>
    [JsonProperty("is_public")]
    [JsonPropertyName("is_public")]
    public bool IsPublic { get; init; }

    /// <summary>
    /// If <see cref="IsPublic"/> is <c>true</c> and <see cref="PublicUrl"/> has been shared, this is <c>true</c>
    /// otherwise <c>false</c>.
    /// </summary>
    [JsonProperty("public_url_shared")]
    [JsonPropertyName("public_url_shared")]
    public bool PublicUrlShared { get; init; }

    /// <summary>
    /// Url to a single page for the file containing details, comments and a download link
    /// </summary>
    [JsonProperty("public_url")]
    [JsonPropertyName("public_url")]
    public string? PublicUrl { get; init; }

    /// <summary>
    /// Url to the file contents.
    /// </summary>
    [JsonProperty("url_private")]
    [JsonPropertyName("url_private")]
    public string? UrlPrivate { get; init; }

    /// <summary>
    /// If <see cref="Editable"/>, this is a URl to the file contents
    /// which includes headers to force a browser download.[Authorize]string accessToken
    /// </summary>
    [JsonProperty("url_private_download")]
    [JsonPropertyName("url_private_download")]
    public string? UrlPrivateDownload { get; init; }

    /// <summary>
    /// Url to a single page for the file containing details, comments and a download link
    /// </summary>
    [JsonProperty("permalink")]
    [JsonPropertyName("permalink")]
    public string? Permalink { get; init; }

    /// <summary>
    /// If <see cref="IsPublic"/> is <c>true</c>, this is a URL to the public file itself.
    /// </summary>
    [JsonProperty("permalink_public")]
    [JsonPropertyName("permalink_public")]
    public string? PermalinkPublic { get; init; }

    /// <summary>
    /// Contains (when present) the URL of a 64x64 thumb.
    /// </summary>
    [JsonProperty("thumb_64")]
    [JsonPropertyName("thumb_64")]
    public string? Thumb64 { get; init; }

    /// <summary>
    /// Contains (when present) the URL of an 80x80 thumb.
    /// Unlike <see cref="Thumb64"/>, this size is guaranteed to be 80x80,
    /// even when the source image was smaller (it's padded with transparent pixels).
    /// </summary>
    [JsonProperty("thumb_80")]
    [JsonPropertyName("thumb_80")]
    public string? Thumb80 { get; init; }

    /// <summary>
    /// Undocumented
    /// </summary>
    [JsonProperty("thumb_160")]
    [JsonPropertyName("thumb_160")]
    public string? Thumb160 { get; init; }

    /// <summary>
    /// A variable sized thumb, with its longest size no bigger than 360
    /// (although it might be smaller depending on the source size).
    /// </summary>
    [JsonProperty("thumb_360")]
    [JsonPropertyName("thumb_360")]
    public string? Thumb360 { get; init; }

    /// <summary>
    /// Width of <see cref="Thumb360"/>.
    /// </summary>
    [JsonProperty("thumb_360_w")]
    [JsonPropertyName("thumb_360_w")]
    public int? Thumb360Width { get; init; }

    /// <summary>
    /// Height of <see cref="Thumb360"/>.
    /// </summary>
    [JsonProperty("thumb_360_h")]
    [JsonPropertyName("thumb_360_h")]
    public int? Thumb360Height { get; init; }

    /// <summary>
    /// An animated thumbnail, in the case where the original image was an animated gif with dimensions greater than 360 pixels.
    /// </summary>
    [JsonProperty("thumb_360_gif")]
    [JsonPropertyName("thumb_360_gif")]
    public string? Thumb360Gif { get; init; }

    /// <summary>
    /// Undocumented
    /// </summary>
    [JsonProperty("thumb_480")]
    [JsonPropertyName("thumb_480")]
    public string? Thumb480 { get; init; }

    /// <summary>
    /// Undocumented
    /// </summary>
    [JsonProperty("original_w")]
    [JsonPropertyName("original_w")]
    public int? OriginalWidth { get; init; }

    /// <summary>
    /// Undocumented
    /// </summary>
    [JsonProperty("original_h")]
    [JsonPropertyName("original_h")]
    public int? OriginalHeight { get; init; }

    /// <summary>
    /// Undocumented
    /// </summary>
    [JsonProperty("thumb_tiny")]
    [JsonPropertyName("thumb_tiny")]
    public string? ThumbTiny { get; init; }
}
