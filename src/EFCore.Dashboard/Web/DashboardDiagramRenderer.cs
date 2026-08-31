using System.Net;
using System.Text;
using EFCore.Dashboard.Core;

namespace EFCore.Dashboard.Web;

/// <summary>
/// Renders the data model as a dependency-free inline SVG document built from dashboard
/// resource and relationship metadata. All metadata is HTML-escaped and colors follow the
/// dashboard theme through CSS variables, so the diagram adapts to light and dark mode
/// without any client-side rendering.
/// </summary>
public sealed class DashboardDiagramRenderer
{
    private const int TableWidth = 240;
    private const int HeaderHeight = 36;
    private const int RowHeight = 24;
    private const int TableGapX = 150;
    private const int TableGapY = 64;
    private const int Padding = 30;
    private const int PortInset = 10;
    private const int RouteLaneGap = 16;

    /// <summary>
    /// Renders the model as a standalone SVG element, one table per resource. Resources
    /// are grouped into columns by dependency depth so principals appear to the left of
    /// their dependents.
    /// </summary>
    /// <param name="resources">Discovered dashboard resources, one table per resource.</param>
    /// <param name="relationships">Relationships between discovered resources.</param>
    /// <param name="slugToUrl">Optional URL builder per resource slug; when a URL is returned the
    /// table header links to it.</param>
    /// <returns>SVG markup ready to be placed inline via <c>Html.Raw</c>.</returns>
    public string Render(
        IReadOnlyList<DashboardResource> resources,
        IReadOnlyList<DashboardRelationship> relationships,
        Func<string, string>? slugToUrl = null)
    {
        if (resources.Count == 0)
            return EmptySvg();

        var tables = resources.Select(BuildTable).ToList();
        var relations = BuildRelations(tables, relationships);

        var depths = AssignDepths(tables, relations);
        Layout(tables, relations, depths);
        AssignRoutes(relations);

        var width = Math.Max(
            tables.Max(table => table.X + TableWidth),
            relations.Count == 0 ? 0 : relations.Max(relation => relation.LaneX)) + Padding;
        var height = tables.Max(table => table.Y + table.Height) + Padding;

        var svg = new StringBuilder();

        svg.AppendLine($"""
            <svg class="ef-dashboard-diagram"
                 xmlns="http://www.w3.org/2000/svg"
                 viewBox="0 0 {width} {height}"
                 role="img"
                 aria-label="Database schema diagram">
            """);

        svg.AppendLine("""
                <defs>
                    <marker id="ef-diagram-arrow"
                            markerWidth="8" markerHeight="8"
                            refX="7" refY="4"
                            orient="auto">
                        <path d="M 0 0 L 8 4 L 0 8 z" class="ef-diagram-arrow" />
                    </marker>
                </defs>
            """);

        RenderRelations(svg, relations);

        foreach (var table in tables)
            RenderTable(svg, table, slugToUrl?.Invoke(table.Slug));

        svg.AppendLine("</svg>");

        return svg.ToString();
    }

    private static DiagramTable BuildTable(DashboardResource resource)
    {
        var columns = resource.Fields
            .Where(field => !field.Hidden)
            .Select(field => new DiagramColumn(
                field.Name,
                TypeName(field.PropertyType),
                field.IsKey,
                field is RelationField))
            .ToList();

        return new DiagramTable(resource, columns);
    }

    private static List<DiagramRelation> BuildRelations(
        IReadOnlyList<DiagramTable> tables,
        IReadOnlyList<DashboardRelationship> relationships)
    {
        var lookup = tables.ToDictionary(table => table.Resource.EntityType);
        var result = new List<DiagramRelation>();

        foreach (var relationship in relationships)
        {
            if (!lookup.TryGetValue(relationship.TargetEntityType, out var dependent) ||
                !lookup.TryGetValue(relationship.SourceEntityType, out var principal))
            {
                continue;
            }

            result.Add(new DiagramRelation(
                dependent,
                principal,
                Cardinality(relationship.Multiplicity, relationship.Required, source: false),
                Cardinality(relationship.Multiplicity, relationship.Required, source: true),
                relationship.Multiplicity,
                RelationshipLabel(relationship)));
        }

        return result;
    }

