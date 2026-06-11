using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;

    public const string RequirementMessage =
        "Password must be at least 15 characters and include at least one uppercase letter, one number, and one special character.";

    public const string CommonPasswordMessage =
        "Password is too common. Choose a less predictable password.";

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.Ordinal)
    {
        "password123456!",
        "password123456789!",
        "p@ssword123456!",
        "p@ssw0rd123456!",
        "qwerty123456789!",
        "qwertyuiop12345!",
        "welcome1234567!",
        "changeme123456!",
        "letmein1234567!",
        "admin123456789!",
        "administrator1!",
        "superadmin12345!",
        "company1234567!",
        "nvgdispatch123!",
        "nvginventory1!",
        "spring20261234!",
        "summer20261234!",
        "winter20261234!",
        "january2026123!",
        "december202612!"
    };

    public static void EnsureValid(string password)
    {
        if (!MeetsComplexity(password))
        {
            throw new BusinessRuleViolationException(RequirementMessage);
        }

        if (IsCommon(password))
        {
            throw new BusinessRuleViolationException(CommonPasswordMessage);
        }
    }

    public static bool IsValid(string password)
    {
        return MeetsComplexity(password) && !IsCommon(password);
    }

    public static bool IsCommon(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return CommonPasswords.Contains(NormalizeForCommonPasswordCheck(password));
    }

    private static bool MeetsComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            return false;
        }

        var hasUppercase = false;
        var hasNumber = false;
        var hasSpecial = false;

        foreach (var character in password)
        {
            if (char.IsUpper(character))
            {
                hasUppercase = true;
            }
            else if (char.IsDigit(character))
            {
                hasNumber = true;
            }
            else if (char.IsPunctuation(character) || char.IsSymbol(character))
            {
                hasSpecial = true;
            }
        }

        return hasUppercase && hasNumber && hasSpecial;
    }

    private static string NormalizeForCommonPasswordCheck(string password)
    {
        return new string(password
            .Trim()
            .Where(character => !char.IsWhiteSpace(character))
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
