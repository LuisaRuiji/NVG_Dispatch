namespace NVGInventory.Controllers;

internal static class QueryParsing
{
    public static bool TryParseEnum<TEnum>(string? raw, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (Enum.TryParse(raw, true, out value))
        {
            return true;
        }

        var normalized = Normalize(raw);
        foreach (var name in Enum.GetNames(typeof(TEnum)))
        {
            if (Normalize(name) == normalized)
            {
                value = (TEnum)Enum.Parse(typeof(TEnum), name, true);
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
