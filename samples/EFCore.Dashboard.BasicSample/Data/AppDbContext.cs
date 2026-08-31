using Microsoft.EntityFrameworkCore;
using EFCore.Dashboard.BasicSample.Models;

namespace EFCore.Dashboard.BasicSample.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Article>().HasOne(x => x.Author).WithMany(x => x.Articles).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Article>().HasOne(x => x.Category).WithMany(x => x.Articles).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Article>().HasMany(x => x.Tags).WithMany(x => x.Articles);
        modelBuilder.Entity<Article>().HasMany(x => x.Comments).WithOne(x => x.Article).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Author>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<NewsletterSubscriber>().HasIndex(x => x.Email).IsUnique();
    }
}
