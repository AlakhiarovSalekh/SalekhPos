using System.Globalization;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Infrastructure.Authentication;

namespace SalekhPos.Desktop.Infrastructure.Configuration;

public sealed record DesktopRuntimeSettings(Uri ApiBaseAddress, NativeOidcSettings Oidc,
    PosWorkspaceScope Scope, string DatabasePath)
{
    public const string ApiBaseAddressVariable = "SALEKHPOS_API_BASE_URL";
    public const string OidcAuthorityVariable = "SALEKHPOS_OIDC_AUTHORITY";
    public const string OidcClientIdVariable = "SALEKHPOS_OIDC_CLIENT_ID";
    public const string OidcScopesVariable = "SALEKHPOS_OIDC_SCOPES";
    public const string OidcCallbackPortVariable = "SALEKHPOS_OIDC_CALLBACK_PORT";
    public const string OidcClientSecretVariable = "SALEKHPOS_OIDC_CLIENT_SECRET";
    public const string OrganizationIdVariable = "SALEKHPOS_ORGANIZATION_ID";
    public const string BranchIdVariable = "SALEKHPOS_BRANCH_ID";
    public const string DeviceIdVariable = "SALEKHPOS_DEVICE_ID";
    public const string DatabasePathVariable = "SALEKHPOS_SQLITE_PATH";

    public static DesktopRuntimeSettings FromEnvironment() => Load(Environment.GetEnvironmentVariable);

    public static DesktopRuntimeSettings Load(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var api = Required(read, ApiBaseAddressVariable);
        var authority = Required(read, OidcAuthorityVariable);
        var clientId = Required(read, OidcClientIdVariable);
        var scopes = Required(read, OidcScopesVariable).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var callbackPort = ParseInt32(Required(read, OidcCallbackPortVariable));
        var organizationId = ParseGuid(Required(read, OrganizationIdVariable));
        var branchId = ParseGuid(Required(read, BranchIdVariable));
        var deviceId = ParseGuid(Required(read, DeviceIdVariable));
        var databasePath = Required(read, DatabasePathVariable);

        if (!string.IsNullOrWhiteSpace(read(OidcClientSecretVariable))
            || !Uri.TryCreate(api, UriKind.Absolute, out var apiAddress))
            throw Invalid();

        var settings = new DesktopRuntimeSettings(apiAddress,
            new NativeOidcSettings(authority, clientId, scopes, callbackPort),
            new PosWorkspaceScope(organizationId, branchId, deviceId), databasePath);
        settings.Validate();
        return settings with { DatabasePath = Path.GetFullPath(databasePath) };
    }

    public void Validate()
    {
        if (ApiBaseAddress is null || Oidc is null || ApiBaseAddress.Scheme != Uri.UriSchemeHttps
            || !ApiBaseAddress.IsAbsoluteUri
            || !string.IsNullOrEmpty(ApiBaseAddress.UserInfo) || !string.IsNullOrEmpty(ApiBaseAddress.Query)
            || !string.IsNullOrEmpty(ApiBaseAddress.Fragment) || ApiBaseAddress.AbsolutePath != "/"
            || ApiBaseAddress.AbsoluteUri.Length > 2048 || Scope.OrganizationId == Guid.Empty
            || Scope.BranchId == Guid.Empty || Scope.DeviceId == Guid.Empty
            || string.IsNullOrWhiteSpace(DatabasePath) || DatabasePath.Any(char.IsControl)
            || !Path.IsPathFullyQualified(DatabasePath))
            throw Invalid();

        try
        {
            var fullPath = Path.GetFullPath(DatabasePath);
            if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath))
                || (OperatingSystem.IsWindows() && fullPath.StartsWith("\\\\", StringComparison.Ordinal)))
                throw Invalid();
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            throw Invalid();
        }

        try
        {
            Oidc.Validate();
        }
        catch (InvalidOperationException)
        {
            throw Invalid();
        }
    }

    private static string Required(Func<string, string?> read, string name)
    {
        var value = read(name);
        return string.IsNullOrWhiteSpace(value) ? throw Invalid() : value.Trim();
    }

    private static Guid ParseGuid(string value) => Guid.TryParseExact(value, "D", out var parsed)
        ? parsed : throw Invalid();

    private static int ParseInt32(string value) => int.TryParse(value, NumberStyles.None,
        CultureInfo.InvariantCulture, out var parsed) ? parsed : throw Invalid();

    private static InvalidOperationException Invalid() =>
        new("Desktop runtime configuration is incomplete or unsafe.");
}
