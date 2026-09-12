using System.Runtime.Versioning;
using System.Security.Cryptography;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Infrastructure.Devices;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class DeviceSigningKeyTests
{
    [Fact]
    public void SigningKeyBoundaryHasNoPrivateKeyExportSurface()
    {
        var members = typeof(IDeviceSigningKey).GetMembers().Select(member => member.Name).ToArray();

        Assert.DoesNotContain(members, name => name.Contains("Private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("ExportParameters", members);
        Assert.DoesNotContain("ExportPkcs8PrivateKey", members);
        Assert.Equal(["GetSubjectPublicKeyInfo", "KeyReference", "Sign", "get_KeyReference"],
            members.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void WindowsKeyIsStableP256NonExportableAndSignsP1363()
    {
        if (!OperatingSystem.IsWindows()) return;
        WindowsKeyRoundTrip();
    }

    [Fact]
    public void WindowsProviderRejectsMissingAndUnsafeKeys()
    {
        if (!OperatingSystem.IsWindows()) return;
        WindowsUnsafeKeys();
    }

    [Fact]
    public void UnsupportedProviderFailsClosed()
    {
        var provider = new UnsupportedDeviceSigningKeyProvider();

        Assert.Throws<PlatformNotSupportedException>(() => provider.CreateKeyReference());
        Assert.Throws<PlatformNotSupportedException>(() => provider.Open("anything", true));
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsKeyRoundTrip()
    {
        var provider = new WindowsCngDeviceSigningKeyProvider();
        var reference = provider.CreateKeyReference();
        try
        {
            byte[] publicKey;
            using (var first = provider.Open(reference, true))
            {
                publicKey = first.GetSubjectPublicKeyInfo();
                using var verifier = ECDsa.Create();
                verifier.ImportSubjectPublicKeyInfo(publicKey, out var read);
                var parameters = verifier.ExportParameters(false);
                Assert.Equal(publicKey.Length, read);
                Assert.Equal("1.2.840.10045.3.1.7", parameters.Curve.Oid.Value);
                Assert.Equal(32, parameters.Q.X!.Length);
                Assert.Equal(32, parameters.Q.Y!.Length);
                var signature = first.Sign("signed-device-proof"u8);
                Assert.Equal(64, signature.Length);
                Assert.True(verifier.VerifyData("signed-device-proof"u8, signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            }

            using var reopened = provider.Open(reference, false);
            Assert.Equal(publicKey, reopened.GetSubjectPublicKeyInfo());
            using var persisted = CngKey.Open(Name(reference),
                CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey);
            Assert.False(persisted.IsMachineKey);
            Assert.Equal(CngExportPolicies.None, persisted.ExportPolicy);
        }
        finally { Delete(reference); }
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsUnsafeKeys()
    {
        var provider = new WindowsCngDeviceSigningKeyProvider();
        var missing = provider.CreateKeyReference();
        Assert.Throws<InvalidOperationException>(() => provider.Open(missing, false));

        var wrongCurve = provider.CreateKeyReference();
        var exportable = provider.CreateKeyReference();
        try
        {
            using (CngKey.Create(CngAlgorithm.ECDsaP384, Name(wrongCurve),
                       Parameters(CngExportPolicies.None))) { }
            Assert.Throws<InvalidOperationException>(() => provider.Open(wrongCurve, false));

            using (CngKey.Create(CngAlgorithm.ECDsaP256, Name(exportable),
                       Parameters(CngExportPolicies.AllowExport))) { }
            Assert.Throws<InvalidOperationException>(() => provider.Open(exportable, false));
        }
        finally { Delete(wrongCurve); Delete(exportable); }
    }

    [SupportedOSPlatform("windows")]
    private static CngKeyCreationParameters Parameters(CngExportPolicies policy) => new()
    {
        Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
        ExportPolicy = policy,
        KeyUsage = CngKeyUsages.Signing,
    };

    [SupportedOSPlatform("windows")]
    private static void Delete(string reference)
    {
        var name = Name(reference);
        if (!CngKey.Exists(name, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey)) return;
        using var key = CngKey.Open(name, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey);
        key.Delete();
    }

    private static string Name(string reference) => reference["cng-user:".Length..];
}
