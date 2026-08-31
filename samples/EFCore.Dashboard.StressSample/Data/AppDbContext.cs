using Microsoft.EntityFrameworkCore;
using EFCore.Dashboard.StressSample.Models;

namespace EFCore.Dashboard.StressSample.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<AuthorProfile> AuthorProfiles => Set<AuthorProfile>();
    public DbSet<AuthorSkill> AuthorSkills => Set<AuthorSkill>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<ArticleRevision> ArticleRevisions => Set<ArticleRevision>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>().HasIndex(x => x.PublicId).IsUnique();
        modelBuilder.Entity<Article>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Author>().HasIndex(x => x.PublicId).IsUnique();
        modelBuilder.Entity<Category>().HasIndex(x => x.PublicId).IsUnique();
        modelBuilder.Entity<NewsletterSubscriber>().HasIndex(x => x.PublicId).IsUnique();
        modelBuilder.Entity<NewsletterSubscriber>().HasIndex(x => x.Email).IsUnique();

        modelBuilder.Entity<Category>().HasMany(x => x.Children).WithOne(x => x.Parent).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Category>().HasMany(x => x.Articles).WithOne(x => x.Category).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Publisher>().HasMany(x => x.Authors).WithOne(x => x.Publisher).HasForeignKey(x => x.PublisherId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Publisher>().HasMany(x => x.Series).WithOne(x => x.Publisher).HasForeignKey(x => x.PublisherId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Series>().HasMany(x => x.Articles).WithOne(x => x.Series).HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Author>().HasMany(x => x.Articles).WithOne(x => x.Author).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Author>().HasMany(x => x.Skills).WithOne(x => x.Author).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Author>().HasOne(x => x.Profile).WithOne(x => x.Author).HasForeignKey<AuthorProfile>(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Article>().HasMany(x => x.Tags).WithMany(x => x.Articles);
        modelBuilder.Entity<Article>().HasMany(x => x.Issues).WithMany(x => x.Articles);
        modelBuilder.Entity<Article>().HasMany(x => x.Revisions).WithOne(x => x.Article).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Article>().HasMany(x => x.Comments).WithOne(x => x.Article).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Article>().HasMany(x => x.Attachments).WithOne(x => x.Article).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MediaAsset>().HasOne(x => x.CoverArticle).WithOne(x => x.Cover).HasForeignKey<MediaAsset>(x => x.CoverArticleId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<NewsletterSubscriber>().HasMany(x => x.Interests).WithMany(x => x.Subscribers);
        modelBuilder.Entity<NewsletterSubscriber>().HasOne(x => x.FavoriteArticle).WithMany().HasForeignKey(x => x.FavoriteArticleId).OnDelete(DeleteBehavior.SetNull);
    }
}
