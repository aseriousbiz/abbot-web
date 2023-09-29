using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Serious.Abbot.Pages.Shared.Components.StaffBar;

public class StaffBarViewComponent : ViewComponent
{
    public StaffBarViewComponent()
    {
    }

    public async Task<IViewComponentResult> InvokeAsync(PageModel? page)
    {
        if (!HttpContext.IsStaffMode())
        {
            return Content("");
        }

        return View(new ViewModel(page as AbbotPageModelBase));
    }

    public record ViewModel(AbbotPageModelBase? AbbotPageModel);
}
