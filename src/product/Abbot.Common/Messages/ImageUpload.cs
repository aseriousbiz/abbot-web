namespace Serious.Abbot.Messages;
#pragma warning disable CA1819

/// <summary>
/// Represents an image to upload to Slack.
/// </summary>
/// <param name="ImageBytes">The bytes of the image.</param>
/// <param name="Title">The title of the image, if any.</param>
public record ImageUpload(byte[] ImageBytes, string? Title);
