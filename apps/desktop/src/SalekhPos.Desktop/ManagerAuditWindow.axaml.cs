using Avalonia.Controls;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerAuditWindow : Window
{
    private readonly IAuditViewer audit;
    private readonly Guid organizationId;
    private readonly Guid branchId;
    public ManagerAuditWindow() => throw new InvalidOperationException("Audit viewer is required.");
    public ManagerAuditWindow(IAuditViewer audit,Guid organizationId,Guid branchId)
    {
        this.audit=audit??throw new ArgumentNullException(nameof(audit));
        if(organizationId==Guid.Empty||branchId==Guid.Empty)throw new ArgumentException("Audit scope is invalid.");
        this.organizationId=organizationId;this.branchId=branchId;InitializeComponent();Opened+=async(_,_)=>await Load();
    }
    private async void RefreshClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await Load();
    private async void VerifyClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
    {
        try{var result=await audit.VerifyAsync(organizationId,null,5000,default);StatusText.Text=result.IsValid?$"Integrity valid · {result.VerifiedEvents} event(s) verified.":"INTEGRITY FAILURE — audit chain verification failed.";}
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or HttpRequestException){StatusText.Text="Audit integrity verification could not be completed.";}
    }
    private async Task Load()
    {
        try
        {
            var action=string.IsNullOrWhiteSpace(ActionBox.Text)?null:ActionBox.Text.Trim();
            var page=await audit.ListAsync(organizationId,100,null,action,branchId,default);
            EventsList.ItemsSource=page.Items.Select(x=>new AuditRow($"#{x.Sequence} · {x.Action}",$"{x.ActorSubject} · {x.Outcome}",
                $"{x.TargetType}{(x.TargetId.HasValue?$" · {x.TargetId:D}":"")} · {x.OccurredAt:O}",x.EventHash)).ToArray();
            StatusText.Text=$"Loaded {page.Items.Count} audit event(s) for branch {branchId:D}.";
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or HttpRequestException)
        { StatusText.Text="Audit events could not be loaded."; }
    }
    private sealed record AuditRow(string Heading,string Actor,string Target,string Hash);
}
