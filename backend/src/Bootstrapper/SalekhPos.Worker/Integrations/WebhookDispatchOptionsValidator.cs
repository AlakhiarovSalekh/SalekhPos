using Microsoft.Extensions.Options;
using SalekhPos.Authorization.Application;
using SalekhPos.Integrations.Application;

namespace SalekhPos.Worker.Integrations;

public sealed class WebhookDispatchOptionsValidator : IValidateOptions<WebhookDispatchOptions>
{
    public ValidateOptionsResult Validate(string? name, WebhookDispatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (options.PollInterval < TimeSpan.FromSeconds(1) || options.PollInterval > TimeSpan.FromMinutes(5))
        {
            failures.Add($"{nameof(options.PollInterval)} must be between one second and five minutes.");
        }

        if (options.OrganizationPageSize is < 1 or > 100)
        {
            failures.Add($"{nameof(options.OrganizationPageSize)} must be between 1 and 100.");
        }

        if (options.MaximumDeliveriesPerOrganization is < 1 or > 100)
        {
            failures.Add($"{nameof(options.MaximumDeliveriesPerOrganization)} must be between 1 and 100.");
        }

        if (options.Enabled)
        {
            try
            {
                _ = new AccessIdentity(options.Issuer, options.Subject);
                new IntegrationIdentity(options.Issuer, options.Subject).Validate();
            }
            catch (ArgumentException)
            {
                failures.Add("Enabled webhook dispatch requires a valid service-principal issuer and subject.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
