using System.Globalization;
using System.Text.Json;
using EFCore.Dashboard.Core;

namespace EFCore.Dashboard.Web;

/// <summary>Converts dashboard form values and formats scalar values for round trips.</summary>
public interface IDashboardValueConverter
{
    /// <summary>Converts one submitted invariant string to the field's nullable underlying CLR type.</summary>
    bool TryConvert(DashboardField field, string? value, out object? result, out string? error);
    /// <summary>Formats a scalar value using a stable invariant representation suitable for form submission.</summary>
    string Format(DashboardField field, object? value);
}

/// <summary>
/// Converts strings, booleans, signed and unsigned numbers, GUIDs, enums, and supported date/time scalars.
/// </summary>
public sealed class DashboardValueConverter : IDashboardValueConverter
{
    /// <inheritdoc />
    public bool TryConvert(DashboardField field, string? value, out object? result, out string? error)
    {
        result = null;
        error = null;
        var target = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (field.Required)
            {
                error = $"{field.Label} is required.";
                return false;
            }
            return true;
        }

        try
        {
            if (target == typeof(string))
            {
                if (field.Editor == DashboardEditors.Json) JsonDocument.Parse(value).Dispose();
                result = value;
            }
            else if (target == typeof(Guid)) result = Guid.Parse(value);
            else if (target == typeof(DateOnly)) result = DateOnly.Parse(value, CultureInfo.InvariantCulture);
            else if (target == typeof(TimeOnly)) result = TimeOnly.Parse(value, CultureInfo.InvariantCulture);
            else if (target == typeof(TimeSpan)) result = TimeSpan.Parse(value, CultureInfo.InvariantCulture);
            else if (target == typeof(DateTime)) result = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            else if (target == typeof(DateTimeOffset)) result = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            else if (target == typeof(bool)) result = value.ToLowerInvariant() switch
            {
                "true" or "on" or "1" => true,
                "false" or "off" or "0" => false,
                _ => throw new FormatException()
            };
            else if (target.IsEnum) result = Enum.Parse(target, value, true);
            else result = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            return true;
        }
        catch (JsonException)
        {
            error = $"{field.Label} must contain valid JSON.";
            return false;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            error = $"{field.Label} has an invalid value.";
            return false;
        }
    }

    /// <inheritdoc />
    public string Format(DashboardField field, object? value)
    {
        if (value is null) return string.Empty;
        return value switch
        {
            byte[] => string.Empty,
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }
}
