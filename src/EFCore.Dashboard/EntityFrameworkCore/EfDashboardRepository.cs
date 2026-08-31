using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using EFCore.Dashboard.Core;

namespace EFCore.Dashboard.EntityFrameworkCore;

public sealed class EfDashboardRepository(DbContext db, IDashboardResourceProvider? resources = null) : IDashboardRepository
{
    private const int RelatedKeyChunkSize = 500;
    private static readonly MethodInfo SetMethod = typeof(DbContext).GetMethods()
        .Single(x => x.Name == nameof(DbContext.Set) && x.IsGenericMethod && x.GetParameters().Length == 0);
    private static readonly MethodInfo AsNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions).GetMethods()
        .Single(x => x.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) &&
            x.IsGenericMethod && x.GetParameters().Length == 1);
    private readonly ConditionalWeakTable<object, Dictionary<string, object?>> _relatedValues = new();

    public async Task<DashboardPage> QueryAsync(DashboardResource resource, DashboardQuery query, CancellationToken cancellationToken = default)
    {
        var source = GetQuery(resource.EntityType);
        source = ApplyFilter(source, resource, query);
        source = EfQueryHelpers.ApplySearch(source, resource, query.Search);
        var total = await EfQueryHelpers.CountAsync(source, cancellationToken);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        source = EfQueryHelpers.ApplySort(source, resource, query.Sort, query.Descending);
        source = EfQueryHelpers.ApplyPaging(source, resource.EntityType, page, pageSize);
        var items = await EfQueryHelpers.ToObjectListAsync(source, cancellationToken);
        await ResolveRelatedValuesAsync(resource, query, items, cancellationToken);
        return new DashboardPage(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<object>> ExportAsync(DashboardResource resource, DashboardQuery query, int take = 1000, CancellationToken cancellationToken = default)
    {
        var source = GetQuery(resource.EntityType);
        source = ApplyFilter(source, resource, query);
        source = EfQueryHelpers.ApplySearch(source, resource, query.Search);
        source = EfQueryHelpers.ApplySort(source, resource, query.Sort, query.Descending);
        source = EfQueryHelpers.ApplyPaging(source, resource.EntityType, 1, take, 100_000);
        var items = await EfQueryHelpers.ToObjectListAsync(source, cancellationToken);
        await ResolveRelatedValuesAsync(resource, query, items, cancellationToken);
        return items;
    }

    public async Task<object?> FindAsync(DashboardResource resource, object key, CancellationToken cancellationToken = default)
        => await db.FindAsync(resource.EntityType, [key], cancellationToken);

    public async Task<object> CreateAsync(DashboardResource resource, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        var entity = Activator.CreateInstance(resource.EntityType)
            ?? throw new InvalidOperationException($"Could not create {resource.EntityType.Name}. A public parameterless constructor is required.");
        ApplyValues(resource, entity, values, includeKey: true);
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(DashboardResource resource, object key, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(resource, key, cancellationToken)
            ?? throw new KeyNotFoundException($"{resource.Name} '{key}' was not found.");
        ApplyValues(resource, entity, values, includeKey: false);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DashboardResource resource, object key, CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(resource, key, cancellationToken);
        if (entity is null) return;
        db.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteManyAsync(
        DashboardResource resource,
        IReadOnlyCollection<object> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0) return;
        var entities = new List<object>(keys.Count);
        foreach (var key in keys.Distinct())
        {
            var entity = await FindAsync(resource, key, cancellationToken);
            if (entity is not null) entities.Add(entity);
        }
        if (entities.Count == 0) return;
        db.RemoveRange(entities);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountAsync(DashboardResource resource, CancellationToken cancellationToken = default)
        => EfQueryHelpers.CountAsync(GetQuery(resource.EntityType), cancellationToken);

    public async Task<int> CountChildrenAsync(DashboardResource resource, object key, CancellationToken cancellationToken = default)
        => (await GetDeleteBlockersAsync(resource, key, cancellationToken)).Sum(x => x.Count);

    public async Task<IReadOnlyList<DashboardDeleteReference>> GetDeleteBlockersAsync(
        DashboardResource resource,
        object key,
        CancellationToken cancellationToken = default)
    {
        var relationships = (resource.Relationships ?? []).Where(x =>
            x.SourceEntityType == resource.EntityType &&
            x.ForeignKeyProperty is not null &&
            x.DeleteBehavior == RelationshipDeleteBehavior.Restrict).ToArray();
        if (relationships.Length == 0) return [];
        var references = new List<DashboardDeleteReference>();
        foreach (var relationship in relationships)
        {
            var query = EfQueryHelpers.FilterEqual(
                GetQuery(relationship.TargetEntityType), relationship.TargetEntityType, relationship.ForeignKeyProperty!, key);
            var count = await EfQueryHelpers.CountAsync(query, cancellationToken);
            if (count > 0)
                references.Add(new DashboardDeleteReference(
                    relationship.TargetEntityType,
                    relationship.ForeignKeyProperty!,
                    count));
        }
        return references;
    }

    public async Task<IReadOnlyList<DashboardLookupOption>> LookupAsync(
        DashboardResource resource,
        string? search = null,
        int take = 250,
        DashboardResource? dependentResource = null,
        RelationField? relation = null,
        CancellationToken cancellationToken = default)
    {
        var query = GetQuery(resource.EntityType);
        if (relation?.IsUnique == true && dependentResource is not null)
            query = EfQueryHelpers.ExcludeReferencedPrincipals(
                query, resource, GetQuery(dependentResource.EntityType), dependentResource, relation);
        if (!string.IsNullOrWhiteSpace(search))
            query = EfQueryHelpers.ApplySearch(query, resource, search);
        query = EfQueryHelpers.ApplySort(query, resource, resource.DisplayField?.Name, false);
        query = EfQueryHelpers.ApplyPaging(query, resource.EntityType, 1, take, 100_000);
        var items = await EfQueryHelpers.ToObjectListAsync(query, cancellationToken);
        return items.Select(item => new DashboardLookupOption(
            Convert.ToString(GetValue(item, resource.Key), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(GetValue(item, resource.DisplayField ?? resource.Key), System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty)).ToArray();
    }

    public bool TryGetRelatedValue(object item, RelationField field, out object? value)
    {
        if (_relatedValues.TryGetValue(item, out var values) && values.TryGetValue(field.Name, out value))
            return true;
        value = null;
        return false;
    }

    private IQueryable GetQuery(Type entityType)
    {
        var query = (IQueryable)SetMethod.MakeGenericMethod(entityType).Invoke(db, null)!;
        return (IQueryable)AsNoTrackingMethod.MakeGenericMethod(entityType).Invoke(null, [query])!;
    }

    private static IQueryable ApplyFilter(IQueryable query, DashboardResource resource, DashboardQuery request)
    {
        if (request.FilterValue is null || string.IsNullOrWhiteSpace(request.FilterField)) return query;
        var field = resource.Fields.FirstOrDefault(x =>
            x.Name.Equals(request.FilterField, StringComparison.OrdinalIgnoreCase));
        return field is null
            ? query
            : EfQueryHelpers.FilterEqual(query, resource.EntityType, field.Name, request.FilterValue);
    }

    private async Task ResolveRelatedValuesAsync(
        DashboardResource resource,
        DashboardQuery query,
        IReadOnlyList<object> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 || resources is null) return;
        var visibleNames = query.VisibleFields is { Count: > 0 }
            ? query.VisibleFields.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : resource.Fields.Where(x => !x.Hidden && !x.HiddenInList).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relationFields = resource.Fields.OfType<RelationField>()
            .Where(x => visibleNames.Contains(x.Name))
            .ToArray();

        foreach (var field in relationFields)
        {
            var relatedResource = resources.Find(field.RelatedEntityType);
            if (relatedResource is null) continue;
            var displayField = relatedResource.DisplayField ?? relatedResource.Key;
            var keys = items.Select(item => GetValue(item, field))
                .Where(value => value is not null)
                .Cast<object>()
                .Distinct()
                .ToArray();
            if (keys.Length == 0) continue;

            var displaysByKey = new Dictionary<object, object?>();
            foreach (var chunk in keys.Chunk(RelatedKeyChunkSize))
            {
                var principalQuery = EfQueryHelpers.FilterIn(
                    GetQuery(field.RelatedEntityType),
                    field.RelatedEntityType,
                    field.PrincipalKeyProperty,
                    chunk);
                var principals = await EfQueryHelpers.ToObjectListAsync(principalQuery, cancellationToken);
                foreach (var principal in principals)
                {
                    var principalKey = principal.GetType().GetProperty(field.PrincipalKeyProperty)?.GetValue(principal);
                    if (principalKey is not null)
                        displaysByKey[principalKey] = GetValue(principal, displayField);
                }
            }

            foreach (var item in items)
            {
                var foreignKey = GetValue(item, field);
                if (foreignKey is not null && displaysByKey.TryGetValue(foreignKey, out var display))
                    _relatedValues.GetOrCreateValue(item)[field.Name] = display;
            }
        }
    }

    private static void ApplyValues(
        DashboardResource resource,
        object entity,
        IReadOnlyDictionary<string, object?> values,
        bool includeKey)
    {
        foreach (var field in resource.Fields.Where(x => !x.ReadOnly && (includeKey || !x.IsKey)))
        {
            if (!values.TryGetValue(field.Name, out var value)) continue;
            var property = resource.EntityType.GetProperty(field.Name, BindingFlags.Public | BindingFlags.Instance);
            property?.SetValue(entity, value);
        }
    }

    public static object? GetValue(object entity, DashboardField field)
        => entity.GetType().GetProperty(field.Name)?.GetValue(entity);
}
