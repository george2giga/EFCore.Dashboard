using System.Text;
using System.Text.RegularExpressions;

namespace EFCore.Dashboard.Core;

/// <summary>Provides the naming conventions used for labels and resource slugs.</summary>
public static partial class DashboardNaming
{
    public static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return WordBoundaryRegex().Replace(value, "$1 $2");
    }

    public static string SlugFor(string typeName)
        => ToKebabCase(Pluralize(typeName));

    public static string Pluralize(string value)
    {
        if (value.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            value.Length > 1 && !"aeiou".Contains(char.ToLowerInvariant(value[^2])))
            return value[..^1] + "ies";

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            return value + "es";

        return value + "s";
    }

    public static string ToKebabCase(string value)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0) sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundaryRegex();
}
