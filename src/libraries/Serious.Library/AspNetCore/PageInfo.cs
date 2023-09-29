namespace Serious.Abbot;

/// <summary>
/// Encapsulates information about a page we can use to send to analytics to describe the page.
/// </summary>
/// <param name="Category">The page category.</param>
/// <param name="Name">The name of the page.</param>
/// <param name="Title">If specified, the title to use in the title bar.</param>
public record PageInfo(string Category, string Name, string Title)
{
    /// <summary>
    /// Creates a <see cref="PageInfo"/> with just the category and name. The <paramref name="name"/> is
    /// used as the title.
    /// </summary>
    /// <param name="category">The page category.</param>
    /// <param name="name">The name of the page.</param>
    public PageInfo(string category, string name) : this(category, name, name) { }
}
