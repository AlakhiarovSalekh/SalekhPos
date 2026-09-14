namespace SalekhPos.Authorization.Contracts.Organizations;

public sealed record AccessibleOrganizationResponse(Guid Id, string Name);

public sealed record AccessibleOrganizationPage(
    IReadOnlyList<AccessibleOrganizationResponse> Items,
    Guid? NextCursor);
