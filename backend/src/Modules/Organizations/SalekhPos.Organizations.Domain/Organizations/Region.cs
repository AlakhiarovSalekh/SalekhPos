namespace SalekhPos.Organizations.Domain.Organizations;

public sealed record Region
{
    public Guid OrganizationId { get; }
    public Guid BusinessId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public bool IsActive { get; }

    public Region(Guid organizationId, Guid businessId, Guid id, string code, string name, bool isActive = true)
    {
        OrganizationId = OrganizationRules.ValidateId(organizationId, nameof(organizationId));
        BusinessId = OrganizationRules.ValidateId(businessId, nameof(businessId));
        Id = OrganizationRules.ValidateId(id, nameof(id));
        Code = OrganizationRules.ValidateCode(code);
        Name = OrganizationRules.ValidateName(name);
        IsActive = isActive;
    }

    public Region Rename(string name) => new(OrganizationId, BusinessId, Id, Code, name, IsActive);
    public Region Deactivate() => new(OrganizationId, BusinessId, Id, Code, Name, false);
}
