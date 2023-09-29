using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Serious.Abbot.Extensions;

/// <summary>
/// Extensions for working with image types.
/// </summary>
public static class FileExtensions
{
    static readonly IReadOnlyDictionary<byte[], string> FileHeaderBytes = new Dictionary<byte[], string>
    {
        { new byte[] { 0x42, 0x4d }, "bmp" },
        { new byte[] { 0x47, 0x49, 0x46 }, "gif" },
        { new byte[] { 0x89, 0x50, 0x4e, 0x47 }, "png" },
        { new byte[] { 0x4d, 0x4d, 0x00, 0x2a }, "tiff" },
        { new byte[] { 0x49, 0x49, 0x2a }, "tiff" },
        { new byte[] { 0xff, 0xd8 }, "jpeg" },
        { new byte[] { 0x25, 0x50, 0x44, 0x46 }, "pdf" },
    };

    static readonly IReadOnlyList<string> InitialBase64EncodedBytesForFiles =
        FileHeaderBytes.Keys.Select(ToBase64StringWithoutPadding).ToList();

    /// <summary>
    /// Returns true if the string represents a Base64 encoded file that we know about.
    /// </summary>
    /// <param name="fileReference">Either the URL to an image or the base64 encoded bytes of a file.</param>
    /// <returns><c>true</c> if the file is a base64 encoded file. Otherwise false.</returns>
    public static bool IsBase64EncodedFile(this string fileReference) =>
        !fileReference.StartsWith("http://", StringComparison.Ordinal)
        && !fileReference.StartsWith("https://", StringComparison.Ordinal)
        && InitialBase64EncodedBytesForFiles.Any(header => fileReference.StartsWith(header, StringComparison.Ordinal));

    /// <summary>
    /// Returns true if the string represents a Base64 encoded file that we know about.
    /// </summary>
    /// <param name="fileReference">Either the URL to an image or the base64 encoded bytes of a file.</param>
    /// <returns><c>true</c> if the file is a base64 encoded file. Otherwise false.</returns>
    public static bool TryGetBytesFromBase64EncodedFile(this string fileReference, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = IsBase64EncodedFile(fileReference)
            ? Convert.FromBase64String(fileReference)
            : null;

        return bytes is not null;
    }

    /// <summary>
    /// Given a set of bytes for an image, returns the image type.
    /// </summary>
    /// <param name="image">The image bytes.</param>
    /// <returns>The type of image.</returns>
    public static string ParseFileType(this byte[] image) => FileHeaderBytes
        .FirstOrDefault(entry => entry.Key.SequenceEqual(image.Take(entry.Key.Length))).Value ?? "unknown";

    static string ToBase64StringWithoutPadding(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        var paddingIndex = base64.IndexOf('=', StringComparison.Ordinal);
        return paddingIndex > -1
            ? base64[..paddingIndex]
            : base64;
    }
}
