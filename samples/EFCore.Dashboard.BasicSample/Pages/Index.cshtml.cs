using EFCore.Dashboard.BasicSample.Data;
using EFCore.Dashboard.BasicSample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Dashboard.BasicSample.Pages;

public sealed class IndexModel(AppDbContext db) : PageModel
{
    public IReadOnlyList<Article> Articles { get; private set; } = [];
    public IReadOnlyList<Author> Authors { get; private set; } = [];
    public IReadOnlyList<Category> Categories { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Subscription { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var published = db.Articles
            .AsNoTracking()
            .Where(article => article.Status == ArticleStatus.Published && article.PublishedAt <= now)
            .Include(article => article.Author)
            .Include(article => article.Category);

        IQueryable<Article> stories = published;
        if (CategoryId is not null)
            stories = stories.Where(article => article.CategoryId == CategoryId);
        Articles = await stories.OrderByDescending(article => article.PublishedAt).ToArrayAsync(cancellationToken);
        Authors = await db.Authors.AsNoTracking()
            .Where(author => author.Articles.Any(article => article.Status == ArticleStatus.Published && article.PublishedAt <= now))
            .OrderBy(author => author.Name)
            .ToArrayAsync(cancellationToken);
        Categories = await db.Categories.AsNoTracking().OrderBy(category => category.Name).ToArrayAsync(cancellationToken);
        return Page();
    }
}
