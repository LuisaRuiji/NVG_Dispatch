using System.Security.Cryptography;
using System.Text;

namespace NVGInventory.Security;

public static class SensitiveFieldProtector
{
    private const string LegacyVersion = "v1";
    private const string CurrentVersion = "v2";
    private const string DefaultKeyId = "default";
    private const int MinimumKeyBytes = 32;
    private static KeyRing? _keyRing;

    public static void Configure(string? keyText)
    {
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return;
        }

        ConfigureKeyRing(DefaultKeyId, new Dictionary<string, string>
        {
            [DefaultKeyId] = keyText
        });
    }

    public static void ConfigureKeyRing(string? currentKeyId, IReadOnlyDictionary<string, string> keyTexts)
    {
        if (keyTexts.Count == 0)
        {
            return;
        }

        var normalizedCurrentKeyId = NormalizeKeyId(currentKeyId);
        if (string.IsNullOrWhiteSpace(normalizedCurrentKeyId))
        {
            normalizedCurrentKeyId = DefaultKeyId;
        }

        var keys = keyTexts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => NormalizeKeyId(pair.Key),
                pair => CreateKeySet(NormalizeKeyId(pair.Key), ParseKey(pair.Value.Trim())),
                StringComparer.Ordinal);

        if (keys.Count == 0)
        {
            return;
        }

        if (!keys.ContainsKey(normalizedCurrentKeyId))
        {
            throw new InvalidOperationException("Encryption:CurrentKeyId must match a key in Encryption:Keys.");
        }

        Interlocked.Exchange(ref _keyRing, new KeyRing(normalizedCurrentKeyId, keys));
    }

    public static void RequireConfigured()
    {
        _ = GetKeyRing();
    }

    public static string? Protect(string? plainText)
    {
        if (plainText is null)
        {
            return null;
        }

        var keyRing = GetKeyRing();
        var keySet = keyRing.Keys[keyRing.CurrentKeyId];
        var iv = RandomNumberGenerator.GetBytes(16);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keySet.EncryptionKey;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var mac = ComputeMac(keySet.AuthenticationKey, iv, cipherText);

        return string.Join(
            ':',
            CurrentVersion,
            keySet.KeyId,
            Convert.ToBase64String(iv),
            Convert.ToBase64String(cipherText),
            Convert.ToBase64String(mac));
    }

    public static string? Unprotect(string? protectedValue)
    {
        if (protectedValue is null)
        {
            return null;
        }

        var parts = protectedValue.Split(':');
        return parts[0] switch
        {
            CurrentVersion when parts.Length == 5 => UnprotectCurrent(parts),
            LegacyVersion when parts.Length == 4 => UnprotectLegacy(parts),
            _ => throw new CryptographicException("Sensitive field payload is not in a supported encrypted format.")
        };
    }

    private static string UnprotectCurrent(string[] parts)
    {
        var keyId = NormalizeKeyId(parts[1]);
        var keyRing = GetKeyRing();
        if (!keyRing.Keys.TryGetValue(keyId, out var keySet))
        {
            throw new CryptographicException("Sensitive field payload key is not configured.");
        }

        return UnprotectWithKey(
            keySet,
            Convert.FromBase64String(parts[2]),
            Convert.FromBase64String(parts[3]),
            Convert.FromBase64String(parts[4]));
    }

    private static string UnprotectLegacy(string[] parts)
    {
        var iv = Convert.FromBase64String(parts[1]);
        var cipherText = Convert.FromBase64String(parts[2]);
        var mac = Convert.FromBase64String(parts[3]);
        var keyRing = GetKeyRing();

        foreach (var keySet in keyRing.Keys.Values)
        {
            if (TryUnprotectWithLegacyKey(keySet, iv, cipherText, mac, out var plainText))
            {
                return plainText;
            }
        }

        throw new CryptographicException("Sensitive field payload authentication failed.");
    }

    private static string UnprotectWithKey(KeySet keySet, byte[] iv, byte[] cipherText, byte[] mac)
    {
        if (!TryUnprotectWithKey(keySet, iv, cipherText, mac, out var plainText))
        {
            throw new CryptographicException("Sensitive field payload authentication failed.");
        }

        return plainText;
    }

    private static bool TryUnprotectWithKey(
        KeySet keySet,
        byte[] iv,
        byte[] cipherText,
        byte[] mac,
        out string plainText)
    {
        var expectedMac = ComputeMac(keySet.AuthenticationKey, iv, cipherText);
        if (!CryptographicOperations.FixedTimeEquals(mac, expectedMac))
        {
            plainText = string.Empty;
            return false;
        }

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keySet.EncryptionKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        plainText = Encoding.UTF8.GetString(plainBytes);
        return true;
    }

    private static bool TryUnprotectWithLegacyKey(
        KeySet keySet,
        byte[] iv,
        byte[] cipherText,
        byte[] mac,
        out string plainText)
    {
        var expectedMac = ComputeMac(keySet.LegacyAuthenticationKey, iv, cipherText);
        if (!CryptographicOperations.FixedTimeEquals(mac, expectedMac))
        {
            plainText = string.Empty;
            return false;
        }

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keySet.LegacyEncryptionKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        plainText = Encoding.UTF8.GetString(plainBytes);
        return true;
    }

    private static KeyRing GetKeyRing()
    {
        var existing = Volatile.Read(ref _keyRing);
        if (existing is not null)
        {
            return existing;
        }

        var keyText = Environment.GetEnvironmentVariable("EncryptionKey")
            ?? Environment.GetEnvironmentVariable("NVG_ENCRYPTION_KEY")
            ?? Environment.GetEnvironmentVariable("DataProtection__EncryptionKey");

        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw new InvalidOperationException(
                "EncryptionKey must be configured before sensitive field values are persisted or read.");
        }

        var keys = new Dictionary<string, KeySet>(StringComparer.Ordinal)
        {
            [DefaultKeyId] = CreateKeySet(DefaultKeyId, ParseKey(keyText.Trim()))
        };
        var keyRing = new KeyRing(DefaultKeyId, keys);
        var previous = Interlocked.CompareExchange(ref _keyRing, keyRing, null);
        return previous ?? keyRing;
    }

    private static KeySet CreateKeySet(string keyId, byte[] masterKey)
    {
        var encryptionKey = SHA256.HashData(Combine(masterKey, $"NVGInventory:sensitive-fields:{keyId}:encryption"));
        var authenticationKey = SHA256.HashData(Combine(masterKey, $"NVGInventory:sensitive-fields:{keyId}:authentication"));
        var legacyEncryptionKey = SHA256.HashData(Combine(masterKey, "NVGInventory:sensitive-fields:encryption"));
        var legacyAuthenticationKey = SHA256.HashData(Combine(masterKey, "NVGInventory:sensitive-fields:authentication"));
        return new KeySet(
            keyId,
            encryptionKey,
            authenticationKey,
            legacyEncryptionKey,
            legacyAuthenticationKey);
    }

    private static byte[] ParseKey(string keyText)
    {
        if (keyText.Length % 2 == 0 && keyText.All(Uri.IsHexDigit))
        {
            var hexBytes = Convert.FromHexString(keyText);
            EnsureMinimumKeyLength(hexBytes);
            return hexBytes;
        }

        try
        {
            var base64Bytes = Convert.FromBase64String(keyText);
            if (base64Bytes.Length >= MinimumKeyBytes)
            {
                return base64Bytes;
            }
        }
        catch (FormatException)
        {
        }

        var utf8Bytes = Encoding.UTF8.GetBytes(keyText);
        EnsureMinimumKeyLength(utf8Bytes);
        return utf8Bytes;
    }

    private static string NormalizeKeyId(string? keyId)
    {
        var normalized = string.IsNullOrWhiteSpace(keyId) ? DefaultKeyId : keyId.Trim();
        if (normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Encryption key ids cannot contain ':'.");
        }

        return normalized;
    }

    private static void EnsureMinimumKeyLength(byte[] keyBytes)
    {
        if (keyBytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException("Encryption keys must contain at least 32 bytes.");
        }
    }

    private static byte[] Combine(byte[] key, string label)
    {
        var labelBytes = Encoding.UTF8.GetBytes(label);
        var combined = new byte[key.Length + labelBytes.Length];
        Buffer.BlockCopy(key, 0, combined, 0, key.Length);
        Buffer.BlockCopy(labelBytes, 0, combined, key.Length, labelBytes.Length);
        return combined;
    }

    private static byte[] ComputeMac(byte[] authenticationKey, byte[] iv, byte[] cipherText)
    {
        using var hmac = new HMACSHA256(authenticationKey);
        hmac.TransformBlock(iv, 0, iv.Length, null, 0);
        hmac.TransformFinalBlock(cipherText, 0, cipherText.Length);
        return hmac.Hash ?? throw new CryptographicException("Unable to compute sensitive field authentication tag.");
    }

    private sealed record KeyRing(string CurrentKeyId, IReadOnlyDictionary<string, KeySet> Keys);

    private sealed record KeySet(
        string KeyId,
        byte[] EncryptionKey,
        byte[] AuthenticationKey,
        byte[] LegacyEncryptionKey,
        byte[] LegacyAuthenticationKey);
}
