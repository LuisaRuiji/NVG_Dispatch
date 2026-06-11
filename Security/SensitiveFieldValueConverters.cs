using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NVGInventory.Security;

public static class SensitiveFieldValueConverters
{
    public static ValueConverter<string?, string?> CreateNullableSensitiveStringConverter()
    {
        return new ValueConverter<string?, string?>(
            value => SensitiveFieldProtector.Protect(value),
            value => SensitiveFieldProtector.Unprotect(value));
    }

    public static ValueConverter<string, string> CreateRequiredSensitiveStringConverter()
    {
        return new ValueConverter<string, string>(
            value => SensitiveFieldProtector.Protect(value) ?? string.Empty,
            value => SensitiveFieldProtector.Unprotect(value) ?? string.Empty);
    }

    public static ValueConverter<decimal?, string?> CreateNullableSensitiveDecimalConverter()
    {
        return new ValueConverter<decimal?, string?>(
            value => ProtectDecimal(value),
            value => UnprotectDecimal(value));
    }

    private static string? ProtectDecimal(decimal? value)
    {
        return value.HasValue
            ? SensitiveFieldProtector.Protect(value.Value.ToString(CultureInfo.InvariantCulture))
            : null;
    }

    private static decimal? UnprotectDecimal(string? protectedValue)
    {
        var plainText = SensitiveFieldProtector.Unprotect(protectedValue);
        return string.IsNullOrWhiteSpace(plainText)
            ? null
            : decimal.Parse(plainText, NumberStyles.Number, CultureInfo.InvariantCulture);
    }
}
