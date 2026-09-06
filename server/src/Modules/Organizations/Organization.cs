namespace SalekhPos.Organizations;

public sealed record Organization
{
    public Guid Id { get; }
    public string Name { get; }

    public Organization(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Organization ID must not be empty.", nameof(id));
        }

        Id = id;
        Name = OrganizationRules.ValidateName(name);
    }

    public Organization Rename(string name) => new(Id, name);
}
