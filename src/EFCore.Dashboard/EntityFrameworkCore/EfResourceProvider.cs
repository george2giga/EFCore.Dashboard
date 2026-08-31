using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.Extensibility;

namespace EFCore.Dashboard.EntityFrameworkCore;

public sealed class EfResourceProvider : IDashboardResourceProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DashboardOptions _options;
    private readonly IReadOnlyList<IDashboardFieldProvider> _fieldProviders;
    private IReadOnlyList<DashboardResource>? _resources;
    private IReadOnlyList<DashboardRelationship>? _relationships;
    private readonly object _gate = new();

    public EfResourceProvider(
        IServiceScopeFactory scopeFactory,
        DashboardOptions options,
        IEnumerable<IDashboardFieldProvider> fieldProviders)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _fieldProviders = fieldProviders.OrderBy(x => x.Order).ToArray();
    }

    public IReadOnlyList<DashboardResource> GetResources()
    {
        EnsureBuilt();
        return _resources!;
    }

    public IReadOnlyList<DashboardRelationship> GetRelationships()
    {
        EnsureBuilt();
        return _relationships!;
    }

    public DashboardResource? Find(string nameOrSlug) => GetResources().FirstOrDefault(x =>
        x.Name.Equals(nameOrSlug, StringComparison.OrdinalIgnoreCase) ||
        x.Slug.Equals(nameOrSlug, StringComparison.OrdinalIgnoreCase));

    public DashboardResource? Find(Type entityType) => GetResources().FirstOrDefault(x => x.EntityType == entityType);

    private void EnsureBuilt()
    {
        if (_resources is not null && _relationships is not null) return;
        lock (_gate)
        {
            if (_resources is not null && _relationships is not null) return;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var entities = GetEntities(db.Model);
            var resourceNames = entities.ToDictionary(x => x.ClrType, x => x.ClrType.Name);
            var built = entities.Select(entity => CreateResource(entity, resourceNames)).ToArray();
            var duplicate = built
                .GroupBy(resource => resource.Slug, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
                throw new InvalidOperationException($"Multiple entity types map to the dashboard slug '{duplicate.Key}'. Configure unique CLR type names.");
            _relationships = BuildRelationships(entities, resourceNames).ToArray();
            _resources = built
                .Select(resource => resource with { Relationships = _relationships!.Where(x => x.SourceEntityType == resource.EntityType && x.ForeignKeyProperty is not null).ToArray() })
                .OrderBy(x => x.Label)
                .ToArray();
        }
    }

    private IEntityType[] GetEntities(IModel model) => model.GetEntityTypes()
        .Where(x => !x.IsOwned() && !x.HasSharedClrType && x.FindPrimaryKey() is not null && x.ClrType is not null)
        .Where(x => !_options.Resources.TryGetValue(x.ClrType, out var o) || !o.Excluded)
        .ToArray();

    private DashboardResource CreateResource(IEntityType entity, IReadOnlyDictionary<Type, string> resourceNames)
    {
        _options.Resources.TryGetValue(entity.ClrType, out var resourceOptions);
        var primaryKey = entity.FindPrimaryKey()!;
        if (primaryKey.Properties.Count != 1)
            throw new NotSupportedException($"EFCore.Dashboard v0.1 only supports single-column keys. '{entity.ClrType.Name}' has a composite key.");

        var fields = entity.GetProperties().Where(property => property.PropertyInfo is not null).Select(property =>
            CreateField(property, primaryKey.Properties[0] == property, resourceOptions, resourceNames))
            .Where(field => field is not null)
            .Cast<DashboardField>()
            .ToArray();
        var key = fields.Single(x => x.IsKey);

        DashboardField? display = null;
        if (!string.IsNullOrWhiteSpace(resourceOptions?.DisplayProperty))
            display = fields.FirstOrDefault(x => x.Name.Equals(resourceOptions.DisplayProperty, StringComparison.OrdinalIgnoreCase));
        display ??= fields.FirstOrDefault(x => !x.IsKey && x.PropertyType == typeof(string) &&
            (x.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) || x.Name.Equals("Title", StringComparison.OrdinalIgnoreCase)));
        display ??= fields.FirstOrDefault(x => !x.IsKey && x.PropertyType == typeof(string));
        display ??= key;

        var typeName = entity.ClrType.Name;
        return new DashboardResource
        {
            Name = typeName,
            Slug = DashboardNaming.SlugFor(typeName),
            Label = resourceOptions?.Label ?? DashboardNaming.Humanize(DashboardNaming.Pluralize(typeName)),
            EntityType = entity.ClrType,
            Fields = fields,
            Key = key,
            DisplayField = display
        };
    }

    private DashboardField? CreateField(
        IProperty property,
        bool isKey,
        ResourceOptions? resourceOptions,
        IReadOnlyDictionary<Type, string> resourceNames)
    {
        var fieldOptions = resourceOptions is not null && resourceOptions.Fields.TryGetValue(property.Name, out var options)
            ? options
            : null;
        var required = !property.IsNullable;
        var readOnly = property.ValueGenerated != ValueGenerated.Never;

        var fk = property.GetContainingForeignKeys().FirstOrDefault(x => x.Properties.Count == 1);
        var defaultLabel = fk is not null && property.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            ? DashboardNaming.Humanize(property.Name[..^2])
            : DashboardNaming.Humanize(property.Name);
        var context = new DashboardFieldContext(
            fieldOptions?.Label ?? defaultLabel,
            required,
            fieldOptions?.ReadOnly ?? readOnly,
            fieldOptions?.Hidden ?? false,
            isKey,
            property.GetMaxLength(),
            fieldOptions?.Editor)
        {
            HiddenInList = fieldOptions?.HiddenInList ?? false,
            HiddenInEditor = fieldOptions?.HiddenInEditor ?? false
        };

        if (fk is not null && resourceNames.TryGetValue(fk.PrincipalEntityType.ClrType, out var relatedName))
        {
            return new RelationField
            {
                Name = property.Name,
                Label = context.Label,
                PropertyType = property.ClrType,
                Required = context.Required,
                ReadOnly = context.ReadOnly,
                Hidden = context.Hidden,
                HiddenInList = context.HiddenInList,
                HiddenInEditor = context.HiddenInEditor,
                IsKey = context.IsKey,
                MaxLength = context.MaxLength,
                Editor = context.Editor,
                RelatedEntityType = fk.PrincipalEntityType.ClrType,
                RelatedResourceName = relatedName,
                PrincipalKeyProperty = fk.PrincipalKey.Properties.Single().Name,
                IsUnique = fk.IsUnique
            };
        }

        // byte[] properties are opt-in only: they become a BinaryField when configured
        // with Editor(DashboardEditors.Image) and are otherwise omitted from lists, forms, search, and
        // the data-model diagram so sensitive binary columns never surface by accident.
        if (!isKey && property.ClrType == typeof(byte[]) &&
            context.Editor != DashboardEditors.Image)
        {
            return null;
        }

        var provider = _fieldProviders.First(x => x.CanHandle(property));
        var field = provider.Create(property, context);
        return field with { Name = property.Name };
    }

    private IEnumerable<DashboardRelationship> BuildRelationships(
        IEntityType[] entities,
        IReadOnlyDictionary<Type, string> resourceNames)
    {
        foreach (var entity in entities)
        {
            foreach (var fk in entity.GetForeignKeys())
            {
                if (!resourceNames.ContainsKey(fk.PrincipalEntityType.ClrType) ||
                    !resourceNames.ContainsKey(fk.DeclaringEntityType.ClrType))
                {
                    continue;
                }

                yield return new DashboardRelationship
                {
                    SourceEntityType = fk.PrincipalEntityType.ClrType,
                    TargetEntityType = fk.DeclaringEntityType.ClrType,
                    Multiplicity = fk.IsUnique ? RelationshipMultiplicity.OneToOne : RelationshipMultiplicity.OneToMany,
                    Required = fk.IsRequired,
                    DeleteBehavior = fk.DeleteBehavior switch
                    {
                        DeleteBehavior.Cascade => RelationshipDeleteBehavior.Cascade,
                        DeleteBehavior.SetNull => RelationshipDeleteBehavior.SetNull,
                        _ => RelationshipDeleteBehavior.Restrict
                    },
                    NavigationName = fk.PrincipalToDependent?.Name,
                    InverseNavigationName = fk.DependentToPrincipal?.Name,
                    ForeignKeyProperty = fk.Properties.Count == 1 ? fk.Properties[0].Name : null
                };
            }
        }

        var seenManyToMany = new HashSet<(string, string)>();
        foreach (var entity in entities)
        {
            foreach (var skip in entity.GetSkipNavigations())
            {
                var left = skip.DeclaringEntityType.ClrType;
                var right = skip.TargetEntityType.ClrType;
                if (!resourceNames.ContainsKey(left) || !resourceNames.ContainsKey(right))
                    continue;

                var navigation = $"{left.FullName}.{skip.Name}";
                var inverseNavigation = $"{right.FullName}.{skip.Inverse?.Name}";
                var key = string.CompareOrdinal(navigation, inverseNavigation) <= 0
                    ? (navigation, inverseNavigation)
                    : (inverseNavigation, navigation);
                if (!seenManyToMany.Add(key))
                    continue;

                yield return new DashboardRelationship
                {
                    SourceEntityType = left,
                    TargetEntityType = right,
                    Multiplicity = RelationshipMultiplicity.ManyToMany,
                    Required = false,
                    NavigationName = skip.Name,
                    InverseNavigationName = skip.Inverse?.Name,
                    JoinEntityTypeName = skip.ForeignKey.DeclaringEntityType.Name
                };
            }
        }
    }
}
