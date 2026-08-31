using EFCore.Dashboard.Core;
using EFCore.Dashboard.EntityFrameworkCore;
using EFCore.Dashboard.Web;

namespace EFCore.Dashboard.Pages.Admin;

/// <summary>
/// Describes a child-resource grid pre-filtered to the records that reference one principal
/// record through a single-column foreign key.
/// </summary>
public sealed record RelatedRecordLink(
    string ResourceSlug,
    string ResourceLabel,
    string FilterField,
    string FilterValue)
{
    /// <summary>
    /// Finds discoverable one-to-many dependents of <paramref name="resource"/> whose foreign key
    /// references the resource's primary key. Relationships that use an alternate principal key,
    /// composite foreign keys, and many-to-many join navigations cannot be expressed by the grid
    /// filter and are skipped. Links are ordered by child resource label.
    /// </summary>
    public static IReadOnlyList<RelatedRecordLink> CreateMany(
        IDashboardResourceProvider resources,
        IDashboardValueConverter converter,
        DashboardResource resource,
        object entity)
    {
        var key = EfDashboardRepository.GetValue(entity, resource.Key);
        if (key is null) return [];

        var links = new List<RelatedRecordLink>();
        foreach (var relationship in resources.GetRelationships())
        {
            if (relationship.SourceEntityType != resource.EntityType) continue;
            if (relationship.Multiplicity != RelationshipMultiplicity.OneToMany) continue;
            if (relationship.ForeignKeyProperty is null) continue;

            var dependent = resources.Find(relationship.TargetEntityType);
            if (dependent is null) continue;
            var foreignKey = dependent.Fields.OfType<RelationField>().FirstOrDefault(field =>
                field.Name.Equals(relationship.ForeignKeyProperty, StringComparison.OrdinalIgnoreCase));
            if (foreignKey is null) continue;
            if (!foreignKey.PrincipalKeyProperty.Equals(resource.Key.Name, StringComparison.Ordinal)) continue;

            links.Add(new RelatedRecordLink(
                dependent.Slug,
                dependent.Label,
                foreignKey.Name,
                converter.Format(foreignKey, key)));
        }

        links.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.ResourceLabel, right.ResourceLabel));
        return links;
    }
}
