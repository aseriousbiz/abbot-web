using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Serious.Abbot.Infrastructure;

namespace Serious.Abbot.Web;

public class LowercaseParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        // We're lower-casing on purpose.
        return value?.ToString()?.ToLowerInvariant();
    }
}

public class LowercasePageRoutingConvention : IPageRouteModelConvention
{
    readonly IReadOnlyList<string> _lowercasedTokenNames;

    public LowercasePageRoutingConvention(params string[] lowercasedTokenNames)
    {
        _lowercasedTokenNames = lowercasedTokenNames;
    }

    public void Apply(PageRouteModel model)
    {
        RoutingHelper.ApplyConstraintToAllInstancesOfToken(model.Selectors, _lowercasedTokenNames, "tolower");
    }
}
