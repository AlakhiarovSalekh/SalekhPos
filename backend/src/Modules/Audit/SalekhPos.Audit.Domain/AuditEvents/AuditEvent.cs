namespace SalekhPos.Audit.Domain.AuditEvents;

public enum AuditOutcome { Attempted, Succeeded, Failed }

public sealed record AuditEventDraft
{
    public Guid OrganizationId { get; }
    public Guid EventId { get; }
    public Guid OperationId { get; }
    public string Action { get; }
    public string TargetType { get; }
    public Guid? TargetId { get; }
    public Guid? BranchId { get; }
    public Guid? DeviceId { get; }
    public string? SourceIp { get; }
    public string CorrelationId { get; }
    public string RequestId { get; }
    public AuditOutcome Outcome { get; }
    public string? Reason { get; }

    public AuditEventDraft(Guid organizationId, Guid eventId, Guid operationId, string action,
        string targetType, Guid? targetId, Guid? branchId, Guid? deviceId, string? sourceIp,
        string correlationId, string requestId, AuditOutcome outcome, string? reason)
    {
        if (organizationId == Guid.Empty || eventId == Guid.Empty || operationId == Guid.Empty)
            throw new ArgumentException("Audit identifiers are invalid.");
        OrganizationId = organizationId; EventId = eventId; OperationId = operationId;
        Action = Required(action, 180, "action"); TargetType = Required(targetType, 80, "target type");
        TargetId = targetId == Guid.Empty ? throw new ArgumentException("Target ID is invalid.") : targetId;
        BranchId = branchId == Guid.Empty ? throw new ArgumentException("Branch ID is invalid.") : branchId;
        DeviceId = deviceId == Guid.Empty ? throw new ArgumentException("Device ID is invalid.") : deviceId;
        SourceIp = Optional(sourceIp, 64, "source IP");
        CorrelationId = Required(correlationId, 128, "correlation ID");
        RequestId = Required(requestId, 128, "request ID");
        Outcome = outcome;
        Reason = Optional(reason, 400, "reason");
    }

    public static string OutcomeName(AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Attempted => "attempted",
        AuditOutcome.Succeeded => "succeeded",
        AuditOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    private static string Required(string value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > max || value.Any(char.IsControl))
            throw new ArgumentException($"Audit {field} is invalid.");
        return value;
    }

    private static string? Optional(string? value, int max, string field) => value is null
        ? null
        : Required(value, max, field);
}
