using System.Data.Common;
using System.Text;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;
using EFCore.Dashboard.Pages.Admin;
using EFCore.Dashboard.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class EfDashboardRepositoryRegressionTests
{
    [Fact]
    public async Task Edit_fields_follow_CLR_declaration_order()
    {
        using var database = new TestDatabase();
        var (article, category) = ArticleResources();
        article = article with { Fields = article.Fields.OrderBy(field => field.Name).ToArray() };
        var model = new EditModel(
            new StaticResourceProvider(article, category),
            database.Repository,
            new DashboardValueConverter());

        await model.OnGetAsync(article.Slug, null, CancellationToken.None);

        Assert.Equal(
            [nameof(Article.Id), nameof(Article.Title), nameof(Article.CategoryId), nameof(Article.BackupCategoryId), nameof(Article.Published)],
            model.EditableFields.Select(field => field.Name));
    }

    [Fact]
    public async Task Edit_route_keys_use_the_configured_value_converter()
    {
        using var database = new TestDatabase();
        database.Db.Items.Add(NewItem(1));
        await database.Db.SaveChangesAsync();
        var resource = ItemResource();
        var model = new EditModel(
            new StaticResourceProvider(resource),
            database.Repository,
            new NamedKeyConverter());

        var result = await model.OnGetAsync(resource.Slug, "one", CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(1, Assert.IsType<Item>(model.Entity).Id);
    }

    [Fact]
    public async Task Resource_columns_follow_CLR_declaration_order()
    {
        using var database = new TestDatabase();
        var (article, category) = ArticleResources();
        article = article with { Fields = article.Fields.OrderBy(field => field.Name).ToArray() };
        var model = new ResourceModel(
            new StaticResourceProvider(article, category),
            database.Repository,
            new DashboardValueConverter());

        await model.OnGetAsync(article.Slug);

        Assert.Equal(
            [nameof(Article.Id), nameof(Article.Title), nameof(Article.CategoryId), nameof(Article.BackupCategoryId), nameof(Article.Published)],
            model.Columns.Select(field => field.Name));
    }

    [Fact]
    public async Task Export_honors_a_requested_cap_above_100()
    {
        using var database = new TestDatabase();
        database.Db.Items.AddRange(Enumerable.Range(1, 150).Select(NewItem));
        await database.Db.SaveChangesAsync();

        var exported = await database.Repository.ExportAsync(ItemResource(), new DashboardQuery(), take: 125);

        Assert.Equal(125, exported.Count);
    }

    [Fact]
    public async Task Lookup_uses_its_default_scale_above_100()
    {
        using var database = new TestDatabase();
        database.Db.Items.AddRange(Enumerable.Range(1, 150).Select(NewItem));
        await database.Db.SaveChangesAsync();

        var options = await database.Repository.LookupAsync(ItemResource());

        Assert.Equal(150, options.Count);
        Assert.Contains(options, option => option.Label == "Item 150");
    }

    [Fact]
    public async Task Lookup_filters_by_search_term()
    {
        using var database = new TestDatabase();
        database.Db.Items.AddRange(Enumerable.Range(1, 10).Select(id => new Item { Id = id, Name = $"Widget {id}" }));
        database.Db.Items.AddRange(Enumerable.Range(11, 10).Select(id => new Item { Id = id, Name = $"Item {id}" }));
        await database.Db.SaveChangesAsync();

        var options = await database.Repository.LookupAsync(ItemResource(), search: "widget");

        Assert.Equal(10, options.Count);
        Assert.All(options, option => Assert.Contains("Widget", option.Label));
    }

    [Fact]
    public async Task Query_clamps_a_page_beyond_the_last_page()
    {
        using var database = new TestDatabase();
        database.Db.Items.AddRange(Enumerable.Range(1, 25).Select(NewItem));
        await database.Db.SaveChangesAsync();

        var page = await database.Repository.QueryAsync(
            ItemResource(), new DashboardQuery(Page: 99, PageSize: 10));

        Assert.Equal(3, page.Page);
        Assert.Equal(5, page.Items.Count);
    }

    [Fact]
    public async Task Query_filters_by_an_exact_field_value()
    {
        using var database = new TestDatabase();
        database.Db.Articles.AddRange(
            new Article { Id = 1, Title = "First", CategoryId = 2 },
            new Article { Id = 2, Title = "Second", CategoryId = 2 },
            new Article { Id = 3, Title = "Third", CategoryId = 3 });
        await database.Db.SaveChangesAsync();
        var (article, _) = ArticleResources();

        var page = await database.Repository.QueryAsync(
            article,
            new DashboardQuery(FilterField: nameof(Article.CategoryId), FilterValue: 2));

        Assert.Equal(2, page.Total);
        Assert.All(page.Items.Cast<Article>(), item => Assert.Equal(2, item.CategoryId));
    }

    [Fact]
    public async Task Resource_page_converts_and_applies_a_linked_filter()
    {
        using var database = new TestDatabase();
        database.Db.Articles.AddRange(
            new Article { Id = 1, Title = "First", CategoryId = 2 },
            new Article { Id = 2, Title = "Second", CategoryId = 3 });
        await database.Db.SaveChangesAsync();
        var (article, category) = ArticleResources();
        var model = new ResourceModel(
            new StaticResourceProvider(article, category),
            database.CreateRepository(article, category),
            new DashboardValueConverter())
        {
            FilterField = nameof(Article.CategoryId),
            FilterValue = "2"
        };

        await model.OnGetAsync(article.Slug);

        Assert.Equal(nameof(Article.CategoryId), model.ActiveFilter?.Name);
        Assert.Equal(1, model.Result?.Total);
        Assert.Equal(1, Assert.IsType<Article>(Assert.Single(model.Result!.Items)).Id);
    }

    [Fact]
    public async Task Delete_blockers_report_nullable_foreign_keys_on_SQLite()
    {
        using var database = new TestDatabase();
        database.Db.Parents.Add(new Parent { Id = 7 });
        database.Db.Children.AddRange(
            new Child { Id = 1, ParentId = 7 },
            new Child { Id = 2, ParentId = 7 },
            new Child { Id = 3, ParentId = null });
        await database.Db.SaveChangesAsync();

        var blockers = await database.Repository.GetDeleteBlockersAsync(ParentResource(), 7);

        var blocker = Assert.Single(blockers);
        Assert.Equal(typeof(Child), blocker.EntityType);
        Assert.Equal(nameof(Child.ParentId), blocker.ForeignKeyProperty);
        Assert.Equal(2, blocker.Count);
        Assert.Equal(2, await database.Repository.CountChildrenAsync(ParentResource(), 7));
    }

    [Theory]
    [InlineData(RelationshipDeleteBehavior.Cascade)]
    [InlineData(RelationshipDeleteBehavior.SetNull)]
    public async Task Delete_blockers_ignore_database_managed_relationships(RelationshipDeleteBehavior deleteBehavior)
    {
        using var database = new TestDatabase();
        database.Db.Parents.Add(new Parent { Id = 7 });
        database.Db.Children.Add(new Child { Id = 1, ParentId = 7 });
        await database.Db.SaveChangesAsync();

        var blockers = await database.Repository.GetDeleteBlockersAsync(ParentResource(deleteBehavior), 7);

        Assert.Empty(blockers);
        Assert.Equal(0, await database.Repository.CountChildrenAsync(ParentResource(deleteBehavior), 7));
    }

    [Fact]
    public async Task Delete_blockers_keep_separate_foreign_keys_to_the_same_resource()
    {
        using var database = new TestDatabase();
        database.Db.Articles.AddRange(
            new Article { Id = 1, Title = "Both", CategoryId = 2, BackupCategoryId = 2 },
            new Article { Id = 2, Title = "Primary", CategoryId = 2, BackupCategoryId = 3 });
        await database.Db.SaveChangesAsync();
        var (article, category) = ArticleResources();
        category = category with
        {
            Relationships =
            [
                RestrictRelationship(typeof(Category), typeof(Article), nameof(Article.CategoryId)),
                RestrictRelationship(typeof(Category), typeof(Article), nameof(Article.BackupCategoryId))
            ]
        };

        var blockers = await database.Repository.GetDeleteBlockersAsync(category, 2);

        Assert.Collection(
            blockers.OrderBy(x => x.ForeignKeyProperty),
            blocker =>
            {
                Assert.Equal(nameof(Article.BackupCategoryId), blocker.ForeignKeyProperty);
                Assert.Equal(1, blocker.Count);
            },
            blocker =>
            {
                Assert.Equal(nameof(Article.CategoryId), blocker.ForeignKeyProperty);
                Assert.Equal(2, blocker.Count);
            });
    }

    [Fact]
    public async Task Delete_reports_blocking_entity_labels_and_counts()
    {
        using var database = new TestDatabase();
        database.Db.Parents.Add(new Parent { Id = 7 });
        database.Db.Children.Add(new Child { Id = 1, ParentId = 7 });
        await database.Db.SaveChangesAsync();
        var parent = ParentResource();
        var child = ChildResource();
        var model = new EditModel(
            new StaticResourceProvider(parent, child),
            database.CreateRepository(parent, child),
            new DashboardValueConverter());

        var result = await model.OnPostDeleteAsync(parent.Slug, "7", CancellationToken.None);

        Assert.IsType<PageResult>(result);
        var reference = Assert.Single(model.DeleteReferences);
        Assert.Equal("7", reference.PrincipalLabel);
        Assert.Equal(child.Slug, reference.ResourceSlug);
        Assert.Equal(nameof(Child.ParentId), reference.ForeignKeyProperty);
        Assert.Equal("7", reference.ForeignKeyValue);
        Assert.Equal(1, reference.Count);
    }

    [Fact]
    public async Task Bulk_delete_removes_all_selected_records()
    {
        using var database = new TestDatabase();
        database.Db.Items.AddRange(NewItem(1), NewItem(2), NewItem(3));
        await database.Db.SaveChangesAsync();
        var resource = ItemResource();
        var model = new ResourceModel(
            new StaticResourceProvider(resource),
            database.CreateRepository(resource),
            new DashboardValueConverter());

        var result = await model.OnPostBulkDeleteAsync(resource.Slug, ["1", "3"]);

        Assert.IsType<RedirectToPageResult>(result);
        database.Db.ChangeTracker.Clear();
        var remainingIds = await database.Db.Items.Select(item => item.Id).ToArrayAsync();
        Assert.Equal([2], remainingIds);
    }

    [Fact]
    public async Task Bulk_delete_is_all_or_nothing_when_a_selected_record_is_referenced()
    {
        using var database = new TestDatabase();
        database.Db.Parents.AddRange(new Parent { Id = 7 }, new Parent { Id = 8 });
        database.Db.Children.Add(new Child { Id = 1, ParentId = 7 });
        await database.Db.SaveChangesAsync();
        var parent = ParentResource();
        var child = ChildResource();
        var model = new ResourceModel(
            new StaticResourceProvider(parent, child),
            database.CreateRepository(parent, child),
            new DashboardValueConverter());

        var result = await model.OnPostBulkDeleteAsync(parent.Slug, ["7", "8"]);

        Assert.IsType<PageResult>(result);
        var reference = Assert.Single(model.DeleteReferences);
        Assert.Equal(child.Slug, reference.ResourceSlug);
        Assert.Equal(nameof(Child.ParentId), reference.ForeignKeyProperty);
        database.Db.ChangeTracker.Clear();
        Assert.Equal(2, await database.Db.Parents.CountAsync());
    }

    [Fact]
    public async Task Create_applies_a_manually_assigned_key()
    {
        using var database = new TestDatabase();

        var created = (Item)await database.Repository.CreateAsync(ItemResource(), new Dictionary<string, object?>
        {
            [nameof(Item.Id)] = 42,
            [nameof(Item.Name)] = "Assigned"
        });

        Assert.Equal(42, created.Id);
        Assert.Equal("Assigned", (await database.Db.Items.SingleAsync()).Name);
    }

    [Fact]
    public async Task Update_does_not_mutate_the_key()
    {
        using var database = new TestDatabase();
        database.Db.Items.Add(NewItem(7));
        await database.Db.SaveChangesAsync();

        await database.Repository.UpdateAsync(ItemResource(), 7, new Dictionary<string, object?>
        {
            [nameof(Item.Id)] = 99,
            [nameof(Item.Name)] = "Updated"
        });

        var item = await database.Db.Items.SingleAsync();
        Assert.Equal(7, item.Id);
        Assert.Equal("Updated", item.Name);
    }

    [Fact]
    public async Task Binary_values_round_trip_through_create_and_update()
    {
        using var database = new TestDatabase();
        var payload = new byte[] { 1, 2, 3 };

        var created = (BinaryItem)await database.Repository.CreateAsync(BinaryItemResource(), new Dictionary<string, object?>
        {
            [nameof(BinaryItem.Id)] = 11,
            [nameof(BinaryItem.Payload)] = payload
        });
        Assert.Equal(payload, created.Payload);

        var updated = new byte[] { 9, 8, 7 };
        await database.Repository.UpdateAsync(BinaryItemResource(), 11, new Dictionary<string, object?>
        {
            [nameof(BinaryItem.Payload)] = updated
        });

        var item = await database.Db.BinaryItems.SingleAsync();
        Assert.Equal(updated, item.Payload);
    }

    [Fact]
    public async Task Resource_image_handler_returns_supported_binary_value()
    {
        using var database = new TestDatabase();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        database.Db.BinaryItems.Add(new BinaryItem { Id = 11, Payload = bytes });
        await database.Db.SaveChangesAsync();
        var resource = BinaryItemResource();
        var model = new ResourceModel(
            new StaticResourceProvider(resource),
            database.Repository,
            new DashboardValueConverter());

        var result = await model.OnGetImageAsync(resource.Slug, "11", nameof(BinaryItem.Payload));

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(bytes, file.FileContents);
    }

    [Fact]
    public async Task Query_resolves_relation_display_once_for_duplicate_foreign_keys()
    {
        using var database = new TestDatabase();
        database.Db.Categories.Add(new Category { Id = 2, Code = "tech", Name = "Technology" });
        database.Db.Articles.AddRange(Enumerable.Range(1, 50).Select(id =>
            new Article { Id = id, Title = $"Article {id}", CategoryId = 2 }));
        await database.Db.SaveChangesAsync();
        var (articleResource, categoryResource) = ArticleResources();
        var repository = database.CreateRepository(articleResource, categoryResource);
        database.Commands.Reset();

        var page = await repository.QueryAsync(articleResource, new DashboardQuery(PageSize: 50));

        var categoryField = Assert.IsType<RelationField>(articleResource.Fields.Single(x => x.Name == nameof(Article.CategoryId)));
        Assert.All(page.Items, item =>
        {
            Assert.True(repository.TryGetRelatedValue(item, categoryField, out var display));
            Assert.Equal("Technology", display);
        });
        Assert.Equal(3, database.Commands.ReaderCount);
    }

    [Fact]
    public async Task Relation_resolution_handles_null_missing_and_independent_same_type_foreign_keys()
    {
        using var database = new TestDatabase();
        database.Db.Categories.AddRange(
            new Category { Id = 2, Code = "tech", Name = "Technology" },
            new Category { Id = 3, Code = "science", Name = "Science" });
        database.Db.Articles.AddRange(
            new Article { Id = 1, Title = "Null", CategoryId = null },
            new Article { Id = 2, Title = "Missing", CategoryId = 999 },
            new Article { Id = 3, Title = "Two", CategoryId = 2, BackupCategoryId = 3 });
        await database.Db.SaveChangesAsync();
        var (articleResource, categoryResource) = ArticleResources();
        var repository = database.CreateRepository(articleResource, categoryResource);
        var page = await repository.QueryAsync(articleResource, new DashboardQuery(PageSize: 10));
        var model = new ResourceModel(new StaticResourceProvider(articleResource, categoryResource), repository, new DashboardValueConverter());
        var category = Assert.IsType<RelationField>(articleResource.Fields.Single(x => x.Name == nameof(Article.CategoryId)));
        var backup = Assert.IsType<RelationField>(articleResource.Fields.Single(x => x.Name == nameof(Article.BackupCategoryId)));
        var nullArticle = page.Items.Single(x => ((Article)x).Id == 1);
        var missingArticle = page.Items.Single(x => ((Article)x).Id == 2);
        var twoRelations = page.Items.Single(x => ((Article)x).Id == 3);

        Assert.Equal("—", model.Display(nullArticle, category));
        Assert.Equal("CategoryId: null", model.Tooltip(nullArticle, category));
        Assert.Equal("—", model.Display(missingArticle, category));
        Assert.Equal("CategoryId: 999", model.Tooltip(missingArticle, category));
        Assert.Equal("Technology", model.Display(twoRelations, category));
        Assert.Equal("CategoryId: 2", model.Tooltip(twoRelations, category));
        Assert.Equal("Science", model.Display(twoRelations, backup));
    }

    [Fact]
    public async Task Navigationless_alternate_key_relation_resolves()
    {
        using var database = new TestDatabase();
        database.Db.Categories.Add(new Category { Id = 2, Code = "tech", Name = "Technology" });
        database.Db.AlternateArticles.Add(new AlternateArticle { Id = 1, CategoryCode = "tech" });
        await database.Db.SaveChangesAsync();
        var categoryResource = CategoryResource();
        var key = Key(nameof(AlternateArticle.Id));
        var relation = new RelationField
        {
            Name = nameof(AlternateArticle.CategoryCode),
            Label = "Category",
            PropertyType = typeof(string),
            RelatedEntityType = typeof(Category),
            RelatedResourceName = nameof(Category),
            PrincipalKeyProperty = nameof(Category.Code)
        };
        var articleResource = Resource(typeof(AlternateArticle), key, [key, relation]);
        var repository = database.CreateRepository(articleResource, categoryResource);

        var page = await repository.QueryAsync(articleResource, new DashboardQuery());

        Assert.True(repository.TryGetRelatedValue(Assert.Single(page.Items), relation, out var display));
        Assert.Equal("Technology", display);
    }

    [Fact]
    public async Task Relation_resolution_falls_back_to_key_and_honors_global_filters()
    {
        using var database = new TestDatabase();
        database.Db.Categories.AddRange(
            new Category { Id = 2, Code = "visible", Name = "Visible" },
            new Category { Id = 3, Code = "hidden", Name = "Hidden", Visible = false });
        database.Db.Articles.AddRange(
            new Article { Id = 1, Title = "Visible", CategoryId = 2 },
            new Article { Id = 2, Title = "Hidden", CategoryId = 3 });
        await database.Db.SaveChangesAsync();
        var (articleResource, categoryResource) = ArticleResources();
        categoryResource = categoryResource with { DisplayField = null };
        var repository = database.CreateRepository(articleResource, categoryResource);
        var page = await repository.QueryAsync(articleResource, new DashboardQuery());
        var relation = Assert.IsType<RelationField>(articleResource.Fields.Single(x => x.Name == nameof(Article.CategoryId)));

        Assert.True(repository.TryGetRelatedValue(page.Items.Single(x => ((Article)x).Id == 1), relation, out var display));
        Assert.Equal(2, display);
        Assert.False(repository.TryGetRelatedValue(page.Items.Single(x => ((Article)x).Id == 2), relation, out _));
    }

    [Fact]
    public async Task Csv_export_contains_related_display_and_preserves_non_relation_rendering()
    {
        using var database = new TestDatabase();
        database.Db.Categories.Add(new Category { Id = 2, Code = "tech", Name = "Technology" });
        database.Db.Articles.Add(new Article { Id = 1, Title = "First", CategoryId = 2, Published = true });
        await database.Db.SaveChangesAsync();
        var (articleResource, categoryResource) = ArticleResources();
        var provider = new StaticResourceProvider(articleResource, categoryResource);
        var repository = database.CreateRepository(articleResource, categoryResource);
        var model = new ResourceModel(provider, repository, new DashboardValueConverter()) { Export = true };

        var result = Assert.IsType<FileContentResult>(await model.OnGetAsync(articleResource.Slug));
        var csv = Encoding.UTF8.GetString(result.FileContents);

        Assert.Contains("Id,Title,Category,Backup Category,Published\r\n", csv);
        Assert.Contains("1,First,Technology,—,Yes\r\n", csv);
        Assert.Equal("Yes", model.Display(database.Db.Articles.Local.Single(), articleResource.Fields.Single(x => x.Name == nameof(Article.Published))));
    }

    [Fact]
    public async Task Resource_list_shows_every_field_except_configured_hidden_fields()
    {
        using var database = new TestDatabase();
        var (articleResource, categoryResource) = ArticleResources();
        articleResource = articleResource with
        {
            Fields = articleResource.Fields
                .Select(field => field.Name == nameof(Article.BackupCategoryId) ? field with { HiddenInList = true } : field)
                .ToArray()
        };
        var model = new ResourceModel(
            new StaticResourceProvider(articleResource, categoryResource),
            database.CreateRepository(articleResource, categoryResource),
            new DashboardValueConverter());

        await model.OnGetAsync(articleResource.Slug);

        Assert.Equal(
            [nameof(Article.Id), nameof(Article.Title), nameof(Article.CategoryId), nameof(Article.Published)],
            model.Columns.Select(field => field.Name));
    }

    [Fact]
    public async Task Editor_omits_only_fields_configured_as_hidden_in_editor()
    {
        using var database = new TestDatabase();
        var (articleResource, categoryResource) = ArticleResources();
        articleResource = articleResource with
        {
            Fields = articleResource.Fields.Select(field => field.Name switch
            {
                nameof(Article.Title) => field with { HiddenInList = true },
                nameof(Article.Published) => field with { HiddenInEditor = true },
                _ => field
            }).ToArray()
        };
        var model = new EditModel(
            new StaticResourceProvider(articleResource, categoryResource),
            database.CreateRepository(articleResource, categoryResource),
            new DashboardValueConverter());

        await model.OnGetAsync(articleResource.Slug, null, CancellationToken.None);

        Assert.Contains(model.EditableFields, field => field.Name == nameof(Article.Title));
        Assert.DoesNotContain(model.EditableFields, field => field.Name == nameof(Article.Published));
    }

    [Fact]
    public async Task Large_export_chunks_related_key_lookups()
    {
        using var database = new TestDatabase();
        const int count = 1201;
        database.Db.Categories.AddRange(Enumerable.Range(1, count).Select(id =>
            new Category { Id = id, Code = $"c{id}", Name = $"Category {id}" }));
        database.Db.Articles.AddRange(Enumerable.Range(1, count).Select(id =>
            new Article { Id = id, Title = $"Article {id}", CategoryId = id }));
        await database.Db.SaveChangesAsync();
        var (articleResource, categoryResource) = ArticleResources();
        var repository = database.CreateRepository(articleResource, categoryResource);
        database.Commands.Reset();

        var items = await repository.ExportAsync(articleResource, new DashboardQuery(), take: count);

        Assert.Equal(count, items.Count);
        Assert.Equal(4, database.Commands.ReaderCount);
    }

    [Fact]
    public void Relation_headers_are_not_sortable()
    {
        var (articleResource, categoryResource) = ArticleResources();
        using var database = new TestDatabase();
        var model = new ResourceModel(
            new StaticResourceProvider(articleResource, categoryResource),
            database.CreateRepository(articleResource, categoryResource),
            new DashboardValueConverter());

        Assert.False(model.IsSortable(articleResource.Fields.Single(x => x.Name == nameof(Article.CategoryId))));
        Assert.True(model.IsSortable(articleResource.Fields.Single(x => x.Name == nameof(Article.Title))));
    }

    private static Item NewItem(int id) => new() { Id = id, Name = $"Item {id}" };

    private sealed class NamedKeyConverter : IDashboardValueConverter
    {
        private readonly DashboardValueConverter _inner = new();

        public bool TryConvert(DashboardField field, string? value, out object? result, out string? error)
        {
            if (field.IsKey && value == "one")
            {
                result = 1;
                error = null;
                return true;
            }

            return _inner.TryConvert(field, value, out result, out error);
        }

        public string Format(DashboardField field, object? value) => _inner.Format(field, value);
    }

    private static DashboardResource ItemResource()
    {
        var key = Key(nameof(Item.Id));
        var name = new TextField { Name = nameof(Item.Name), Label = "Name", PropertyType = typeof(string) };
        return Resource(typeof(Item), key, [key, name], name);
    }

    private static DashboardResource ParentResource(
        RelationshipDeleteBehavior deleteBehavior = RelationshipDeleteBehavior.Restrict)
    {
        var key = Key(nameof(Parent.Id));
        return Resource(typeof(Parent), key, [key]) with
        {
            Relationships =
            [
                new DashboardRelationship
                {
                    SourceEntityType = typeof(Parent),
                    TargetEntityType = typeof(Child),
                    Multiplicity = RelationshipMultiplicity.OneToMany,
                    Required = false,
                    DeleteBehavior = deleteBehavior,
                    ForeignKeyProperty = nameof(Child.ParentId)
                }
            ]
        };
    }

    private static DashboardRelationship RestrictRelationship(Type source, Type target, string foreignKey) => new()
    {
        SourceEntityType = source,
        TargetEntityType = target,
        Multiplicity = RelationshipMultiplicity.OneToMany,
        Required = false,
        DeleteBehavior = RelationshipDeleteBehavior.Restrict,
        ForeignKeyProperty = foreignKey
    };

    private static DashboardResource ChildResource()
    {
        var key = Key(nameof(Child.Id));
        var parent = new RelationField
        {
            Name = nameof(Child.ParentId),
            Label = "Parent",
            PropertyType = typeof(int?),
            RelatedEntityType = typeof(Parent),
            RelatedResourceName = nameof(Parent),
            PrincipalKeyProperty = nameof(Parent.Id)
        };
        return Resource(typeof(Child), key, [key, parent]);
    }

    private static DashboardResource Resource(
        Type type,
        DashboardField key,
        IReadOnlyList<DashboardField> fields,
        DashboardField? display = null) => new()
        {
            Name = type.Name,
            Slug = type.Name.ToLowerInvariant(),
            Label = type.Name,
            EntityType = type,
            Key = key,
            Fields = fields,
            DisplayField = display
        };

    private static NumberField Key(string name) =>
        new() { Name = name, Label = name, PropertyType = typeof(int), IsKey = true };

    private static DashboardResource BinaryItemResource()
    {
        var key = Key(nameof(BinaryItem.Id));
        var payload = new BinaryField { Name = nameof(BinaryItem.Payload), Label = "Payload", PropertyType = typeof(byte[]) };
        return Resource(typeof(BinaryItem), key, [key, payload]);
    }

    private static (DashboardResource Article, DashboardResource Category) ArticleResources()
    {
        var key = Key(nameof(Article.Id));
        var title = new TextField { Name = nameof(Article.Title), Label = "Title", PropertyType = typeof(string) };
        var category = Relation(nameof(Article.CategoryId), "Category", typeof(int?));
        var backup = Relation(nameof(Article.BackupCategoryId), "Backup Category", typeof(int?));
        var published = new BooleanField { Name = nameof(Article.Published), Label = "Published", PropertyType = typeof(bool) };
        return (Resource(typeof(Article), key, [key, title, category, backup, published], title), CategoryResource());
    }

    private static DashboardResource CategoryResource()
    {
        var key = Key(nameof(Category.Id));
        var code = new TextField { Name = nameof(Category.Code), Label = "Code", PropertyType = typeof(string) };
        var name = new TextField { Name = nameof(Category.Name), Label = "Name", PropertyType = typeof(string) };
        return Resource(typeof(Category), key, [key, code, name], name);
    }

    private static RelationField Relation(string name, string label, Type propertyType) => new()
    {
        Name = name,
        Label = label,
        PropertyType = propertyType,
        RelatedEntityType = typeof(Category),
        RelatedResourceName = nameof(Category),
        PrincipalKeyProperty = nameof(Category.Id)
    };

    private sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=False");

        public TestDatabase()
        {
            _connection.Open();
            Commands = new CommandCounter();
            Db = new TestContext(new DbContextOptionsBuilder<TestContext>()
                .UseSqlite(_connection)
                .AddInterceptors(Commands)
                .Options);
            Db.Database.EnsureCreated();
            Repository = new EfDashboardRepository(Db);
        }

        public TestContext Db { get; }
        public EfDashboardRepository Repository { get; }
        public CommandCounter Commands { get; }

        public EfDashboardRepository CreateRepository(params DashboardResource[] resources)
            => new(Db, new StaticResourceProvider(resources));

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
        public DbSet<BinaryItem> BinaryItems => Set<BinaryItem>();
        public DbSet<Parent> Parents => Set<Parent>();
        public DbSet<Child> Children => Set<Child>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Article> Articles => Set<Article>();
        public DbSet<AlternateArticle> AlternateArticles => Set<AlternateArticle>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<BinaryItem>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<Parent>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<Child>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<Parent>().HasMany(x => x.Children).WithOne().HasForeignKey(x => x.ParentId);
            modelBuilder.Entity<Category>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<Category>().HasAlternateKey(x => x.Code);
            modelBuilder.Entity<Category>().HasQueryFilter(x => x.Visible);
            modelBuilder.Entity<Article>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<Article>().HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId);
            modelBuilder.Entity<Article>().HasOne<Category>().WithMany().HasForeignKey(x => x.BackupCategoryId);
            modelBuilder.Entity<AlternateArticle>().Property(x => x.Id).ValueGeneratedNever();
            modelBuilder.Entity<AlternateArticle>().HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryCode)
                .HasPrincipalKey(x => x.Code);
        }
    }

    private sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class BinaryItem
    {
        public int Id { get; set; }
        public byte[]? Payload { get; set; }
    }

    private sealed class Parent
    {
        public int Id { get; set; }
        public List<Child> Children { get; set; } = [];
    }

    private sealed class Child
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
    }

    private sealed class Category
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
    }

    private sealed class Article
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public int? BackupCategoryId { get; set; }
        public bool Published { get; set; }
    }

    private sealed class AlternateArticle
    {
        public int Id { get; set; }
        public string? CategoryCode { get; set; }
    }

    private sealed class StaticResourceProvider(params DashboardResource[] resources) : IDashboardResourceProvider
    {
        public IReadOnlyList<DashboardResource> GetResources() => resources;
        public IReadOnlyList<DashboardRelationship> GetRelationships() => [];
        public DashboardResource? Find(string nameOrSlug) => resources.FirstOrDefault(x =>
            x.Name.Equals(nameOrSlug, StringComparison.OrdinalIgnoreCase) ||
            x.Slug.Equals(nameOrSlug, StringComparison.OrdinalIgnoreCase));
        public DashboardResource? Find(Type entityType) => resources.FirstOrDefault(x => x.EntityType == entityType);
    }

    public sealed class CommandCounter : DbCommandInterceptor
    {
        public int ReaderCount { get; private set; }

        public void Reset() => ReaderCount = 0;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCount++;
            return ValueTask.FromResult(result);
        }
    }
}
