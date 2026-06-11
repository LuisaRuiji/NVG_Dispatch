using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NVGInventory.Security;
using Xunit;

namespace NVGInventory.Tests;

public sealed class SensitiveFieldProtectorTests
{
    private const string TestKey = "local-sensitive-field-test-key-123456";
    private const string RotatedKey = "rotated-sensitive-field-test-key-123";

    [Fact]
    public void Protect_RoundTripsSensitiveString()
    {
        SensitiveFieldProtector.Configure(TestKey);

        var protectedValue = SensitiveFieldProtector.Protect("OR-12345");

        Assert.NotNull(protectedValue);
        Assert.StartsWith("v2:default:", protectedValue);
        Assert.DoesNotContain("OR-12345", protectedValue);
        Assert.Equal("OR-12345", SensitiveFieldProtector.Unprotect(protectedValue));
    }

    [Fact]
    public void NullableDecimalConverter_RoundTripsThroughEncryptedString()
    {
        SensitiveFieldProtector.Configure(TestKey);
        var converter = SensitiveFieldValueConverters.CreateNullableSensitiveDecimalConverter();
        var toProvider = converter.ConvertToProviderExpression.Compile();
        var fromProvider = converter.ConvertFromProviderExpression.Compile();

        var protectedValue = toProvider(12345.67m);

        Assert.NotNull(protectedValue);
        Assert.StartsWith("v2:default:", protectedValue);
        Assert.DoesNotContain("12345.67", protectedValue);
        Assert.Equal(12345.67m, fromProvider(protectedValue));
        Assert.Null(toProvider(null));
        Assert.Null(fromProvider(null));
    }

    [Fact]
    public void KeyRing_DecryptsOldKeyAndEncryptsWithCurrentKey()
    {
        SensitiveFieldProtector.ConfigureKeyRing(
            "old",
            new Dictionary<string, string>
            {
                ["old"] = TestKey
            });
        var oldPayload = SensitiveFieldProtector.Protect("rotation-test");

        SensitiveFieldProtector.ConfigureKeyRing(
            "new",
            new Dictionary<string, string>
            {
                ["old"] = TestKey,
                ["new"] = RotatedKey
            });
        var newPayload = SensitiveFieldProtector.Protect("rotation-test");

        Assert.NotNull(oldPayload);
        Assert.NotNull(newPayload);
        Assert.StartsWith("v2:old:", oldPayload);
        Assert.StartsWith("v2:new:", newPayload);
        Assert.Equal("rotation-test", SensitiveFieldProtector.Unprotect(oldPayload));
        Assert.Equal("rotation-test", SensitiveFieldProtector.Unprotect(newPayload));
    }

    [Fact]
    public void KeyRing_DecryptsLegacyV1Payload()
    {
        var legacyPayload = ProtectLegacyV1("legacy-value", TestKey);

        SensitiveFieldProtector.ConfigureKeyRing(
            "new",
            new Dictionary<string, string>
            {
                ["old"] = TestKey,
                ["new"] = RotatedKey
            });

        Assert.StartsWith("v1:", legacyPayload);
        Assert.Equal("legacy-value", SensitiveFieldProtector.Unprotect(legacyPayload));
    }

    private static string ProtectLegacyV1(string plainText, string keyText)
    {
        var masterKey = Encoding.UTF8.GetBytes(keyText);
        var encryptionKey = SHA256.HashData(Combine(masterKey, "NVGInventory:sensitive-fields:encryption"));
        var authenticationKey = SHA256.HashData(Combine(masterKey, "NVGInventory:sensitive-fields:authentication"));
        var iv = RandomNumberGenerator.GetBytes(16);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = encryptionKey;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        using var hmac = new HMACSHA256(authenticationKey);
        hmac.TransformBlock(iv, 0, iv.Length, null, 0);
        hmac.TransformFinalBlock(cipherText, 0, cipherText.Length);

        return string.Join(
            ':',
            "v1",
            Convert.ToBase64String(iv),
            Convert.ToBase64String(cipherText),
            Convert.ToBase64String(hmac.Hash ?? Array.Empty<byte>()));
    }

    private static byte[] Combine(byte[] key, string label)
    {
        var labelBytes = Encoding.UTF8.GetBytes(label);
        var combined = new byte[key.Length + labelBytes.Length];
        Buffer.BlockCopy(key, 0, combined, 0, key.Length);
        Buffer.BlockCopy(labelBytes, 0, combined, key.Length, labelBytes.Length);
        return combined;
    }
}
