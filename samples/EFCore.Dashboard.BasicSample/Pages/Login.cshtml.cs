using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EFCore.Dashboard.BasicSample.Pages;

public sealed class LoginModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "demo-administrator")],
                CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToPage("/Admin/Index");
    }
}
