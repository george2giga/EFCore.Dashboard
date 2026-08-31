namespace EFCore.Dashboard.Web;

/// <summary>Serializes string rows using RFC 4180-style escaping and CRLF record separators.</summary>
public static class DashboardCsv
{
    /// <summary>Escapes a single CSV field when quoting is required.</summary>
    public static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    /// <summary>Serializes rows without adding a BOM or final record separator.</summary>
    public static string Serialize(IReadOnlyList<IReadOnlyList<string>> rows)
        => string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Escape))));
}
