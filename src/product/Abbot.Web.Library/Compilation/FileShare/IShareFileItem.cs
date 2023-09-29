namespace Serious.Abbot.Storage.FileShare;

public interface IShareFileItem
{
    /// <summary>
    /// Gets a value indicating whether this item is a directory.
    /// </summary>
    bool IsDirectory { get; }

    /// <summary>
    /// Gets the name of this item.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional value indicating the file size, if this item is
    /// a file.
    /// </summary>
    long? FileSize { get; }
}
