using System.Security.Cryptography;
using System.Runtime.Versioning;
using SalekhPos.Desktop.Application.Devices;

namespace SalekhPos.Desktop.Infrastructure.Devices;

public static class DeviceSigningKeyProvider
{
    public static IDeviceSigningKeyProvider CreateForCurrentPlatform() => OperatingSystem.IsWindows()
        ? new WindowsCngDeviceSigningKeyProvider() : new UnsupportedDeviceSigningKeyProvider();
}

public sealed class UnsupportedDeviceSigningKeyProvider : IDeviceSigningKeyProvider
{
    public string CreateKeyReference() => throw Unsupported();
    public IDeviceSigningKey Open(string keyReference, bool createIfMissing) => throw Unsupported();
    private static PlatformNotSupportedException Unsupported() => new(
        "Non-exportable desktop device keys are not implemented for this operating system.");
}

[SupportedOSPlatform("windows")]
public sealed class WindowsCngDeviceSigningKeyProvider : IDeviceSigningKeyProvider
{
    private const string Prefix = "cng-user:";

    public string CreateKeyReference()
    {
        RequireWindows();
        return Prefix + Guid.NewGuid().ToString("N");
    }

    public IDeviceSigningKey Open(string keyReference, bool createIfMissing)
    {
        RequireWindows();
        var name = Parse(keyReference);
        CngKey key;
        if (CngKey.Exists(name, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey))
            key = CngKey.Open(name, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey);
        else if (createIfMissing)
            key = CngKey.Create(CngAlgorithm.ECDsaP256, name, new CngKeyCreationParameters
            {
                Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
                ExportPolicy = CngExportPolicies.None,
                KeyUsage = CngKeyUsages.Signing,
                KeyCreationOptions = CngKeyCreationOptions.None,
            });
        else
            throw new InvalidOperationException("The persisted device signing key is missing.");

        try { return new WindowsCngDeviceSigningKey(keyReference, key); }
        catch { key.Dispose(); throw; }
    }

    internal static string Parse(string reference)
    {
        if (reference is null || !reference.StartsWith(Prefix, StringComparison.Ordinal)
            || reference.Length != Prefix.Length + 32
            || !Guid.TryParseExact(reference[Prefix.Length..], "N", out _))
            throw new InvalidOperationException("The device key reference is invalid.");
        return reference[Prefix.Length..];
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException(
            "Windows CNG device keys are available only on Windows.");
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsCngDeviceSigningKey : IDeviceSigningKey
{
    private const string P256Oid = "1.2.840.10045.3.1.7";
    private CngKey? key;
    private ECDsaCng? signer;
    private readonly byte[] publicKey;

    public WindowsCngDeviceSigningKey(string reference, CngKey key)
    {
        KeyReference = reference;
        if (key.IsMachineKey || key.Provider != CngProvider.MicrosoftSoftwareKeyStorageProvider
            || key.ExportPolicy != CngExportPolicies.None || (key.KeyUsage & CngKeyUsages.Signing) == 0)
            throw Unsafe();
        var candidate = new ECDsaCng(key);
        try
        {
            var parameters = candidate.ExportParameters(false);
            if (!parameters.Curve.IsNamed || parameters.Curve.Oid.Value != P256Oid
                || parameters.Q.X?.Length != 32 || parameters.Q.Y?.Length != 32 || candidate.KeySize != 256)
                throw Unsafe();
            publicKey = candidate.ExportSubjectPublicKeyInfo();
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey, out var read);
            if (read != publicKey.Length || !publicKey.AsSpan().SequenceEqual(verifier.ExportSubjectPublicKeyInfo()))
                throw Unsafe();
            this.key = key;
            signer = candidate;
        }
        catch { candidate.Dispose(); throw; }
    }

    public string KeyReference { get; }
    public byte[] GetSubjectPublicKeyInfo() => (byte[])publicKey.Clone();

    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(signer is null, this);
        var signature = signer.SignData(data, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        if (signature.Length != 64) throw Unsafe();
        return signature;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref signer, null)?.Dispose();
        Interlocked.Exchange(ref key, null)?.Dispose();
    }

    private static InvalidOperationException Unsafe() => new("The device signing key is not a safe P-256 key.");
}
