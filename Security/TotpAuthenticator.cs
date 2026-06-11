using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NVGInventory.Security;

public sealed class TotpAuthenticator
{
    private const int SecretBytes = 20;
    private const int TimeStepSeconds = 30;
    private const int Digits = 6;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecretKey()
    {
        return EncodeBase32(RandomNumberGenerator.GetBytes(SecretBytes));
    }

    public string BuildOtpAuthUri(string issuer, string accountName, string secretKey)
    {
        var safeIssuer = string.IsNullOrWhiteSpace(issuer) ? "NVG Dispatch" : issuer.Trim();
        var safeAccount = string.IsNullOrWhiteSpace(accountName) ? "account" : accountName.Trim();
        var label = Uri.EscapeDataString($"{safeIssuer}:{safeAccount}");
        var queryIssuer = Uri.EscapeDataString(safeIssuer);
        return $"otpauth://totp/{label}?secret={secretKey}&issuer={queryIssuer}&digits={Digits}&period={TimeStepSeconds}";
    }

    public bool VerifyCode(string secretKey, string code, DateTime nowUtc, int windowSteps = 1)
    {
        var normalized = NormalizeCode(code);
        if (normalized is null)
        {
            return false;
        }

        var secretBytes = DecodeBase32(secretKey);
        var currentStep = GetTimeStep(nowUtc);
        var window = Math.Max(0, windowSteps);

        for (var offset = -window; offset <= window; offset++)
        {
            var expected = ComputeCode(secretBytes, currentStep + offset);
            if (FixedTimeEquals(expected, normalized))
            {
                return true;
            }
        }

        return false;
    }

    private static long GetTimeStep(DateTime nowUtc)
    {
        return new DateTimeOffset(nowUtc).ToUnixTimeSeconds() / TimeStepSeconds;
    }

    private static string? NormalizeCode(string code)
    {
        var normalized = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        return normalized.Length == Digits ? normalized : null;
    }

    private static string ComputeCode(byte[] secretBytes, long timeStep)
    {
        var counter = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits), CultureInfo.InvariantCulture);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(left),
            Encoding.ASCII.GetBytes(right));
    }

    private static string EncodeBase32(byte[] data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                output.Append(Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            output.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return output.ToString();
    }

    private static byte[] DecodeBase32(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("TOTP secret is required.");
        }

        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var character in normalized)
        {
            var index = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException("TOTP secret is not valid Base32.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                bitsLeft -= 8;
            }
        }

        return bytes.ToArray();
    }
}
