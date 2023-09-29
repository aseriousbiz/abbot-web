using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Mvc.ModelBinding;

public static class ModelStateTestExtensions
{
    /// <summary>
    /// Gets error messages associated with the specified key,
    /// or an empty list if there is no model state entry with the specified key.
    /// </summary>
    /// <param name="self">The <see cref="ModelStateDictionary"/> to retrieve errors from.</param>
    /// <param name="key">The key to look up errors from.</param>
    public static IReadOnlyList<string> ErrorsFor(this ModelStateDictionary self, string key) =>
        self.TryGetValue(key, out var state)
            ? state.Errors.Select(e => e.ErrorMessage).ToArray()
            : Array.Empty<string>();
}
