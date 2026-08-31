using EFCore.Dashboard.Core;
using EFCore.Dashboard.Pages.Admin;
using EFCore.Dashboard.Web;
using Xunit;

namespace EFCore.Dashboard.Tests;

public sealed class DashboardValueConverterTests
{
    private readonly DashboardValueConverter _converter = new();

    [Fact]
    public void Converts_integer()
    {
        var field = new NumberField { Name = "Count", Label = "Count", PropertyType = typeof(int), Required = true };
        Assert.True(_converter.TryConvert(field, "42", out var value, out _));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Required_field_rejects_empty_value()
    {
        var field = new TextField { Name = "Title", Label = "Title", PropertyType = typeof(string), Required = true };
        Assert.False(_converter.TryConvert(field, "", out _, out var error));
        Assert.Equal("Title is required.", error);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("false-ish")]
    [InlineData("2")]
    public void Invalid_boolean_fails(string input)
    {
        var field = new BooleanField { Name = "Active", Label = "Active", PropertyType = typeof(bool) };

        Assert.False(_converter.TryConvert(field, input, out _, out var error));
        Assert.Equal("Active has an invalid value.", error);
    }

    [Fact]
    public void DateOnly_round_trips() =>
        AssertRoundTrip(typeof(DateOnly), new DateOnly(2026, 8, 25));

    [Fact]
    public void TimeOnly_round_trips() =>
        AssertRoundTrip(typeof(TimeOnly), new TimeOnly(13, 14, 15, 123));

    [Fact]
    public void TimeSpan_round_trips() =>
        AssertRoundTrip(typeof(TimeSpan), new TimeSpan(1, 2, 3, 4, 567));

    [Fact]
    public void DateTime_formatting_preserves_seconds_fraction_and_kind()
    {
        var dateTime = new DateTime(2026, 8, 22, 13, 14, 15, 123, DateTimeKind.Utc).AddTicks(4567);

        Assert.Equal("2026-08-22T13:14:15.1234567Z", _converter.Format(Field(typeof(DateTime)), dateTime));
    }

    [Fact]
    public void DateTimeOffset_formatting_preserves_seconds_fraction_and_offset()
    {
        var dateTimeOffset = new DateTimeOffset(2026, 8, 22, 13, 14, 15, 123, TimeSpan.FromHours(5.5)).AddTicks(4567);

        Assert.Equal("2026-08-22T13:14:15.1234567+05:30", _converter.Format(Field(typeof(DateTimeOffset)), dateTimeOffset));
    }

    [Fact]
    public void Json_editor_accepts_valid_json_without_changing_it()
    {
        const string json = "{\"enabled\":true}";
        var field = new TextField
        {
            Name = "Settings",
            Label = "Settings",
            PropertyType = typeof(string),
            Editor = DashboardEditors.Json
        };

        Assert.True(_converter.TryConvert(field, json, out var value, out var error), error);
        Assert.Equal(json, value);
    }

    [Fact]
    public void Json_editor_rejects_invalid_json()
    {
        var field = new TextField
        {
            Name = "Settings",
            Label = "Settings",
            PropertyType = typeof(string),
            Editor = DashboardEditors.Json
        };

        Assert.False(_converter.TryConvert(field, "{invalid}", out _, out var error));
        Assert.Equal("Settings must contain valid JSON.", error);
    }

    [Fact]
    public void DateTimeOffset_converts_native_local_datetime_with_explicit_offset()
    {
        var field = Field(typeof(DateTimeOffset));

        Assert.True(_converter.TryConvert(field, "2026-08-22T13:14:15.123+05:45", out var value, out var error), error);
        Assert.Equal(new DateTimeOffset(2026, 8, 22, 13, 14, 15, 123, TimeSpan.FromHours(5.75)), value);
    }

    [Theory]
    [InlineData(typeof(DateOnly), "date", null)]
    [InlineData(typeof(DateTime), "datetime-local", "any")]
    [InlineData(typeof(DateTimeOffset), "datetime-local", "any")]
    [InlineData(typeof(TimeOnly), "time", "any")]
    [InlineData(typeof(TimeSpan), "text", null)]
    public void Edit_model_maps_temporal_types_to_semantic_inputs(Type type, string inputType, string? step)
    {
        var model = new EditModel(null!, null!, _converter);
        var field = Field(type);

        Assert.Equal(inputType, model.InputType(field));
        Assert.Equal(step, model.InputStep(field));
    }

    [Theory]
    [InlineData(DashboardEditors.Email, "email")]
    [InlineData(DashboardEditors.Url, "url")]
    [InlineData(DashboardEditors.ImageUrl, "url")]
    [InlineData(DashboardEditors.Telephone, "tel")]
    public void Edit_model_maps_semantic_text_editors_to_input_types(string editor, string inputType)
    {
        var model = new EditModel(null!, null!, _converter);
        var field = new TextField { Name = "Value", Label = "Value", PropertyType = typeof(string), Editor = editor };

        Assert.Equal(inputType, model.InputType(field));
    }

    [Fact]
    public void Flags_enum_combination_converts_and_reports_selected_options()
    {
        var field = new EnumField
        {
            Name = "Permissions",
            Label = "Permissions",
            PropertyType = typeof(TestPermissions),
            EnumType = typeof(TestPermissions),
            Required = true
        };

        Assert.True(EditModel.IsFlagsEnum(field));
        Assert.True(_converter.TryConvert(field, "Read,Write", out var value, out var error), error);
        Assert.Equal(TestPermissions.Read | TestPermissions.Write, value);
        Assert.True(EditModel.IsEnumOptionSelected(field, "Read, Write", nameof(TestPermissions.Read)));
        Assert.True(EditModel.IsEnumOptionSelected(field, "Read, Write", nameof(TestPermissions.Write)));
        Assert.False(EditModel.IsEnumOptionSelected(field, "Read, Write", nameof(TestPermissions.None)));
    }

    [Fact]
    public void Binary_values_format_as_empty_string()
    {
        var field = new BinaryField { Name = "Cover", Label = "Cover", PropertyType = typeof(byte[]) };

        Assert.Equal(string.Empty, _converter.Format(field, new byte[] { 1, 2, 3 }));
        Assert.Equal(string.Empty, _converter.Format(field, null));
    }

    private void AssertRoundTrip(Type type, object expected)
    {
        var field = Field(type);
        var formatted = _converter.Format(field, expected);

        Assert.True(_converter.TryConvert(field, formatted, out var actual, out var error), error);
        Assert.Equal(expected, actual);
    }

    private static DateTimeField Field(Type type) =>
        new() { Name = "Value", Label = "Value", PropertyType = type };

    [Flags]
    private enum TestPermissions
    {
        None = 0,
        Read = 1,
        Write = 2
    }
}
