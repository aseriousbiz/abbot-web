using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Serious.AspNetCore;

public static class UploadHelpers
{
    // CREDIT: https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/mvc/models/file-uploads/samples/3.x/SampleApp/Utilities/FileHelpers.cs
    // LICENSE: https://github.com/dotnet/AspNetCore.Docs/blob/main/LICENSE
    // For more file signatures, see the File Signatures Database (https://www.filesignatures.net/)
    // and the official specifications for the file types you wish to add.
    static readonly Dictionary<string, List<byte[]>> FileSignature = new()
    {
        { ".GIF", new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } } },
        { ".PNG", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        {
            ".JPEG",
            new List<byte[]>
            {
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 },
            }
        },
        {
            ".JPG",
            new List<byte[]>
            {
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 },
                new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 },
            }
        },
        {
            ".ZIP",
            new List<byte[]>
            {
                new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                new byte[] { 0x50, 0x4B, 0x4C, 0x49, 0x54, 0x45 },
                new byte[] { 0x50, 0x4B, 0x53, 0x70, 0x58 },
                new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                new byte[] { 0x50, 0x4B, 0x07, 0x08 },
                new byte[] { 0x57, 0x69, 0x6E, 0x5A, 0x69, 0x70 },
            }
        },
    };

    /// <summary>
    /// Returns true if the file's extension and signature matches one of the allowed file extensions.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="data">The stream containing the data of the file.</param>
    /// <param name="permittedExtensions">The set of allowed file extensions such as .png.</param>
    /// <returns></returns>
    public static bool IsValidFileExtensionAndSignature(
        string fileName,
        Stream data,
        IEnumerable<string> permittedExtensions)
    {
        if (data.Length == 0)
        {
            return false;
        }

        var ext = Path.GetExtension(fileName).ToUpperInvariant();

        if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        data.Position = 0;

        using var reader = new BinaryReader(data, Encoding.UTF8, leaveOpen: true);

        // File signature check
        // --------------------
        // With the file signatures provided in the _fileSignature
        // dictionary, the following code tests the input content's
        // file signature.
        var signatures = FileSignature[ext];
        var headerBytes = reader.ReadBytes(signatures.Max(m => m.Length));

        var result = signatures.Any(signature =>
            headerBytes.Take(signature.Length).SequenceEqual(signature));
        data.Position = 0;
        return result;
    }
}
