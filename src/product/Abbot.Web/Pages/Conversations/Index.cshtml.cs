using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serious.Abbot.Pages.Conversations;

public class IndexPage : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Index");
    }
}
