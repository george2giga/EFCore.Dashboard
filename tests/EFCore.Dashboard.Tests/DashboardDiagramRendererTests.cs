using System.Text.RegularExpressions;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.Web;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class DashboardDiagramRendererTests
{
    private readonly DashboardDiagramRenderer _renderer = new();

    [Fact]
    public void Render_emits_one_svg_group_per_resource()
    {
        var svg = _renderer.Render(
            [Resource(typeof(Category)), Resource(typeof(Article))],
            []);

        Assert.StartsWith("<svg", svg.TrimStart());
        Assert.Contains("Category", svg);
        Assert.Contains("Article", svg);
        Assert.Contains("data-entity=\"Category\"", svg);
        Assert.Equal(2, Regex.Count(svg, "<g class=\"ef-diagram-table\""));
    }

    [Fact]
    public void Render_marks_primary_keys_and_relation_fields()
    {
        var article = Resource(typeof(Article), [
            KeyField(),
            new TextField { Name = "Title", Label = "Title", PropertyType = typeof(string) },
            new RelationField
            {
                Name = "CategoryId",
                Label = "Category Id",
                PropertyType = typeof(int),
                RelatedEntityType = typeof(Category),
                RelatedResourceName = "Category",
                PrincipalKeyProperty = "Id"
            }
        ]);

        var svg = _renderer.Render([article], []);

        Assert.Contains("<text class=\"ef-diagram-marker\" x=\"10\"", svg);
        Assert.Contains(">PK<", svg);
        Assert.Contains(">FK<", svg);
    }

    [Fact]
    public void Render_omits_hidden_fields()
    {
        var article = Resource(typeof(Article),
            KeyField(),
            new TextField { Name = "Secret", Label = "Secret", PropertyType = typeof(string), Hidden = true });

        var svg = _renderer.Render([article], []);

        Assert.DoesNotContain("Secret", svg);
    }

    [Fact]
    public void Render_links_table_headers_to_slug_urls()
    {
        var svg = _renderer.Render(
            [Resource(typeof(Category))],
            [],
            slug => $"/efcore-dashboard/{slug}");

        Assert.Contains("href=\"/efcore-dashboard/Category\"", svg);
    }

    [Fact]
    public void Render_draws_the_table_outline_after_the_header()
    {
        var svg = _renderer.Render([Resource(typeof(Category))], []);

        Assert.True(svg.IndexOf("ef-diagram-table-outline", StringComparison.Ordinal) >
            svg.IndexOf("ef-diagram-header", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_html_escapes_metadata()
    {
        var weird = new DashboardResource
        {
            Name = "<Bad&Name>",
            Slug = "<slug>",
            Label = "Weird",
            EntityType = typeof(Category),
            Key = KeyField(),
            Fields = [KeyField(), new TextField { Name = "Cr\"azy", Label = "Cr\"azy", PropertyType = typeof(string) }]
        };

        var svg = _renderer.Render([weird], []);

        Assert.DoesNotContain("<Bad", svg);
        Assert.Contains("&lt;Bad&amp;Name&gt;", svg);
        Assert.Contains("Cr&quot;azy", svg);
    }

    [Fact]
    public void Render_groups_dependents_into_later_columns_by_depth()
    {
        var relationship = new DashboardRelationship
        {
            SourceEntityType = typeof(Category),
            TargetEntityType = typeof(Article),
            Multiplicity = RelationshipMultiplicity.OneToMany,
            Required = true,
            NavigationName = "Articles"
        };

        var svg = _renderer.Render(
            [Resource(typeof(Category)), Resource(typeof(Article))],
            [relationship]);

        Assert.Contains("translate(30 30)", svg);
        Assert.Contains("translate(420 30)", svg);
        Assert.Contains("ef-diagram-relation", svg);
        Assert.Contains("data-from=\"Article\"", svg);
        Assert.Contains("data-to=\"Category\"", svg);
        Assert.Contains("Articles", svg);
    }

    [Fact]
    public void Render_reorders_columns_to_avoid_crossed_adjacent_relationships()
    {
        var svg = _renderer.Render(
            [
                Resource(typeof(AlphaParent)),
                Resource(typeof(BetaParent)),
                Resource(typeof(AlphaChild)),
                Resource(typeof(BetaChild))
            ],
            [
                Relationship(typeof(AlphaParent), typeof(BetaChild)),
                Relationship(typeof(BetaParent), typeof(AlphaChild))
            ]);

        var betaChild = PositionOf(svg, nameof(BetaChild));
        var alphaChild = PositionOf(svg, nameof(AlphaChild));

        Assert.Equal(betaChild.X, alphaChild.X);
        Assert.True(betaChild.Y < alphaChild.Y);
    }

    [Fact]
    public void Render_assigns_distinct_ports_to_relationships_on_the_same_table_side()
    {
        var svg = _renderer.Render(
            [Resource(typeof(Category)), Resource(typeof(AlphaChild)), Resource(typeof(BetaChild))],
            [
                Relationship(typeof(Category), typeof(AlphaChild)),
                Relationship(typeof(Category), typeof(BetaChild))
            ]);

        var targetYs = Regex.Matches(
                svg,
                "<path class=\"ef-diagram-relation\"\\s+d=\"(?<path>[^\"]+)\"",
                RegexOptions.Singleline)
            .Select(match => Regex.Matches(match.Groups["path"].Value, "(?<x>\\d+) (?<y>\\d+)")
                .Last().Groups["y"].Value)
            .ToList();

        Assert.Equal(2, targetYs.Count);
        Assert.Equal(2, targetYs.Distinct().Count());
    }

    [Fact]
    public void Render_ignores_self_relationships_when_assigning_depth()
    {
        var svg = _renderer.Render(
            [Resource(typeof(Category)), Resource(typeof(Article))],
            [
                Relationship(typeof(Category), typeof(Category)),
                Relationship(typeof(Category), typeof(Article))
            ]);

        Assert.Equal(30, PositionOf(svg, nameof(Category)).X);
        Assert.Equal(420, PositionOf(svg, nameof(Article)).X);
    }

    [Fact]
    public void Render_places_peer_only_resources_next_to_their_anchored_peer()
    {
        var peerRelationship = Relationship(typeof(Article), typeof(Peer)) with
        {
            Multiplicity = RelationshipMultiplicity.ManyToMany,
            Required = false
        };
        var svg = _renderer.Render(
            [Resource(typeof(Category)), Resource(typeof(Article)), Resource(typeof(Peer))],
            [Relationship(typeof(Category), typeof(Article)), peerRelationship]);

        Assert.Equal(30, PositionOf(svg, nameof(Category)).X);
        Assert.Equal(420, PositionOf(svg, nameof(Article)).X);
        Assert.Equal(810, PositionOf(svg, nameof(Peer)).X);
    }

    [Fact]
    public void Render_routes_skip_column_relationships_directly()
    {
        var svg = _renderer.Render(
            [Resource(typeof(AlphaParent)), Resource(typeof(BetaParent)), Resource(typeof(AlphaChild))],
            [
                Relationship(typeof(AlphaParent), typeof(BetaParent)),
                Relationship(typeof(BetaParent), typeof(AlphaChild)),
                Relationship(typeof(AlphaParent), typeof(AlphaChild))
            ]);

        Assert.DoesNotContain(" H ", svg);
        Assert.Equal(30, PositionOf(svg, nameof(AlphaParent)).Y);
    }

    [Fact]
    public void Render_renders_an_empty_state_when_there_are_no_resources()
    {
        var svg = _renderer.Render([], []);

        Assert.StartsWith("<svg", svg.TrimStart());
        Assert.Contains("No resources discovered yet.", svg);
    }

    private static DashboardResource Resource(Type type, params DashboardField[] extraFields) => new()
    {
        Name = type.Name,
        Slug = type.Name,
        Label = type.Name,
        EntityType = type,
        Key = KeyField(),
        Fields = [KeyField(), .. extraFields]
    };

    private static NumberField KeyField() =>
        new() { Name = "Id", Label = "Id", PropertyType = typeof(int), IsKey = true };

    private static DashboardRelationship Relationship(Type principal, Type dependent) => new()
    {
        SourceEntityType = principal,
        TargetEntityType = dependent,
        Multiplicity = RelationshipMultiplicity.OneToMany,
        Required = true
    };

    private static (int X, int Y) PositionOf(string svg, string tableName)
    {
        var groups = Regex.Matches(
            svg,
            "<g class=\"ef-diagram-table\"[^>]*transform=\"translate\\((?<x>\\d+) (?<y>\\d+)\\)\">(?<body>.*?)</g>",
            RegexOptions.Singleline);
        var group = groups.Single(match => match.Groups["body"].Value.Contains($">{tableName}</text>"));
        return (int.Parse(group.Groups["x"].Value), int.Parse(group.Groups["y"].Value));
    }

    private sealed class Category
    {
        public int Id { get; set; }
    }

    private sealed class Article
    {
        public int Id { get; set; }
    }

    private sealed class AlphaParent;
    private sealed class BetaParent;
    private sealed class AlphaChild;
    private sealed class BetaChild;
    private sealed class Peer;
}
