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

    public static ValueConverter<decimal, string> CreateRequiredSensitiveDecimalConverter()
    {
        return new ValueConverter<decimal, string>(
            value => SensitiveFieldProtector.Protect(value.ToString(CultureInfo.InvariantCulture)) ?? string.Empty,
            value => UnprotectRequiredDecimal(value));
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

    private static decimal UnprotectRequiredDecimal(string? protectedValue)
    {
        // Schema upgrades add required encrypted decimal columns to legacy rows. An
        // empty payload represents the migration-time zero only; all subsequent
        // writes are protected by the current key ring.
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return 0m;
        }

        return decimal.Parse(
            SensitiveFieldProtector.Unprotect(protectedValue) ?? "0",
            NumberStyles.Number,
            CultureInfo.InvariantCulture);
    }
}
