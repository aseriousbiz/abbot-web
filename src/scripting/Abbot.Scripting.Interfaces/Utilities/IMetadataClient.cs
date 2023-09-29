using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting.Utilities;

/// <summary>
/// A client used to manage room metadata for the organization.
/// </summary>
public interface IMetadataClient
{
    /// <summary>
    /// Gets all the metadata fields in your org.
    /// </summary>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the set of customers in the org.</returns>
    Task<AbbotResponse<IReadOnlyList<MetadataFieldInfo>>> GetAllAsync();

    /// <summary>
    /// Gets the metadata field with the specified name.
    /// </summary>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the customer.</returns>
    Task<AbbotResponse<MetadataFieldInfo>> GetByNameAsync(string name);

    /// <summary>
    /// Creates a metadata field for the org.
    /// </summary>
    /// <param name="metadataField">The values for the metadata field to create.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the created customer.</returns>
    Task<AbbotResponse<MetadataFieldInfo>> CreateAsync(MetadataFieldInfo metadataField);

    /// <summary>
    /// Updates a metadata field for the org. If a field is not found, returns null.
    /// </summary>
    /// <param name="name">The name of the field to update.</param>
    /// <param name="metadataField">The updated values for the metadata field.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the updated customer.</returns>
    Task<AbbotResponse<MetadataFieldInfo>> UpdateAsync(string name, MetadataFieldInfo metadataField);
}
