using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using EFCore.Dashboard.Core;

namespace EFCore.Dashboard.EntityFrameworkCore;

internal static class EfQueryHelpers
{
    private static readonly MethodInfo ToListCoreMethod = typeof(EfQueryHelpers).GetMethod(nameof(ToListCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo CountCoreMethod = typeof(EfQueryHelpers).GetMethod(nameof(CountCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static IQueryable ApplySearch(IQueryable query, DashboardResource resource, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var properties = resource.Fields.Where(x => !x.Hidden && !x.HiddenInList && x.PropertyType == typeof(string)).ToArray();
        if (properties.Length == 0) return query;

        var parameter = Expression.Parameter(resource.EntityType, "x");
        Expression? body = null;
        foreach (var field in properties)
        {
            var property = Expression.Property(parameter, field.Name);
            var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
            var loweredProperty = Expression.Call(property, nameof(string.ToLower), Type.EmptyTypes);
            var contains = Expression.Call(loweredProperty, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(search.ToLowerInvariant()));
            var predicate = Expression.AndAlso(notNull, contains);
            body = body is null ? predicate : Expression.OrElse(body, predicate);
        }

        var lambda = Expression.Lambda(body!, parameter);
        var where = Expression.Call(typeof(Queryable), nameof(Queryable.Where), [resource.EntityType], query.Expression, Expression.Quote(lambda));
        return query.Provider.CreateQuery(where);
    }

    public static IQueryable ApplySort(IQueryable query, DashboardResource resource, string? sort, bool descending)
    {
        var field = resource.Fields.FirstOrDefault(x => x.Name.Equals(sort, StringComparison.OrdinalIgnoreCase))
            ?? resource.Key;
        var parameter = Expression.Parameter(resource.EntityType, "x");
        var property = Expression.Property(parameter, field.Name);
        var lambda = Expression.Lambda(property, parameter);
        var method = descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
        var call = Expression.Call(typeof(Queryable), method, [resource.EntityType, property.Type], query.Expression, Expression.Quote(lambda));
        return query.Provider.CreateQuery(call);
    }

    public static IQueryable ApplyPaging(IQueryable query, Type elementType, int page, int pageSize, int maxPageSize = 100)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, maxPageSize);
        var skip = Expression.Call(typeof(Queryable), nameof(Queryable.Skip), [elementType], query.Expression, Expression.Constant((page - 1) * pageSize));
        query = query.Provider.CreateQuery(skip);
        var take = Expression.Call(typeof(Queryable), nameof(Queryable.Take), [elementType], query.Expression, Expression.Constant(pageSize));
        return query.Provider.CreateQuery(take);
    }

    public static IQueryable FilterEqual(IQueryable query, Type elementType, string propertyName, object value)
    {
        var parameter = Expression.Parameter(elementType, "x");
        var property = Expression.Property(parameter, propertyName);
        var constant = Expression.Constant(value);
        Expression right = constant.Type == property.Type ? constant : Expression.Convert(constant, property.Type);
        var predicate = Expression.Equal(property, right);
        var lambda = Expression.Lambda(predicate, parameter);
        var where = Expression.Call(typeof(Queryable), nameof(Queryable.Where), [elementType], query.Expression, Expression.Quote(lambda));
        return query.Provider.CreateQuery(where);
    }

    public static IQueryable ExcludeReferencedPrincipals(
        IQueryable principals,
        DashboardResource principalResource,
        IQueryable dependents,
        DashboardResource dependentResource,
        RelationField relation)
    {
        var principal = Expression.Parameter(principalResource.EntityType, "principal");
        var dependent = Expression.Parameter(dependentResource.EntityType, "dependent");
        Expression principalKey = Expression.Property(principal, relation.PrincipalKeyProperty);
        Expression foreignKey = Expression.Property(dependent, relation.Name);
        if (principalKey.Type != foreignKey.Type)
        {
            if (Nullable.GetUnderlyingType(foreignKey.Type) == principalKey.Type)
                principalKey = Expression.Convert(principalKey, foreignKey.Type);
            else
                foreignKey = Expression.Convert(foreignKey, principalKey.Type);
        }

        var reference = Expression.Lambda(Expression.Equal(foreignKey, principalKey), dependent);
        var any = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Any),
            [dependentResource.EntityType],
            dependents.Expression,
            Expression.Quote(reference));
        var available = Expression.Lambda(Expression.Not(any), principal);
        var where = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [principalResource.EntityType],
            principals.Expression,
            Expression.Quote(available));
        return principals.Provider.CreateQuery(where);
    }

    public static IQueryable FilterIn(IQueryable query, Type elementType, string propertyName, IReadOnlyList<object> values)
    {
        var parameter = Expression.Parameter(elementType, "x");
        var property = Expression.Property(parameter, propertyName);
        var typedValues = Array.CreateInstance(property.Type, values.Count);
        for (var index = 0; index < values.Count; index++)
            typedValues.SetValue(values[index], index);
        var contains = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [property.Type],
            Expression.Constant(typedValues),
            property);
        var lambda = Expression.Lambda(contains, parameter);
        var where = Expression.Call(typeof(Queryable), nameof(Queryable.Where), [elementType], query.Expression, Expression.Quote(lambda));
        return query.Provider.CreateQuery(where);
    }

    public static Task<IReadOnlyList<object>> ToObjectListAsync(IQueryable query, CancellationToken cancellationToken)
        => (Task<IReadOnlyList<object>>)ToListCoreMethod.MakeGenericMethod(query.ElementType).Invoke(null, [query, cancellationToken])!;

    public static Task<int> CountAsync(IQueryable query, CancellationToken cancellationToken)
        => (Task<int>)CountCoreMethod.MakeGenericMethod(query.ElementType).Invoke(null, [query, cancellationToken])!;

    private static async Task<IReadOnlyList<object>> ToListCoreAsync<T>(IQueryable query, CancellationToken cancellationToken)
    {
        var items = await EntityFrameworkQueryableExtensions.ToListAsync((IQueryable<T>)query, cancellationToken);
        return items.Cast<object>().ToArray();
    }

    private static Task<int> CountCoreAsync<T>(IQueryable query, CancellationToken cancellationToken)
        => EntityFrameworkQueryableExtensions.CountAsync((IQueryable<T>)query, cancellationToken);
}
