namespace NVGInventory.Tests;

internal static class TestPasswords
{
    public static readonly string Hashed = BCrypt.Net.BCrypt.HashPassword("plaintext");
}
