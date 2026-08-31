using EFCore.Dashboard.BasicSample.Data;
using EFCore.Dashboard.BasicSample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Dashboard.BasicSample.Pages;

public sealed class AuthorModel(AppDbContext db) : PageModel
{
    public Author? Author { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        Author = await db.Authors
            .AsNoTracking()
            .Include(author => author.Articles.Where(article => article.Status == ArticleStatus.Published && article.PublishedAt <= now))
            .ThenInclude(article => article.Category)
            .SingleOrDefaultAsync(author => author.Slug == slug &&
                author.Articles.Any(article => article.Status == ArticleStatus.Published && article.PublishedAt <= now), cancellationToken);
        return Author is null ? NotFound() : Page();
    }
}
