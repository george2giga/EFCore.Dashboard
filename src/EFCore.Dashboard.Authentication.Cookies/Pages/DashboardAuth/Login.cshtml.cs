using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace EFCore.Dashboard.Authentication.Cookies.Pages.DashboardAuth;

[AllowAnonymous]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class LoginModel : PageModel
{
    private readonly DashboardCookieAuthenticationSettings _settings;
    private readonly DashboardCredentialValidator _credentials;

    public LoginModel(IServiceProvider services)
    {
        _settings = services.GetRequiredService<DashboardCookieAuthenticationSettings>();
        _credentials = services.GetRequiredService<DashboardCredentialValidator>();
    }

    [BindProperty]
    [Required]
    [StringLength(256)]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [StringLength(1024)]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
        => IsTransportAllowed() ? Page() : HttpsRequired();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsTransportAllowed())
            return HttpsRequired();
        if (!ModelState.IsValid)
            return Page();

        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var credentialResult = _credentials.Validate(Username, Password, clientKey);
        if (credentialResult != DashboardCredentialResult.Success)
        {
            if (credentialResult == DashboardCredentialResult.LockedOut)
                Response.Headers.RetryAfter = ((int)_settings.LockoutDuration.TotalSeconds).ToString();
            ModelState.AddModelError(string.Empty, "Sign-in failed. Check the credentials or wait before trying again.");
            return Page();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _settings.Username),
            new Claim(ClaimTypes.Name, _settings.Username)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            DashboardCookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            DashboardCookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { AllowRefresh = true });

        return !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? LocalRedirect(ReturnUrl)
            : LocalRedirect(Url.Page("/Admin/Index") ?? Url.Content("~/"));
    }

    private bool IsTransportAllowed() => _settings.AllowInsecureHttp || Request.IsHttps;

    private static ContentResult HttpsRequired() => new()
    {
        Content = "EFCore.Dashboard cookie authentication requires HTTPS.",
        ContentType = "text/plain",
        StatusCode = StatusCodes.Status400BadRequest
    };
}
