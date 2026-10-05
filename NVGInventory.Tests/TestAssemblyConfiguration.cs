using System.Runtime.CompilerServices;
using NVGInventory.Security;

namespace NVGInventory.Tests;

public static class TestAssemblyConfiguration
{
    [ModuleInitializer]
    public static void InitializeEncryption()
    {
        SensitiveFieldProtector.Configure("vaia-tests-sensitive-field-key-2026-minimum-length");
    }
}
