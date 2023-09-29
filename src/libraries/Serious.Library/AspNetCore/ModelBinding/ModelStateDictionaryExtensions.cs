using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Serious.AspNetCore.ModelBinding;

/// <summary>
/// Extension methods on <see cref="ModelStateDictionary"/>.
/// </summary>
public static class ModelStateDictionaryExtensions
{
    /// <summary>
    /// Removes all model state entries except for the ones that match the prefix. This is useful when submitting
    /// a sub-form to a method and you want to validate only the sub-form.
    /// </summary>
    /// <param name="modelStateDictionary">The <see cref="ModelStateDictionary"/>.</param>
    /// <param name="prefix">The model state prefix to match.</param>
    /// <remarks>Returns a reference to the passed in modified <see cref="ModelStateDictionary"/>.</remarks>
    public static ModelStateDictionary RemoveExcept(this ModelStateDictionary modelStateDictionary, string prefix)
    {
        var keysToKeep = modelStateDictionary
            .FindKeysWithPrefix(prefix)
            .Select(k => k.Key);
        var keysToDelete = modelStateDictionary.Keys.Except(keysToKeep).ToList();
        foreach (var keyToDelete in keysToDelete)
        {
            modelStateDictionary.Remove(keyToDelete);
        }

        return modelStateDictionary;
    }
}
