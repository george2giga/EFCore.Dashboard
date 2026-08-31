using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EFCore.Dashboard.Authentication.Cookies.Pages.DashboardAuth;

[Authorize(AuthenticationSchemes = DashboardCookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class LogoutModel : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(DashboardCookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect(Url.Content("~/"));
    }
}
