using Azure.Storage.Files.Shares.Models;

namespace Serious.Abbot.Storage.FileShare;

public class AzureShareFileItem : IShareFileItem
{
    public AzureShareFileItem(ShareFileItem fileItem)
        : this(fileItem.IsDirectory, fileItem.Name, fileItem.FileSize)
    {
    }

    public AzureShareFileItem(bool isDirectory, string name, long? fileSize = null)
    {
        IsDirectory = isDirectory;
        Name = name;
        FileSize = fileSize;
    }

    /// <summary>
    /// Gets a value indicating whether this item is a directory.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Gets the name of this item.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets an optional value indicating the file size, if this item is
    /// a file.
    /// </summary>
    public long? FileSize { get; }
}
