using System;
using System.IO;
using System.Threading.Tasks;

namespace Serious.Abbot.Clients;

/// <summary>
/// Client used to store customer files such as profile images and uploaded images.
/// </summary>
public interface IAbbotWebFileStorage
{
    /// <summary>
    /// Uploads an organization avatar and returns the URL to the image.
    /// </summary>
    /// <param name="platformId">The platform id for the organization.</param>
    /// <param name="imageStream">The stream containing the image.</param>
    /// <returns>A Task with the <see cref="Uri"/> of the uploaded image.</returns>
    Task<Uri> UploadOrganizationAvatarAsync(string platformId, Stream imageStream);
}
