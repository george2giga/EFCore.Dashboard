using System.Globalization;
using Bogus;
using EFCore.Dashboard.StressSample.Models;
using Microsoft.EntityFrameworkCore;

namespace EFCore.Dashboard.StressSample.Data;

/// <summary>
/// Seeds a stress-test dataset across every sample resource. Bogus generates
/// the values and EF Core persists them in batches. All counts scale with the
/// SEED_SCALE environment variable; the default 1.0 produces roughly 100k rows,
/// while small values like 0.01 produce a fast smoke-test dataset.
/// </summary>
public static class SeedData
{
    private const int BatchSize = 1000;
    private static readonly DateTime SeedNow = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset SeedNowOffset = new(SeedNow);

    public static async Task InitializeAsync(AppDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Articles.AnyAsync())
            return;

        Randomizer.Seed = new Random(20260801);
        var f = new Faker("en");
        var scale = Scale();

        var publishers = await SeedPublishersAsync(db, f, Count(10, scale));
        var categories = await SeedCategoriesAsync(db, f, Count(25, scale));
        var tags = await SeedTagsAsync(db, f, Count(250, scale));
        var authors = await SeedAuthorsAsync(db, f, Count(60, scale), publishers);
        var series = await SeedSeriesAsync(db, f, Count(40, scale), publishers);
        var issues = await SeedIssuesAsync(db, f, Count(30, scale));
        var articles = await SeedArticlesAsync(db, f, Count(4000, scale), categories, authors, series, tags, issues);

