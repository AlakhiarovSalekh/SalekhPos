using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpAuditViewer(HttpClient client) : IAuditViewer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<AuditEventPage> ListAsync(Guid organizationId,int pageSize,long? afterSequence,string? action,Guid? branchId,CancellationToken ct)
    {
        if(organizationId==Guid.Empty||pageSize is <1 or >100||afterSequence<0||branchId==Guid.Empty)throw new ArgumentException("Audit query is invalid.");
        var q=new List<string>{"pageSize="+pageSize}; if(afterSequence.HasValue)q.Add("afterSequence="+afterSequence.Value);
        if(!string.IsNullOrWhiteSpace(action))q.Add("action="+Uri.EscapeDataString(action.Trim())); if(branchId.HasValue)q.Add("branchId="+branchId.Value.ToString("D"));
        using var response=await client.GetAsync($"api/v1/organizations/{organizationId:D}/audit-events?{string.Join('&',q)}",ct); response.EnsureSuccessStatusCode();
        var result=await response.Content.ReadFromJsonAsync<AuditEventPage>(JsonOptions,ct)??throw new InvalidOperationException("Audit response is empty.");
        ValidatePage(result,pageSize,branchId); return result;
    }
    public async Task<AuditIntegritySummary> VerifyAsync(Guid organizationId,long? fromSequence,int limit,CancellationToken ct)
    {
        if(organizationId==Guid.Empty||fromSequence is <1||limit is <1 or >5000)throw new ArgumentException("Audit verification request is invalid.");
        var path=$"api/v1/organizations/{organizationId:D}/audit-events/verify?limit={limit}"+(fromSequence.HasValue?$"&fromSequence={fromSequence.Value}":"");
        using var response=await client.GetAsync(path,ct); response.EnsureSuccessStatusCode();
        var result=await response.Content.ReadFromJsonAsync<AuditIntegritySummary>(JsonOptions,ct)??throw new InvalidOperationException("Audit integrity response is empty.");
        if(result.VerifiedEvents<0||result.FirstSequence is <1||result.LastSequence is <1||result.LastSequence<result.FirstSequence||result.LastHash is {Length:not 64})throw new InvalidOperationException("Audit integrity response is invalid.");
        return result;
    }
    private static void ValidatePage(AuditEventPage page,int pageSize,Guid? branchId)
    {
        if(page.Items.Count>pageSize||page.NextSequence<1)throw new InvalidOperationException("Audit page is invalid.");
        long previous=0; foreach(var item in page.Items){if(item.Id==Guid.Empty||item.Sequence<=previous||item.ActorSubject.Length is <1 or >256||item.Action.Length is <1 or >120||item.TargetType.Length is <1 or >80||item.Outcome.Length is <1 or >32||item.EventHash.Length!=64||item.PreviousHash.Length!=64||item.OccurredAt.Offset!=TimeSpan.Zero||(branchId.HasValue&&item.BranchId!=branchId))throw new InvalidOperationException("Audit event response is invalid.");previous=item.Sequence;}
        if(page.NextSequence.HasValue&&(page.Items.Count==0||page.NextSequence!=page.Items[^1].Sequence))throw new InvalidOperationException("Audit cursor is invalid.");
    }
}
