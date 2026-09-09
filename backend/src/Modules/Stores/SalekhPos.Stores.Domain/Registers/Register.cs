namespace SalekhPos.Stores.Domain.Registers;

public sealed record Register
{
    public Guid Id { get; }
    public Guid BranchId { get; }
    public string Code { get; }
    public string Name { get; }
    public Register(Guid id, Guid branchId, string code, string name)
    {
        if (id == Guid.Empty || branchId == Guid.Empty) throw new ArgumentException("Register identifiers are required.");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 32 || code != code.Trim()
            || code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new ArgumentException("Register code is invalid.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name != name.Trim() || name.Any(char.IsControl))
            throw new ArgumentException("Register name is invalid.");
        Id = id; BranchId = branchId; Code = code; Name = name;
    }
}
