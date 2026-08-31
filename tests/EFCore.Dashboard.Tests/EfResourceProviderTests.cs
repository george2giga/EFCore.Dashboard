using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;
using EFCore.Dashboard.Fields;
using EFCore.Dashboard.Extensibility;
using EFCore.Dashboard.Pages.Admin;
using EFCore.Dashboard.Web;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class EfResourceProviderTests
{
    [Fact]
    public void GetResources_excludes_implicit_join_entities()
    {
        var provider = BuildProvider();

        var resources = provider.GetResources();

        Assert.Contains(resources, x => x.EntityType == typeof(Blog));
        Assert.Contains(resources, x => x.EntityType == typeof(Post));
        Assert.Contains(resources, x => x.EntityType == typeof(Tag));
        Assert.Contains(resources, x => x.EntityType == typeof(Profile));
        Assert.DoesNotContain(resources, x => x.Name.Contains("BlogPost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetRelationships_reports_one_to_many_with_navigations_and_foreign_key()
    {
        var provider = BuildProvider();

        var relationship = provider.GetRelationships().Single(x =>
            x.SourceEntityType == typeof(Blog) && x.TargetEntityType == typeof(Post));

        Assert.Equal(RelationshipMultiplicity.OneToMany, relationship.Multiplicity);
        Assert.False(relationship.Required);
        Assert.Equal("Posts", relationship.NavigationName);
        Assert.Equal("Blog", relationship.InverseNavigationName);
        Assert.Equal("BlogId", relationship.ForeignKeyProperty);
        Assert.Equal(RelationshipDeleteBehavior.Restrict, relationship.DeleteBehavior);
    }

    [Fact]
    public void GetRelationships_reports_one_to_one()
    {
        var provider = BuildProvider();

        var relationship = provider.GetRelationships().Single(x =>
            x.SourceEntityType == typeof(Post) && x.TargetEntityType == typeof(Profile));

        Assert.Equal(RelationshipMultiplicity.OneToOne, relationship.Multiplicity);
        Assert.False(relationship.Required);
        Assert.Equal("Profile", relationship.NavigationName);
        Assert.Equal("PostId", relationship.ForeignKeyProperty);
        Assert.Equal(RelationshipDeleteBehavior.SetNull, relationship.DeleteBehavior);
    }

    [Fact]
    public void GetRelationships_reports_database_cascade_delete_behavior()
    {
        var provider = BuildProvider();

        var relationship = provider.GetRelationships().Single(x =>
            x.SourceEntityType == typeof(Category) && x.TargetEntityType == typeof(Article));

        Assert.Equal(RelationshipDeleteBehavior.Cascade, relationship.DeleteBehavior);
    }

    [Fact]
    public void GetRelationships_reports_each_many_to_many_pair_once()
    {
        var provider = BuildProvider();

        var relationships = provider.GetRelationships().Where(x =>
            x.Multiplicity == RelationshipMultiplicity.ManyToMany).ToArray();

        var relationship = Assert.Single(relationships);
        Assert.Equal(typeof(Post), relationship.SourceEntityType);
        Assert.Equal(typeof(Tag), relationship.TargetEntityType);
        Assert.False(relationship.Required);
        Assert.NotNull(relationship.JoinEntityTypeName);
    }

    [Fact]
    public async Task CountChildrenAsync_reports_referencing_rows_and_clears_after_removal()
    {
        var container = BuildContainer();

        using var scope = container.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelationshipTestContext>();
        await db.Database.EnsureCreatedAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IDashboardRepository>();

        var blog = new Blog { Name = "First blog" };
        db.Add(new Post { Title = "Post one", Blog = blog });
        db.Add(new Post { Title = "Post two", Blog = blog });
        await db.SaveChangesAsync();

        var blogResource = container.GetRequiredService<IDashboardResourceProvider>().GetResources()
            .Single(x => x.EntityType == typeof(Blog));
        Assert.Equal(2, await repository.CountChildrenAsync(blogResource, blog.Id));

        var postResource = container.GetRequiredService<IDashboardResourceProvider>().GetResources()
            .Single(x => x.EntityType == typeof(Post));
        await repository.DeleteAsync(postResource, 1);
        await repository.DeleteAsync(postResource, 2);

        Assert.Equal(0, await repository.CountChildrenAsync(blogResource, blog.Id));
    }

    [Fact]
    public void Byte_array_property_is_not_exposed_by_default()
    {
        var provider = BuildProvider();

        var asset = provider.GetResources().Single(x => x.EntityType == typeof(Asset));

        Assert.DoesNotContain(asset.Fields, x => x.Name == nameof(Asset.Cover));
    }

    [Fact]
    public void Byte_array_property_becomes_binary_field_when_editor_is_image()
    {
        var provider = BuildProviderWithOptions(dash =>
            dash.Resource<Asset>(resource => resource.Field(x => x.Cover).Editor(DashboardEditors.Image)));

        var asset = provider.GetResources().Single(x => x.EntityType == typeof(Asset));
        var cover = Assert.Single(asset.Fields, x => x.Name == nameof(Asset.Cover));
        Assert.IsType<BinaryField>(cover);
    }

    [Fact]
    public void Editor_rejects_unsupported_names()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddEFCoreDashboard<RelationshipTestContext>(dash =>
                dash.Resource<Asset>(resource => resource.Field(x => x.Cover).Editor("unsupported"))));

        Assert.Equal("editor", exception.ParamName);
    }

    [Fact]
    public void Relation_field_uses_principal_key_and_a_human_readable_label()
    {
        var provider = BuildProvider();

        var article = provider.GetResources().Single(x => x.EntityType == typeof(Article));
        var category = Assert.IsType<RelationField>(article.Fields.Single(x => x.Name == nameof(Article.CategoryCode)));

        Assert.Equal(nameof(Category.Code), category.PrincipalKeyProperty);
        Assert.Equal("Category Code", category.Label);
    }

    [Fact]
    public void Relation_field_reports_unique_foreign_keys()
    {
        var provider = BuildProvider();

        var profile = provider.GetResources().Single(x => x.EntityType == typeof(Profile));
        var post = provider.GetResources().Single(x => x.EntityType == typeof(Post));

        Assert.True(Assert.IsType<RelationField>(profile.Fields.Single(x => x.Name == nameof(Profile.PostId))).IsUnique);
        Assert.False(Assert.IsType<RelationField>(post.Fields.Single(x => x.Name == nameof(Post.BlogId))).IsUnique);
    }

    [Fact]
    public async Task Unique_relation_lookup_excludes_assigned_principals()
    {
        await using var container = BuildContainer();
        await using var scope = container.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RelationshipTestContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        var first = new Post { Title = "Assigned" };
        var second = new Post { Title = "Available" };
        var profile = new Profile { Bio = "Existing", Post = first };
        db.AddRange(first, second, profile);
        await db.SaveChangesAsync();

        var resources = scope.ServiceProvider.GetRequiredService<IDashboardResourceProvider>();
        var profileResource = resources.Find(typeof(Profile))!;
        var postResource = resources.Find(typeof(Post))!;
        var relation = Assert.IsType<RelationField>(profileResource.Fields.Single(x => x.Name == nameof(Profile.PostId)));
        var repository = scope.ServiceProvider.GetRequiredService<IDashboardRepository>();

        var options = await repository.LookupAsync(postResource, dependentResource: profileResource, relation: relation);

        Assert.DoesNotContain(options, option => option.Value == first.Id.ToString());
        Assert.Contains(options, option => option.Value == second.Id.ToString());
    }

    [Fact]
    public void Relation_field_removes_id_suffix_unless_a_label_is_configured()
    {
        var defaultProvider = BuildProvider();
        var defaultField = Assert.IsType<RelationField>(defaultProvider.GetResources()
            .Single(x => x.EntityType == typeof(Post)).Fields.Single(x => x.Name == nameof(Post.BlogId)));
        var configuredProvider = BuildProviderWithOptions(dash =>
            dash.Resource<Post>(resource => resource.Field(x => x.BlogId).Label("Publication")));
        var configuredField = Assert.IsType<RelationField>(configuredProvider.GetResources()
            .Single(x => x.EntityType == typeof(Post)).Fields.Single(x => x.Name == nameof(Post.BlogId)));

        Assert.Equal("Blog", defaultField.Label);
        Assert.Equal("Publication", configuredField.Label);
    }

    [Fact]
    public void RelatedRecordLink_create_many_builds_one_to_many_filter_links()
    {
        var provider = BuildProvider();
        var converter = new DashboardValueConverter();

        var blogResource = provider.GetResources().Single(x => x.EntityType == typeof(Blog));
        var postResource = provider.GetResources().Single(x => x.EntityType == typeof(Post));
        var links = RelatedRecordLink.CreateMany(provider, converter, blogResource, new Blog { Id = 7, Name = "Seven" });

        var link = Assert.Single(links);
        Assert.Equal(postResource.Slug, link.ResourceSlug);
        Assert.Equal(postResource.Label, link.ResourceLabel);
        Assert.Equal(nameof(Post.BlogId), link.FilterField);
        Assert.Equal("7", link.FilterValue);
    }

    [Fact]
    public void RelatedRecordLink_create_many_skips_alternate_keys_and_many_to_many()
    {
        var provider = BuildProvider();
        var converter = new DashboardValueConverter();

        var articleResource = provider.GetResources().Single(x => x.EntityType == typeof(Article));
        var postResource = provider.GetResources().Single(x => x.EntityType == typeof(Post));

        Assert.Empty(RelatedRecordLink.CreateMany(provider, converter, articleResource, new Article { Id = 3, CategoryCode = "code" }));
        Assert.Empty(RelatedRecordLink.CreateMany(provider, converter, postResource, new Post { Id = 9, Title = "Post" }));
    }

    [Fact]
    public void Field_visibility_can_target_lists_editors_or_every_surface()
    {
        var provider = BuildProviderWithOptions(dash => dash.Resource<Post>(resource =>
        {
            resource.Field(x => x.Title).HiddenInList();
            resource.Field(x => x.BlogId).HiddenInEditor();
            resource.Field(x => x.Id).Hidden();
        }));

        var fields = provider.GetResources().Single(x => x.EntityType == typeof(Post)).Fields;
        var title = fields.Single(x => x.Name == nameof(Post.Title));
        var blog = fields.Single(x => x.Name == nameof(Post.BlogId));
        var id = fields.Single(x => x.Name == nameof(Post.Id));

        Assert.True(title.HiddenInList);
        Assert.False(title.HiddenInEditor);
        Assert.True(blog.HiddenInEditor);
        Assert.False(blog.HiddenInList);
        Assert.True(id.Hidden);
    }

    private static IDashboardResourceProvider BuildProviderWithOptions(Action<DashboardBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddDbContext<RelationshipTestContext>(options => options.UseInMemoryDatabase("efcore-dashboard-binary-optin"));
        services.AddEFCoreDashboard<RelationshipTestContext>(configure);
        return services.BuildServiceProvider().GetRequiredService<IDashboardResourceProvider>();
    }

    private static ServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();
        services.AddDbContext<RelationshipTestContext>(options => options.UseInMemoryDatabase("efcore-dashboard-delete-guard"));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<RelationshipTestContext>());
        services.AddSingleton(new DashboardOptions());
        services.AddSingleton<IDashboardFieldProvider, DefaultFieldProvider>();
        services.AddSingleton<IDashboardResourceProvider, EfResourceProvider>();
        services.AddScoped<IDashboardRepository, EfDashboardRepository>();
        return services.BuildServiceProvider();
    }

    private static IDashboardResourceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<RelationshipTestContext>(options => options.UseInMemoryDatabase("efcore-dashboard-relationships"));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<RelationshipTestContext>());
        services.AddSingleton(new DashboardOptions());
        services.AddSingleton<IDashboardFieldProvider, DefaultFieldProvider>();
        services.AddSingleton<IDashboardResourceProvider, EfResourceProvider>();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDashboardResourceProvider>();
    }

    private sealed class RelationshipTestContext(DbContextOptions<RelationshipTestContext> options) : DbContext(options)
    {
        public DbSet<Asset> Assets => Set<Asset>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>().HasMany(x => x.Posts).WithOne(x => x.Blog).HasForeignKey(x => x.BlogId);
            modelBuilder.Entity<Post>().HasMany(x => x.Tags).WithMany(x => x.Posts);
            modelBuilder.Entity<Post>().HasOne(x => x.Profile).WithOne(x => x.Post).HasForeignKey<Profile>(x => x.PostId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Category>().HasAlternateKey(x => x.Code);
            modelBuilder.Entity<Article>().HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryCode)
                .HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class Blog
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Post> Posts { get; set; } = [];
    }

    public sealed class Post
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int? BlogId { get; set; }
        public Blog? Blog { get; set; }
        public List<Tag> Tags { get; set; } = [];
        public Profile? Profile { get; set; }
    }

    public sealed class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Post> Posts { get; set; } = [];
    }

    public sealed class Profile
    {
        public int Id { get; set; }
        public string Bio { get; set; } = string.Empty;
        public int? PostId { get; set; }
        public Post? Post { get; set; }
    }

    public sealed class Asset
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte[]? Cover { get; set; }
    }

    public sealed class Category
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class Article
    {
        public int Id { get; set; }
        public string? CategoryCode { get; set; }
    }
}
