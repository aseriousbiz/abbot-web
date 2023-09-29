using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Extensions;
using Serious.Abbot.Routing;

namespace Serious.Abbot.Components;

[ViewComponent]
public class AddToSlackButton : ViewComponent
{
    readonly IUrlGenerator _urlGenerator;

    public AddToSlackButton(IUrlGenerator urlGenerator)
    {
        _urlGenerator = urlGenerator;
    }

    public IViewComponentResult Invoke(string verb, bool first)
    {
        var organization = HttpContext.GetCurrentOrganization();
        var appName = organization?.BotAppName ?? "Abbot";
        var model = new SlackButtonModel(appName, _urlGenerator.SlackInstallUrl(), verb, first);

        return View(model);
    }
}

public record SlackButtonModel(string AppName, string InstallUrl, string Verb, bool First);
