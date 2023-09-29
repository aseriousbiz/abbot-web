using System;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Serious.Abbot.Web.Infrastructure;

public class AbbotApiExplorerVisibilityConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        var ns = action.Controller.ControllerType.Namespace;
        action.ApiExplorer.IsVisible = ns?.EndsWith("InternalApi", StringComparison.Ordinal) ?? false;
    }
}