        await SeedRevisionsAsync(db, f, Count(8000, scale), articles);
        await SeedCommentsAsync(db, f, Count(40000, scale), articles);
        await SeedAttachmentsAsync(db, f, Count(12000, scale), articles);
        await SeedMediaAsync(db, f, Count(3200, scale), articles);
        await SeedSubscribersAsync(db, f, Count(8000, scale), tags, articles);
        await transaction.CommitAsync();
    }

    private static async Task<List<Publisher>> SeedPublishersAsync(AppDbContext db, Faker f, int count)
    {
        var pending = new List<Publisher>();
        for (var i = 0; i < count; i++)
        {
            pending.Add(new Publisher
            {
                Name = f.Company.CompanyName(),
                Url = $"https://publisher-{i + 1}.example.com"
            });
        }
        await PersistAsync(db, pending);
        return pending;
    }

    private static async Task<List<Category>> SeedCategoriesAsync(AppDbContext db, Faker f, int count)
    {
        var sections = new[] { "News", "Engineering", "Product", "Design", "Culture", "Tutorials", "Research", "Opinion", "Events", "Announcements" };
        var pending = new List<Category>();
        for (var i = 0; i < count; i++)
        {
            var description = f.Lorem.Paragraphs(1, 2);
            var category = new Category
            {
                PublicId = f.Random.Guid(),
                Name = $"{f.Random.ArrayElement(sections)} {string.Join(" ", f.Random.WordsArray(1, 2))}",
                Description = description[..Math.Min(description.Length, 500)],
                SortOrder = i,
                IsActive = f.Random.Bool()
            };
            if (i >= 4 && f.Random.Bool(0.6f))
                category.Parent = pending[f.Random.Number(i - 4)];
            pending.Add(category);
        }
        await PersistAsync(db, pending);
        return pending;
    }

    private static async Task<List<Tag>> SeedTagsAsync(AppDbContext db, Faker f, int count)
    {
        var pending = new List<Tag>();
        for (var i = 0; i < count; i++)
        {
            pending.Add(new Tag
            {
                Name = string.Join(" ", f.Random.WordsArray(1, 2))
            });
        }
        await PersistAsync(db, pending);
        return pending;
    }

    private static async Task<List<Author>> SeedAuthorsAsync(
        AppDbContext db,
        Faker f,
        int count,
        List<Publisher> publishers)
    {
        var pending = new List<Author>();
        for (var i = 0; i < count; i++)
        {
            var firstName = f.Name.FirstName();
            var lastName = f.Name.LastName();
            var author = new Author
            {
                PublicId = f.Random.Guid(),
                Name = $"{firstName} {lastName}",
                Bio = f.Lorem.Paragraphs(2, 3),
                Email = $"author-{i + 1}@example.com",
                Status = f.Random.Enum<AuthorStatus>(),
                JoinedOn = DateOnly.FromDateTime(f.Date.Between(SeedNow.AddYears(-12), SeedNow))
            };
            if (f.Random.Bool(0.9f))
                author.Publisher = f.Random.ListItem(publishers);
            pending.Add(author);
        }
        await PersistAsync(db, pending);

        await SeedAuthorProfilesAsync(db, f, pending);
        await SeedAuthorSkillsAsync(db, f, pending);
        return pending;
    }

    private static async Task SeedAuthorProfilesAsync(AppDbContext db, Faker f, List<Author> authors)
    {
        var pending = new List<AuthorProfile>();
        foreach (var author in authors)
        {
            if (!f.Random.Bool(0.92f)) continue;
            pending.Add(new AuthorProfile
            {
                Location = $"{f.Address.City()}, {f.Address.StateAbbr()}",
                Rating = Math.Round(f.Random.Double(3.0, 5.0) * 10) / 10,
                Author = author
            });
        }
        await PersistAsync(db, pending);
    }

    private static async Task SeedAuthorSkillsAsync(AppDbContext db, Faker f, List<Author> authors)
    {
        var skills = new[] { "Writing", "Editing", "Photography", "SEO", "Interviewing", "Data Analysis", "Video", "Podcasting", "Research", "Copywriting" };
        var pending = new List<AuthorSkill>();
        foreach (var author in authors)
        {
            var skillCount = f.Random.Number(1, 5);
            foreach (var skill in f.Random.ArrayElements(skills, skillCount))
            {
                pending.Add(new AuthorSkill
                {
                    Name = skill,
                    Proficiency = f.Random.Enum<Proficiency>(),
                    YearsExperience = f.Random.Number(0, 25),
                    Author = author
                });
            }
        }
        await PersistAsync(db, pending);
    }

    private static async Task<List<Series>> SeedSeriesAsync(AppDbContext db, Faker f, int count, List<Publisher> publishers)
    {
        var pending = new List<Series>();
        for (var i = 0; i < count; i++)
        {
            var series = new Series
            {
                Name = f.Company.CatchPhrase()
            };
            if (f.Random.Bool(0.85f))
                series.Publisher = f.Random.ListItem(publishers);
            pending.Add(series);
        }
        await PersistAsync(db, pending);
        return pending;
    }

    private static async Task<List<Issue>> SeedIssuesAsync(AppDbContext db, Faker f, int count)
    {
        var pending = new List<Issue>();
        for (var i = 0; i < count; i++)
        {
            var status = f.Random.Enum<IssueStatus>();
            pending.Add(new Issue
            {
                Name = $"Issue {f.Random.Number(1, 999)} - {f.Company.CatchPhrase()}",
                PublishedOn = status == IssueStatus.Draft
                    ? null
                    : DateOnly.FromDateTime(f.Date.Between(SeedNow.AddDays(-365), SeedNow)),
                Status = status
            });
        }
        await PersistAsync(db, pending);
        return pending;
    }

    private static async Task<List<Article>> SeedArticlesAsync(
        AppDbContext db,
        Faker f,
        int count,
        List<Category> categories,
        List<Author> authors,
        List<Series> series,
        List<Tag> tags,
        List<Issue> issues)
    {
        var pending = new List<Article>();
        var articles = new List<Article>();
        for (var i = 0; i < count; i++)
        {
            var slug = f.Lorem.Slug();
            var status = f.Random.Enum<ArticleStatus>();
            var article = new Article
            {
                PublicId = f.Random.Guid(),
                Title = f.Hacker.Phrase(),
                Slug = $"{slug[..Math.Min(slug.Length, 165)]}-{i + 1}",
                Content = $"## {f.Company.CatchPhrase()}\n\n{f.Lorem.Paragraphs(3, 6)}",
                Status = status,
                Featured = f.Random.Bool(0.15f),
                Rating = Math.Round(f.Random.Double(1.0, 5.0) * 10) / 10,
                CreatedAt = f.Date.Between(SeedNow.AddDays(-730), SeedNow),
                Category = f.Random.ListItem(categories),
                Author = f.Random.ListItem(authors)
            };
            if (f.Random.Bool(0.6f))
                article.Series = f.Random.ListItem(series);
            if (status == ArticleStatus.Draft && f.Random.Bool(0.3f))
                article.ScheduledAt = f.Date.BetweenOffset(SeedNowOffset.AddDays(-30), SeedNowOffset.AddDays(30));
            article.Tags.AddRange(f.Random.ListItems(tags, f.Random.Number(1, Math.Min(4, tags.Count))));
            pending.Add(article);

            if (pending.Count >= BatchSize)
            {
                await PersistAsync(db, pending);
                articles.AddRange(pending);
                pending.Clear();
            }
        }
        if (pending.Count > 0)
        {
            await PersistAsync(db, pending);
            articles.AddRange(pending);
        }

        foreach (var issue in issues)
        {
            var assigned = f.Random.Number(10, 40);
            foreach (var article in f.Random.ArrayElements(articles.ToArray(), Math.Min(assigned, count)))
                issue.Articles.Add(article);
        }
        await db.SaveChangesAsync();
        return articles;
    }

    private static async Task SeedRevisionsAsync(AppDbContext db, Faker f, int count, List<Article> articles)
    {
        var pending = new List<ArticleRevision>();
        for (var i = 0; i < count; i++)
        {
            var article = f.Random.ListItem(articles);
            pending.Add(new ArticleRevision
            {
                Title = f.Company.CatchPhrase(),
                Body = f.Lorem.Paragraphs(2, 5),
                CreatedAt = f.Date.Between(article.CreatedAt, SeedNow),
                Article = article
            });
        }
        await PersistAsync(db, pending);
    }

    private static async Task SeedCommentsAsync(AppDbContext db, Faker f, int count, List<Article> articles)
    {
        var pending = new List<Comment>();
        for (var i = 0; i < count; i++)
        {
            var name = f.Name.FullName();
            var article = f.Random.ListItem(articles);
            pending.Add(new Comment
            {
                AuthorName = name,
                AuthorEmail = $"commenter-{i + 1}@example.com",
                Body = f.Rant.Review(),
                Status = f.Random.Enum<CommentStatus>(),
                CreatedAt = f.Date.Between(article.CreatedAt, SeedNow),
                Article = article
            });
        }
        await PersistAsync(db, pending);
    }

    private static async Task SeedAttachmentsAsync(AppDbContext db, Faker f, int count, List<Article> articles)
    {
        var fileTypes = new[]
        {
            (Extension: ".pdf", MimeType: "application/pdf"),
            (Extension: ".png", MimeType: "image/png"),
            (Extension: ".jpg", MimeType: "image/jpeg"),
            (Extension: ".zip", MimeType: "application/zip"),
            (Extension: ".txt", MimeType: "text/plain")
        };
        var pending = new List<Attachment>();
        for (var i = 0; i < count; i++)
        {
            var fileType = f.Random.ArrayElement(fileTypes);
            pending.Add(new Attachment
            {
                Name = $"attachment-{i + 1}{fileType.Extension}",
                MimeType = fileType.MimeType,
                SizeBytes = f.Random.Long(1024, 10L * 1024 * 1024),
                Article = f.Random.ListItem(articles)
            });
        }
        await PersistAsync(db, pending);
    }

    private static async Task SeedMediaAsync(AppDbContext db, Faker f, int count, List<Article> articles)
    {
        var mimes = new[] { "image/jpeg", "image/png", "image/webp", "image/svg+xml" };
        var pending = new List<MediaAsset>();
        foreach (var i in Enumerable.Range(0, count))
        {
            var cover = i < articles.Count ? articles[i] : null;
            pending.Add(new MediaAsset
            {
                Url = $"https://example.com/media/{i + 1}.jpg",
                MimeType = f.Random.ArrayElement(mimes),
                Width = f.Random.Number(640, 3840),
                Height = f.Random.Number(360, 2160),
                Kind = f.Random.Enum<MediaKind>(),
                CoverArticle = cover
            });
        }
        await PersistAsync(db, pending);
    }

    private static async Task SeedSubscribersAsync(AppDbContext db, Faker f, int count, List<Tag> tags, List<Article> articles)
    {
        var pending = new List<NewsletterSubscriber>();
        for (var i = 0; i < count; i++)
        {
            var subscriber = new NewsletterSubscriber
            {
                PublicId = f.Random.Guid(),
                Email = $"subscriber-{i + 1:D8}@example.com",
                IsActive = f.Random.Bool(0.9f),
                SubscribedAt = f.Date.Between(SeedNow.AddDays(-365), SeedNow)
            };
            if (f.Random.Bool(0.3f))
                subscriber.FavoriteArticle = f.Random.ListItem(articles);
            subscriber.Interests.AddRange(f.Random.ListItems(tags, f.Random.Number(1, Math.Min(5, tags.Count))));
            pending.Add(subscriber);
        }
        await PersistAsync(db, pending);
    }

    private static double Scale()
    {
        var raw = Environment.GetEnvironmentVariable("SEED_SCALE");
        if (string.IsNullOrWhiteSpace(raw)) return 1.0;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) ||
            !double.IsFinite(scale) || scale is < 0.01 or > 10.0)
        {
            throw new InvalidOperationException("SEED_SCALE must be a number from 0.01 to 10 using a period as the decimal separator.");
        }

        return scale;
    }

    private static int Count(int baseCount, double scale) => Math.Max(1, (int)(baseCount * scale));

    private static async Task PersistAsync<TEntity>(AppDbContext db, List<TEntity> entities)
    {
        foreach (var batch in entities.Chunk(BatchSize))
        {
            db.AddRange(batch);
            await db.SaveChangesAsync();
        }
    }
}
