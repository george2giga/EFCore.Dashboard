using System.ComponentModel.DataAnnotations;
using EFCore.Dashboard.BasicSample.Data;
using EFCore.Dashboard.BasicSample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Dashboard.BasicSample.Pages;

public sealed class ArticleModel(AppDbContext db) : PageModel
{
    public Article? Article { get; private set; }
    public IReadOnlyList<Article> Related { get; private set; } = [];
    [BindProperty] public CommentInput Comment { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
        => await LoadAsync(slug, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostCommentAsync(string slug, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var article = await db.Articles.SingleOrDefaultAsync(candidate =>
            candidate.Slug == slug && candidate.Status == ArticleStatus.Published && candidate.PublishedAt <= now, cancellationToken);
        if (article is null) return NotFound();
        if (!string.IsNullOrEmpty(Comment.Website))
            return RedirectToPage("/Article", new { slug, commented = true });

        if (!ModelState.IsValid)
        {
            await LoadAsync(slug, cancellationToken);
            return Page();
        }

        db.Comments.Add(new Comment
        {
            ArticleId = article.Id,
            AuthorName = Comment.Name.Trim(),
            AuthorEmail = Comment.Email.Trim(),
            Body = Comment.Body.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("/Article", pageHandler: null, routeValues: new { slug, commented = true }, fragment: "comments");
    }

    private async Task<bool> LoadAsync(string slug, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        Article = await db.Articles
            .AsNoTracking()
            .Include(article => article.Author)
            .Include(article => article.Category)
            .Include(article => article.Tags)
            .Include(article => article.Comments.Where(comment => comment.Status == CommentStatus.Approved))
            .SingleOrDefaultAsync(article => article.Slug == slug && article.Status == ArticleStatus.Published && article.PublishedAt <= now, cancellationToken);
        if (Article is null) return false;

        Related = await db.Articles
            .AsNoTracking()
            .Where(article => article.Status == ArticleStatus.Published && article.PublishedAt <= now && article.Id != Article.Id && article.CategoryId == Article.CategoryId)
            .Include(article => article.Author)
            .OrderByDescending(article => article.PublishedAt)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return true;
    }

    public sealed class CommentInput
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = string.Empty;
        [Required, MaxLength(1200)] public string Body { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
    }
}
