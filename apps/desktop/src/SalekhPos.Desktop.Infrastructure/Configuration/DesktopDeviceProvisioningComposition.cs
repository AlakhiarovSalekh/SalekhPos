using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Infrastructure.Devices;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;

namespace SalekhPos.Desktop.Infrastructure.Configuration;

public static class DesktopDeviceProvisioningComposition
{
    public static IDeviceProvisioner Create(string databasePath, HttpClient authenticatedClient)
    {
        ArgumentNullException.ThrowIfNull(authenticatedClient);
        return new DeviceProvisioner(DeviceSigningKeyProvider.CreateForCurrentPlatform(),
            new SqliteDeviceProvisioningStateStore(databasePath),
            new HttpDeviceProvisioningClient(authenticatedClient));
    }
}
