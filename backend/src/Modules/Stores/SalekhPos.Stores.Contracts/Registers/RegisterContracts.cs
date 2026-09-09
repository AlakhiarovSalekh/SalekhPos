namespace SalekhPos.Stores.Contracts.Registers;

public sealed record CreateRegisterRequest(string Code, string Name);
public sealed record RegisterResponse(Guid Id, Guid BranchId, string Code, string Name, bool IsActive, DateTimeOffset CreatedAt);
public sealed record RegisterPage(IReadOnlyList<RegisterResponse> Items, Guid? NextCursor);
