namespace SalekhPos.Organizations.Domain.Organizations;

public sealed record Branch
{
    public Guid OrganizationId { get; }
    public Guid BusinessId { get; }
    public Guid? RegionId { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public string TimeZoneId { get; }
    public bool IsActive { get; }

    public Branch(Guid organizationId, Guid businessId, Guid id, string code, string name,
        string timeZoneId, Guid? regionId = null, bool isActive = true)
    {
        OrganizationId = OrganizationRules.ValidateId(organizationId, nameof(organizationId));
        BusinessId = OrganizationRules.ValidateId(businessId, nameof(businessId));
        Id = OrganizationRules.ValidateId(id, nameof(id));
        RegionId = regionId is { } value ? OrganizationRules.ValidateId(value, nameof(regionId)) : null;
        Code = OrganizationRules.ValidateCode(code);
        Name = OrganizationRules.ValidateName(name);
        TimeZoneId = OrganizationRules.ValidateTimeZoneId(timeZoneId);
        IsActive = isActive;
    }

    public Branch Rename(string name) => new(OrganizationId, BusinessId, Id, Code, name, TimeZoneId, RegionId, IsActive);
    public Branch Deactivate() => new(OrganizationId, BusinessId, Id, Code, Name, TimeZoneId, RegionId, false);
}
