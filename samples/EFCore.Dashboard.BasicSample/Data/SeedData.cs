using System.Reflection;
using Microsoft.EntityFrameworkCore;
using EFCore.Dashboard.BasicSample.Models;

namespace EFCore.Dashboard.BasicSample.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        if (await db.Categories.AnyAsync()) return;
        var now = DateTime.UtcNow;

        var product = new Category { Name = "Product" };
        var engineering = new Category { Name = "Engineering" };
        var fieldNotes = new Category { Name = "Field Notes" };
        db.AddRange(product, engineering, fieldNotes);

        var maya = new Author
        {
            Name = "Maya Chen",
            Slug = "maya-chen",
            Role = "Product editor",
            Bio = "Writes about quiet software, useful constraints, and the details that make tools feel dependable.",
            Email = "maya@example.test",
            Website = "https://example.com",
            AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=320&q=85",
            AvailableForWork = false,
            JoinedOn = new DateOnly(2024, 3, 12)
        };
        var theo = new Author
        {
            Name = "Theo Martin",
            Slug = "theo-martin",
            Role = "Software designer",
            Bio = "Builds server-rendered products and documents the tradeoffs behind simple technical systems.",
            Email = "theo@example.test",
            Website = "https://example.com",
            AvatarUrl = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=320&q=85",
            AvailableForWork = true,
            JoinedOn = new DateOnly(2024, 7, 8)
        };
        var noa = new Author
        {
            Name = "Noa Williams",
            Slug = "noa-williams",
            Role = "Independent researcher",
            Bio = "Studies how small teams make decisions, maintain software, and keep operational complexity under control.",
            Email = "noa@example.test",
            Website = "https://example.com",
            AvatarUrl = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=320&q=85",
            AvailableForWork = false,
            JoinedOn = new DateOnly(2025, 1, 20)
        };
        db.AddRange(maya, theo, noa);

        var intro = PublishedArticle(
            "The useful admin screen hiding in your data model",
            "the-useful-admin-screen-hiding-in-your-data-model",
            "A database already describes more of an internal tool than we usually admit.",
            "The tables, labels, constraints, and relationships in a mature application are not implementation debris. Together they are a practical map of how the business works. A good internal interface can begin with that map instead of inventing a second model.\n\nThe useful move is not to generate everything and walk away. It is to establish sensible conventions, then make the exceptions obvious. Hide a sensitive field. Choose a human label. Mark the one image worth previewing. The common path stays short while the unusual parts remain deliberate.\n\nThat balance is what makes a generated surface feel like part of the application rather than a separate product that happens to share its database.",
            product, maya, now.AddDays(-1), 6, featured: true);
        var razor = PublishedArticle(
            "The case for a quieter web stack",
            "the-case-for-a-quieter-web-stack",
            "Razor and small HTML exchanges can carry a surprising amount of product.",
            "There is a particular calm to systems where the URL tells the truth, the server owns state, and viewing source still explains most of the page. That calm is not nostalgia. It is the result of having fewer copies of the same idea.\n\nServer-rendered HTML does not rule out responsive interactions. A search box can replace one fragment. A form can preserve validation state. A table can sort without turning the application into an API and a second application layered on top.\n\nProgressive enhancement works best when it is treated as a budget: spend JavaScript where immediacy matters, and let ordinary web behavior carry everything else.",
            engineering, theo, now.AddDays(-4), 5);
        var constraints = PublishedArticle(
            "Constraints are a feature, not an apology",
            "constraints-are-a-feature-not-an-apology",
            "Small software becomes distinctive when it is clear about what it will not do.",
            "A focused library should leave more decisions with its host than it takes away. Authentication, database configuration, routes, and deployment are already part of the application. Replacing them creates novelty where compatibility would be more valuable.\n\nConstraints also improve the interface. One strong path is easier to learn than five configurable modes. A limited set of field types is easier to test than a universal renderer. Clear boundaries make extension points meaningful because they appear only where real differences exist.\n\nSimplicity is not the absence of capability. It is capability with a visible shape.",
            fieldNotes, noa, now.AddDays(-7), 4);
        var metadata = PublishedArticle(
            "Reading an EF Core model like a product brief",
            "reading-an-ef-core-model-like-a-product-brief",
            "What keys, relationships, and delete behavior reveal about an application.",
            "Entity metadata is full of product decisions. Required foreign keys describe ownership. Unique indexes describe identity. Delete behavior reveals whether a record is history, infrastructure, or an interchangeable detail.\n\nSurfacing those decisions in an internal tool makes the model easier to inspect and harder to misunderstand. It also creates useful pressure: vague names and accidental cascades become visible to people beyond the data layer.\n\nThe result is a feedback loop between model quality and operational clarity. Better metadata makes a better dashboard, and a legible dashboard exposes a better model.",
            engineering, maya, now.AddDays(-11), 7);
        var maintenance = PublishedArticle(
            "Maintenance is a design discipline",
            "maintenance-is-a-design-discipline",
            "The most premium feeling in software is confidence that tomorrow will be uneventful.",
            "Polish is often described in pixels, but dependable software has its own visual quality. Names line up. Errors explain the next action. Pages return to the place a person expects. The implementation reads in the same order as the interface.\n\nThese details come from maintenance thinking: choosing the smallest mechanism, deleting unused branches, and keeping framework behavior visible. None of them makes a dramatic launch note. Together they make a product feel considered.\n\nThe work is less about predicting every future requirement than making the current behavior easy to understand when the future arrives.",
            fieldNotes, theo, now.AddDays(-15), 5);
        var release = PublishedArticle(
            "Shipping a small library without making it small-minded",
            "shipping-a-small-library-without-making-it-small-minded",
            "A release checklist for software that intends to stay understandable.",
            "Before publishing, test the package rather than only the project. Run the sample from a clean database. Verify the first screen on a narrow viewport. Read the public API as if every type will need support for years.\n\nDocumentation should show the shortest correct setup. Samples should demonstrate decisions, not collect every experiment the maintainers have ever needed. Extension points should earn their names with a concrete use case.\n\nA small release can still be ambitious. Its ambition is to remain useful without demanding that every consumer adopt the library's worldview.",
            product, noa, now.AddDays(-20), 6);
        var draft = PublishedArticle(
            "Notes on the next useful boundary",
            "notes-on-the-next-useful-boundary",
            "An unpublished working note for the editorial dashboard.",
            "This draft exists to demonstrate that the public site only selects published work while editors can continue to manage everything from the dashboard.",
            product, maya, now, 3);
        draft.Status = ArticleStatus.Draft;
        draft.PublishedAt = null;
        draft.Featured = false;
        db.AddRange(intro, razor, constraints, metadata, maintenance, release, draft);

        var announcements = new Tag { Name = "Announcements" };
        var architecture = new Tag { Name = "Architecture" };
        var craft = new Tag { Name = "Craft" };
        var web = new Tag { Name = "The Web" };
        db.AddRange(announcements, architecture, craft, web);
        intro.Tags.AddRange([announcements, architecture]);
        razor.Tags.AddRange([web, craft]);
        constraints.Tags.AddRange([architecture, craft]);
        metadata.Tags.Add(architecture);
        maintenance.Tags.Add(craft);
        release.Tags.AddRange([announcements, craft]);

        db.AddRange(
            ApprovedComment(intro, "Leah", "This captures why internal tools should follow the model instead of competing with it.", now.AddHours(-8)),
            ApprovedComment(intro, "Jonas", "The distinction between conventions and exceptions is especially useful.", now.AddHours(-3)),
            ApprovedComment(razor, "Sam", "Quiet is the right word. Fewer state boundaries make debugging feel ordinary again.", now.AddDays(-2)),
            ApprovedComment(constraints, "Iris", "A visible shape is a much better goal than a long capability list.", now.AddDays(-5)),
            new Comment { Article = maintenance, AuthorName = "Ari", AuthorEmail = "ari@example.test", Body = "Looking forward to a follow-up on maintenance budgets.", Status = CommentStatus.Pending, CreatedAt = new DateTimeOffset(now.AddHours(-1), TimeSpan.Zero) });

        db.Photos.AddRange(
            new Photo
            {
                Title = "Lines toward the sky",
                Image = LoadImage("engineering.jpg"),
                Caption = "A study in structure, repetition, and the useful space between constraints.",
                Credit = "Dashboard Blog archive",
                CapturedOn = new DateOnly(2025, 4, 18),
                Published = true,
                UploadedAt = new DateTimeOffset(now.AddDays(-18), TimeSpan.Zero)
            },
            new Photo
            {
                Title = "The morning edition",
                Image = LoadImage("news.jpg"),
                Caption = "A quiet record of how information moved before every update became immediate.",
                Credit = "Dashboard Blog archive",
                CapturedOn = new DateOnly(2025, 6, 2),
                Published = true,
                UploadedAt = new DateTimeOffset(now.AddDays(-12), TimeSpan.Zero)
            },
            new Photo
            {
                Title = "A working desk",
                Image = LoadImage("product.jpg"),
                Caption = "The ordinary tools behind a small piece of dependable software.",
                Credit = "Dashboard Blog archive",
                CapturedOn = new DateOnly(2025, 7, 9),
                UploadedAt = new DateTimeOffset(now.AddDays(-6), TimeSpan.Zero)
            });

        db.NewsletterSubscribers.AddRange(
            new NewsletterSubscriber { Email = "reader@example.test", SubscribedAt = new DateTimeOffset(now.AddDays(-30), TimeSpan.Zero) },
            new NewsletterSubscriber { Email = "studio@example.test", SubscribedAt = new DateTimeOffset(now.AddDays(-12), TimeSpan.Zero) },
            new NewsletterSubscriber { Email = "archive@example.test", Active = false, SubscribedAt = new DateTimeOffset(now.AddDays(-45), TimeSpan.Zero) });
        await db.SaveChangesAsync();
    }

    private static Article PublishedArticle(
        string title,
        string slug,
        string excerpt,
        string content,
        Category category,
        Author author,
        DateTime publishedAt,
        int readingMinutes,
        bool featured = false) => new()
        {
            Title = title,
            Slug = slug,
            Excerpt = excerpt,
            Content = content,
            Status = ArticleStatus.Published,
            Featured = featured,
            ReadingMinutes = readingMinutes,
            CreatedAt = publishedAt.AddDays(-2),
            PublishedAt = publishedAt,
            Category = category,
            Author = author
        };

    private static Comment ApprovedComment(Article article, string author, string body, DateTime createdAt) => new()
    {
        Article = article,
        AuthorName = author,
        AuthorEmail = $"{author.ToLowerInvariant()}@example.test",
        Body = body,
        Status = CommentStatus.Approved,
        CreatedAt = new DateTimeOffset(createdAt, TimeSpan.Zero)
    };

    private static byte[] LoadImage(string fileName)
    {
        var resourceName = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Seed image '{fileName}' is not embedded.", fileName);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

}
