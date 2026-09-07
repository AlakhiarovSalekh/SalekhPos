namespace SalekhPos.Organizations.Domain.Organizations;

public sealed record Business
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public bool IsActive { get; }

    public Business(Guid organizationId, Guid id, string code, string name, bool isActive = true)
    {
        OrganizationId = OrganizationRules.ValidateId(organizationId, nameof(organizationId));
        Id = OrganizationRules.ValidateId(id, nameof(id));
        Code = OrganizationRules.ValidateCode(code);
        Name = OrganizationRules.ValidateName(name);
        IsActive = isActive;
    }

    public Business Rename(string name) => new(OrganizationId, Id, Code, name, IsActive);
    public Business Deactivate() => new(OrganizationId, Id, Code, Name, false);
}
