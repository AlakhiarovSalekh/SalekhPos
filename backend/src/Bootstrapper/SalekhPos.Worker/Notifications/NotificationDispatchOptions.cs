namespace SalekhPos.Worker.Notifications;

public sealed class NotificationDispatchOptions
{
    public const string SectionName = "Notifications:Dispatch";

    public bool Enabled { get; set; }

    public string Issuer { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int OrganizationPageSize { get; set; } = 50;

    public int MaximumDeliveriesPerOrganization { get; set; } = 20;

    public int LeaseSeconds { get; set; } = 60;

    public string EmailEndpoint { get; set; } = string.Empty;

    public string PushEndpoint { get; set; } = string.Empty;

    public string EmailCredentialEnvironmentVariable { get; set; } = string.Empty;

    public string PushCredentialEnvironmentVariable { get; set; } = string.Empty;
}
