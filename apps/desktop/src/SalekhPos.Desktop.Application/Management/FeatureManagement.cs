namespace SalekhPos.Desktop.Application.Management;
public sealed record DesktopFeatureDecision(string Key,bool Enabled,string Source,DateTimeOffset EvaluatedAt);
public sealed record DesktopFeatureOverride(Guid OrganizationId,string Key,bool Enabled,string Reason,DateTimeOffset UpdatedAt);
public interface IFeatureManager{Task<DesktopFeatureDecision> EvaluateAsync(Guid organizationId,string key,CancellationToken cancellationToken);Task<DesktopFeatureOverride> OverrideAsync(Guid organizationId,string key,bool enabled,string reason,CancellationToken cancellationToken);}