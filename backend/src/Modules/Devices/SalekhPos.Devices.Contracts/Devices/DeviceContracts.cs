namespace SalekhPos.Devices.Contracts.Devices;

public sealed record RegisterDeviceRequest(Guid RegisterId, string Code, string Name, string Platform, int SyncProtocolVersion);
public sealed record DeviceResponse(Guid Id, Guid BranchId, Guid RegisterId, string Code, string Name, string Platform,
    string Status, int SyncProtocolVersion, DateTimeOffset RegisteredAt, string RegisteredBy);
public sealed record DevicePage(IReadOnlyList<DeviceResponse> Items, Guid? NextCursor);
