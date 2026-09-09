namespace SalekhPos.Devices.Domain.Devices;

public sealed record RegisteredDevice
{
    public RegisteredDevice(Guid id, Guid registerId, string code, string name, string platform, int syncProtocolVersion)
    {
        if (id == Guid.Empty || registerId == Guid.Empty) throw new ArgumentException("Device identifiers are required.");
        if (string.IsNullOrWhiteSpace(code) || code != code.Trim() || code.Length > 64
            || code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new ArgumentException("Device code is invalid.");
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 100 || name.Any(char.IsControl)) throw new ArgumentException("Device name is invalid.");
        if (platform is not ("desktop" or "mobile" or "kiosk")) throw new ArgumentException("Device platform is invalid.");
        if (syncProtocolVersion != 1) throw new ArgumentException("Sync protocol version is unsupported.");
        Id = id; RegisterId = registerId; Code = code; Name = name; Platform = platform; SyncProtocolVersion = syncProtocolVersion;
    }
    public Guid Id { get; }
    public Guid RegisterId { get; }
    public string Code { get; }
    public string Name { get; }
    public string Platform { get; }
    public int SyncProtocolVersion { get; }
}