    private static string RelationshipLabel(DashboardRelationship relationship) =>
        string.IsNullOrWhiteSpace(relationship.NavigationName)
            ? relationship.TargetEntityType.Name
            : relationship.NavigationName;

    private static Dictionary<Type, int> AssignDepths(
        IReadOnlyList<DiagramTable> tables,
        IReadOnlyList<DiagramRelation> relations)
    {
        var result = new Dictionary<Type, int>();
        var hierarchical = relations
            .Where(relation => relation.Multiplicity != RelationshipMultiplicity.ManyToMany && relation.From != relation.To)
            .ToList();

        int GetDepth(Type type, HashSet<Type> visiting)
        {
            if (result.TryGetValue(type, out var existing))
                return existing;

            if (!visiting.Add(type))
                return 0;

            var parents = hierarchical
                .Where(relation => relation.From.Resource.EntityType == type)
                .Select(relation => relation.To.Resource.EntityType)
                .Distinct()
                .ToList();

            var depth = parents.Count == 0
                ? 0
                : parents.Max(p => GetDepth(p, visiting)) + 1;

            visiting.Remove(type);

            return result[type] = depth;
        }

        foreach (var table in tables)
            GetDepth(table.Resource.EntityType, []);

        // Peer-only resources have no natural hierarchy. Put them immediately after
        // the closest anchored peer instead of letting many-to-many edges stretch
        // across the full graph or create artificial dependency chains.
        var anchored = hierarchical
            .SelectMany(relation => new[] { relation.From.Resource.EntityType, relation.To.Resource.EntityType })
            .ToHashSet();
        var peerDepths = new Dictionary<Type, int>();
        foreach (var relation in relations.Where(relation => relation.Multiplicity == RelationshipMultiplicity.ManyToMany))
        {
            var fromType = relation.From.Resource.EntityType;
            var toType = relation.To.Resource.EntityType;
            if (anchored.Contains(fromType) == anchored.Contains(toType))
                continue;

            var peer = anchored.Contains(fromType) ? toType : fromType;
            var anchor = anchored.Contains(fromType) ? fromType : toType;
            var candidate = result[anchor] + 1;
            if (!peerDepths.TryGetValue(peer, out var existing) || candidate < existing)
                peerDepths[peer] = candidate;
        }

        foreach (var (type, depth) in peerDepths)
            result[type] = depth;

        return result;
    }

    private static void Layout(
        IReadOnlyList<DiagramTable> tables,
        IReadOnlyList<DiagramRelation> relations,
        IReadOnlyDictionary<Type, int> depths)
    {
        var columns = tables
            .GroupBy(table => depths[table.Resource.EntityType])
            .OrderBy(group => group.Key)
            .Select(group => group.OrderBy(table => table.Name).ToList())
            .ToList();

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            foreach (var table in columns[columnIndex])
                table.Column = columnIndex;

        var neighbors = tables.ToDictionary(table => table, _ => new List<DiagramTable>());
        foreach (var relation in relations.Where(relation => relation.From != relation.To))
        {
            neighbors[relation.From].Add(relation.To);
            neighbors[relation.To].Add(relation.From);
        }

        // Alternating barycentric sweeps are a small version of the ordering stage
        // used by layered graph renderers. They remove most avoidable crossings while
        // preserving deterministic name ordering for unconnected tables.
        for (var pass = 0; pass < 4; pass++)
        {
            for (var columnIndex = 1; columnIndex < columns.Count; columnIndex++)
                ReorderColumn(columns, columnIndex, neighbors, towardLeft: true);
            for (var columnIndex = columns.Count - 2; columnIndex >= 0; columnIndex--)
                ReorderColumn(columns, columnIndex, neighbors, towardLeft: false);
        }

        var tableTop = Padding;
        var columnHeights = columns
            .Select(column => column.Sum(table => table.Height) + Math.Max(0, column.Count - 1) * TableGapY)
            .ToArray();
        var maxColumnHeight = columnHeights.Max();

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var y = tableTop + (maxColumnHeight - columnHeights[columnIndex]) / 2;
            foreach (var table in columns[columnIndex])
            {
                table.X = Padding + columnIndex * (TableWidth + TableGapX);
                table.Y = y;
                y += table.Height + TableGapY;
            }
        }

