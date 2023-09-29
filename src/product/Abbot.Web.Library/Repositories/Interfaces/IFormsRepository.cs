using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

public interface IFormsRepository
{
    /// <summary>
    /// Gets the form with the specified key, if present.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> that owns the form.</param>
    /// <param name="key">The key of the form to retrieve.</param>
    /// <returns>The <see cref="Form"/>, if present, or <c>null</c> if no such form exists in the organization.</returns>
    Task<Form?> GetFormAsync(Organization organization, string key);

    /// <summary>
    /// Creates a new form in the specified organization.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> that will own the form.</param>
    /// <param name="key">The key of the form to create.</param>
    /// <param name="definition">The serialized <see cref="FormDefinition"/> that describes the fields in the form.</param>
    /// <param name="enabled">A boolean indicating if the form is enabled.</param>
    /// <param name="actor">The <see cref="Member"/> that is creating the form.</param>
    Task<Form> CreateFormAsync(Organization organization, string key, string definition, bool enabled, Member actor);

    /// <summary>
    /// Saves the provided form.
    /// </summary>
    /// <param name="form">The <see cref="Form"/> to save.</param>
    /// <param name="actor">The <see cref="Member"/> that is saving the form.</param>
    Task SaveFormAsync(Form form, Member actor);

    /// <summary>
    /// Deletes the provided form.
    /// </summary>
    /// <param name="form">The <see cref="Form"/> to delete.</param>
    /// <param name="actor">The <see cref="Member"/> that is deleting the form.</param>
    Task DeleteFormAsync(Form form, Member actor);
}
