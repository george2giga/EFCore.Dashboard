using System.ComponentModel.DataAnnotations;
using EFCore.Dashboard.BasicSample.Data;
using EFCore.Dashboard.BasicSample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Dashboard.BasicSample.Pages;

public sealed class SubscribeModel(AppDbContext db) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index", pageHandler: null, routeValues: null, fragment: "newsletter");

    public async Task<IActionResult> OnPostAsync(
        [FromForm, Required, EmailAddress, MaxLength(200)] string email,
        [FromForm] string? company,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return RedirectToPage("/Index", pageHandler: null, routeValues: new { subscription = "invalid" }, fragment: "newsletter");
        if (!string.IsNullOrEmpty(company))
            return RedirectToPage("/Index", pageHandler: null, routeValues: new { subscription = "joined" }, fragment: "newsletter");

        var normalized = email.Trim().ToLowerInvariant();
        var subscriber = await db.NewsletterSubscribers.SingleOrDefaultAsync(item => item.Email == normalized, cancellationToken);
        if (subscriber is null)
            db.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = normalized });
        else
            subscriber.Active = true;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("/Index", pageHandler: null, routeValues: new { subscription = "joined" }, fragment: "newsletter");
    }
}
