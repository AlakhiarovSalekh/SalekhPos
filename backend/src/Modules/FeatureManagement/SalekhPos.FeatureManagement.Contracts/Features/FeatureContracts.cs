namespace SalekhPos.FeatureManagement.Contracts.Features;

public sealed record FeatureDecisionResponse(string Key, bool Enabled, string Source,
    DateTimeOffset EvaluatedAt);

public sealed record FeatureOverrideRequest(bool Enabled, string Reason);

public sealed record FeatureOverrideResponse(Guid OrganizationId, string Key, bool Enabled,
    string Reason, DateTimeOffset UpdatedAt);
