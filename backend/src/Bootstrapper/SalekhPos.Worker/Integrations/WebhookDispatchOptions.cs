namespace SalekhPos.Worker.Integrations;

public sealed class WebhookDispatchOptions
{
    public const string SectionName = "Integrations:WebhookDispatch";

    public bool Enabled { get; set; }

    public string Issuer { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int OrganizationPageSize { get; set; } = 50;

    public int MaximumDeliveriesPerOrganization { get; set; } = 20;
}
