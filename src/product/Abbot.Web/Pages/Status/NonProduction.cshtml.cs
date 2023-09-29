using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;

namespace Serious.Abbot.Pages.Status;

public class NonProductionModel : PageModel
{
    readonly IHostEnvironment _environment;

    public NonProductionModel(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public IActionResult OnGet() =>
        _environment.IsProduction()
            ? NotFound()
            : Page();
}
