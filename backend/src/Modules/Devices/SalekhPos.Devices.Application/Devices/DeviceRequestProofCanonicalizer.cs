using System.Text;

namespace SalekhPos.Devices.Application.Devices;

public static class DeviceRequestProofCanonicalizer
{
    public const string Domain = "salekhpos-device-request-v1";

    // Every value is already in its canonical representation before this method is called:
    // uppercase method, versioned route path with lowercase D-format UUIDs, D-format credential
    // UUID, an endpoint-defined operation identity, uppercase SHA-256 hex, seven-digit UTC time,
    // unpadded base64url nonce, and the server-stored fingerprint encoded as base64.
    // Lines are UTF-8, LF-separated, and there is deliberately no trailing LF.
    public static byte[] Create(string method, string canonicalPath, Guid organizationId, Guid branchId, Guid deviceId,
        Guid credentialId, string operationIdentity, string bodyDigest, string timestamp, string nonce,
        ReadOnlySpan<byte> fingerprint) => Encoding.UTF8.GetBytes(string.Join('\n',
            Domain,
            method,
            canonicalPath,
            organizationId.ToString("D"),
            branchId.ToString("D"),
            deviceId.ToString("D"),
            credentialId.ToString("D"),
            operationIdentity,
            bodyDigest,
            timestamp,
            nonce,
            Convert.ToBase64String(fingerprint)));
}