        // Keep each dependent group centered around the parents already positioned
        // to its left. The ordering remains fixed, so this cannot reintroduce crossings.
        for (var columnIndex = 1; columnIndex < columns.Count; columnIndex++)
            PositionColumn(columns[columnIndex], neighbors, tableTop);
    }

    private static void ReorderColumn(
        IReadOnlyList<List<DiagramTable>> columns,
        int columnIndex,
        IReadOnlyDictionary<DiagramTable, List<DiagramTable>> neighbors,
        bool towardLeft)
    {
        var positions = new Dictionary<DiagramTable, double>();
        for (var i = 0; i < columns.Count; i++)
            for (var row = 0; row < columns[i].Count; row++)
                positions[columns[i][row]] = (row + 0.5) / columns[i].Count;

        var ordered = columns[columnIndex]
            .Select((table, order) =>
            {
                var connected = neighbors[table]
                    .Where(neighbor => towardLeft ? neighbor.Column < columnIndex : neighbor.Column > columnIndex)
                    .Select(neighbor => positions[neighbor])
                    .ToList();
                return new
                {
                    Table = table,
                    Order = order,
                    Score = connected.Count == 0 ? positions[table] : connected.Average()
                };
            })
            .OrderBy(item => item.Score)
            .ThenBy(item => item.Order)
            .Select(item => item.Table)
            .ToList();

        columns[columnIndex].Clear();
        columns[columnIndex].AddRange(ordered);
    }

    private static void PositionColumn(
        IReadOnlyList<DiagramTable> column,
        IReadOnlyDictionary<DiagramTable, List<DiagramTable>> neighbors,
        int tableTop)
    {
        var desiredCenters = column.Select(table =>
        {
            var leftNeighbors = neighbors[table].Where(neighbor => neighbor.Column < table.Column).ToList();
            return leftNeighbors.Count == 0
                ? table.CenterY
                : leftNeighbors.Average(neighbor => neighbor.CenterY);
        }).ToArray();

        var previousBottom = double.NegativeInfinity;
        for (var i = 0; i < column.Count; i++)
        {
            var desiredTop = desiredCenters[i] - column[i].Height / 2.0;
            var top = Math.Max(desiredTop, previousBottom + TableGapY);
            column[i].Y = (int)Math.Round(top);
            previousBottom = column[i].Y + column[i].Height;
        }

        var averageError = desiredCenters
            .Select((desired, index) => desired - column[index].CenterY)
            .Average();
        var shift = (int)Math.Round(averageError);
        var minimum = column.Min(table => table.Y + shift);
        if (minimum < tableTop)
            shift += tableTop - minimum;

        foreach (var table in column)
            table.Y += shift;
    }

    private static void AssignRoutes(IReadOnlyList<DiagramRelation> relations)
    {
        var portRequests = new Dictionary<(DiagramTable Table, PortSide Side), List<PortRequest>>();

        foreach (var relation in relations)
        {
            if (relation.From.Column == relation.To.Column)
            {
                relation.FromSide = PortSide.Right;
                relation.ToSide = PortSide.Right;
            }
            else if (relation.From.Column < relation.To.Column)
            {
                relation.FromSide = PortSide.Right;
                relation.ToSide = PortSide.Left;
            }
            else
            {
                relation.FromSide = PortSide.Left;
                relation.ToSide = PortSide.Right;
            }

            AddPortRequest(relation.From, relation.FromSide, new PortRequest(relation, true, relation.To));
            AddPortRequest(relation.To, relation.ToSide, new PortRequest(relation, false, relation.From));
        }

        foreach (var ((table, _), requests) in portRequests)
        {
            var ordered = requests
                .OrderBy(request => request.Other.CenterY)
                .ThenBy(request => request.Relation.Label)
                .ThenByDescending(request => request.IsFrom)
                .ToList();
            var minimum = HeaderHeight + PortInset;
            var maximum = table.Height - PortInset;
            for (var i = 0; i < ordered.Count; i++)
            {
                var relativeY = minimum + (i + 1) * (maximum - minimum) / (ordered.Count + 1);
                if (ordered[i].IsFrom)
                    ordered[i].Relation.FromPortY = table.Y + relativeY;
                else
                    ordered[i].Relation.ToPortY = table.Y + relativeY;
            }
        }

        var sameColumnGroups = relations
            .Where(relation => relation.From.Column == relation.To.Column)
            .GroupBy(relation => relation.From.Column);
        foreach (var group in sameColumnGroups)
        {
            var lane = 0;
            foreach (var relation in group.OrderBy(relation => Math.Min(relation.From.CenterY, relation.To.CenterY)))
                relation.LaneX = relation.From.X + TableWidth + 42 + lane++ * RouteLaneGap;
        }

        foreach (var relation in relations.Where(relation => relation.From.Column != relation.To.Column))
            relation.LaneX = (PortX(relation.From, relation.FromSide) + PortX(relation.To, relation.ToSide)) / 2;

        void AddPortRequest(DiagramTable table, PortSide side, PortRequest request)
        {
            if (!portRequests.TryGetValue((table, side), out var requests))
                portRequests[(table, side)] = requests = [];
            requests.Add(request);
        }
    }

    private static void RenderRelations(StringBuilder svg, IEnumerable<DiagramRelation> relations)
    {
        foreach (var relation in relations)
        {
            var fromX = PortX(relation.From, relation.FromSide);
            var fromY = relation.FromPortY;
            var toX = PortX(relation.To, relation.ToSide);
            var toY = relation.ToPortY;
            int labelX;
            int labelY;
            string labelAnchor;

            svg.AppendLine($"""
                <g class="ef-diagram-link"
                   data-from="{Escape(relation.From.Slug)}"
                   data-to="{Escape(relation.To.Slug)}">
                """);

            var sameColumn = relation.From.Column == relation.To.Column;
            labelX = sameColumn ? relation.LaneX - 6 : relation.LaneX;
            labelY = (fromY + toY) / 2 - 6;
            labelAnchor = sameColumn ? "end" : "middle";

            svg.AppendLine($"""
                <path class="ef-diagram-relation"
                      d="M {fromX} {fromY}
                         C {relation.LaneX} {fromY},
                           {relation.LaneX} {toY},
                           {toX} {toY}"
                      marker-end="url(#ef-diagram-arrow)" />
                """);

            svg.AppendLine($"""
                <text class="ef-diagram-marker ef-diagram-cardinality"
                      x="{(relation.FromSide == PortSide.Right ? fromX + 7 : fromX - 7)}"
                      y="{fromY + 4}"
                      text-anchor="{(relation.FromSide == PortSide.Right ? "start" : "end")}">
                    {Escape(relation.FromCardinality)}
                </text>
                """);

            svg.AppendLine($"""
                <text class="ef-diagram-marker ef-diagram-cardinality"
                      x="{(relation.ToSide == PortSide.Right ? toX + 7 : toX - 7)}"
                      y="{toY + 4}"
                      text-anchor="{(relation.ToSide == PortSide.Right ? "start" : "end")}">
                    {Escape(relation.ToCardinality)}
                </text>
                """);

            svg.AppendLine($"""
                <text class="ef-diagram-relation-label"
                      x="{labelX}"
                      y="{labelY}"
                      text-anchor="{labelAnchor}">
                    {Escape(relation.Label)}
                </text>
                """);

            svg.AppendLine("</g>");
        }
    }

    private static int PortX(DiagramTable table, PortSide side)
        => side == PortSide.Left ? table.X : table.X + TableWidth;

    private static void RenderTable(StringBuilder svg, DiagramTable table, string? url)
    {
        var linked = !string.IsNullOrWhiteSpace(url);
        var title = Escape(table.Name);

        svg.AppendLine($"""
            <g class="ef-diagram-table"
               data-entity="{Escape(table.Slug)}"
               transform="translate({table.X} {table.Y})">
                <rect class="ef-diagram-table-bg"
                      width="{TableWidth}"
                      height="{table.Height}"
                      rx="7" />
            """);

        if (linked)
            svg.AppendLine($"""<a href="{Escape(url)}">""");

        svg.AppendLine($"""
                <rect class="ef-diagram-header"
                      width="{TableWidth}"
                      height="{HeaderHeight}"
                      rx="7" />
                <text class="ef-diagram-title" x="12" y="23">{title}</text>
            """);

        if (linked)
            svg.AppendLine("</a>");

        svg.AppendLine($"""
                <line class="ef-diagram-divider"
                      x1="0" y1="{HeaderHeight}"
                      x2="{TableWidth}" y2="{HeaderHeight}" />
            """);

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            var y = HeaderHeight + 17 + i * RowHeight;

            var marker = column.IsPrimaryKey
                ? "PK"
                : column.IsForeignKey
                    ? "FK"
                    : "";

            svg.AppendLine($"""
                    <text class="ef-diagram-marker" x="10" y="{y}">{marker}</text>
                    <text class="ef-diagram-column" x="38" y="{y}">{Escape(column.Name)}</text>
                    <text class="ef-diagram-type" x="{TableWidth - 10}" y="{y}" text-anchor="end">{Escape(column.Type)}</text>
                """);
        }

        svg.AppendLine($"""
                <rect class="ef-diagram-table-outline"
                      width="{TableWidth}"
                      height="{table.Height}"
                      rx="7" />
            """);
        svg.AppendLine("</g>");
    }

    private static string Escape(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Cardinality(RelationshipMultiplicity multiplicity, bool required, bool source) => multiplicity switch
    {
        RelationshipMultiplicity.OneToOne => source && !required ? "0..1" : "1",
        RelationshipMultiplicity.OneToMany => source
            ? required ? "1" : "0..1"
            : "N",
        RelationshipMultiplicity.ManyToMany => "*",
        _ => "*"
    };

    private static string TypeName(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(byte[])) return "binary";
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(Guid)) return "guid";
        if (type == typeof(DateTime)) return "datetime";
        if (type == typeof(DateTimeOffset)) return "datetimeoffset";
        if (type == typeof(DateOnly)) return "date";
        if (type == typeof(TimeOnly)) return "time";
        if (type == typeof(TimeSpan)) return "timespan";
        return type.Name;
    }

    private static string EmptySvg()
        => """
           <svg class="ef-dashboard-diagram"
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 400 100" role="img" aria-label="Empty data model">
               <text x="20" y="50" class="ef-diagram-marker">No resources discovered yet.</text>
           </svg>
           """;

    private sealed class DiagramTable(DashboardResource resource, IReadOnlyList<DiagramColumn> columns)
    {
        public DashboardResource Resource { get; } = resource;
        public IReadOnlyList<DiagramColumn> Columns { get; } = columns;

        public string Name => Resource.Name;
        public string Slug => Resource.Slug;

        public int Column { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public double CenterY => Y + Height / 2.0;

        public int Height { get; } = HeaderHeight + columns.Count * RowHeight + 8;
    }

    private sealed record DiagramColumn(
        string Name,
        string Type,
        bool IsPrimaryKey,
        bool IsForeignKey);

    private sealed class DiagramRelation(
        DiagramTable from,
        DiagramTable to,
        string fromCardinality,
        string toCardinality,
        RelationshipMultiplicity multiplicity,
        string label)
    {
        public DiagramTable From { get; } = from;
        public DiagramTable To { get; } = to;
        public string FromCardinality { get; } = fromCardinality;
        public string ToCardinality { get; } = toCardinality;
        public RelationshipMultiplicity Multiplicity { get; } = multiplicity;
        public string Label { get; } = label;

        public PortSide FromSide { get; set; }
        public PortSide ToSide { get; set; }
        public int FromPortY { get; set; }
        public int ToPortY { get; set; }
        public int LaneX { get; set; }
    }

    private sealed record PortRequest(DiagramRelation Relation, bool IsFrom, DiagramTable Other);

    private enum PortSide
    {
        Left,
        Right
    }
}
