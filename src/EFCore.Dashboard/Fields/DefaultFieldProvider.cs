using Microsoft.EntityFrameworkCore.Metadata;
using EFCore.Dashboard.Core;
using EFCore.Dashboard.Extensibility;

namespace EFCore.Dashboard.Fields;

/// <summary>
/// Provides the final convention mapping for strings, booleans, signed and unsigned numbers,
/// GUIDs, enums, supported date/time scalar types, and opt-in byte[] image fields.
/// </summary>
public sealed class DefaultFieldProvider : IDashboardFieldProvider
{
    /// <inheritdoc />
    public int Order => int.MaxValue;
    /// <inheritdoc />
    public bool CanHandle(IProperty property) => true;

    /// <inheritdoc />
    public DashboardField Create(IProperty property, DashboardFieldContext context)
    {
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        DashboardField field = type switch
        {
            _ when type == typeof(bool) => new BooleanField
            {
                Name = string.Empty,
                Label = context.Label,
                PropertyType = property.ClrType
            },
            _ when type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(DateOnly) ||
                type == typeof(TimeOnly) ||
                type == typeof(TimeSpan) => new DateTimeField
                {
                    Name = string.Empty,
                    Label = context.Label,
                    PropertyType = property.ClrType
                },
            _ when type == typeof(byte[]) => new BinaryField
            {
                Name = string.Empty,
                Label = context.Label,
                PropertyType = property.ClrType
            },
            _ when type.IsEnum => new EnumField
            {
                EnumType = type,
                Name = string.Empty,
                Label = context.Label,
                PropertyType = property.ClrType
            },
            _ when IsNumber(type) => new NumberField
            {
                Name = string.Empty,
                Label = context.Label,
                PropertyType = property.ClrType
            },
            _ => new TextField
            {
                Name = string.Empty,
                Label = context.Label,
                PropertyType = property.ClrType
            }
        };

        return field with
        {
            Required = context.Required,
            ReadOnly = context.ReadOnly,
            Hidden = context.Hidden,
            HiddenInList = context.HiddenInList,
            HiddenInEditor = context.HiddenInEditor,
            IsKey = context.IsKey,
            MaxLength = context.MaxLength,
            Editor = context.Editor
        };
    }

    private static bool IsNumber(Type type) =>
        type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
        type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
        type == typeof(float) || type == typeof(double) || type == typeof(decimal);
}
