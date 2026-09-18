using Avalonia.Controls;
using Avalonia.Controls.Selection;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerSupportWindow : Window
{
    private readonly ISupportManager support;
    private readonly Guid organizationId;
    private readonly Guid branchId;
    private readonly List<SupportTicketSummary> tickets = [];
    private SupportTicketDetail? detail;
    private Guid? nextCursor;
    private Guid createOperation;
    private string createFingerprint = "";
    private Guid transitionOperation;
    private string transitionFingerprint = "";
    private Guid diagnosticOperation;
    private string diagnosticFingerprint = "";

    public ManagerSupportWindow() => throw new InvalidOperationException("Support runtime is required.");

    public ManagerSupportWindow(ISupportManager support, Guid organizationId, Guid branchId)
    {
        this.support = support ?? throw new ArgumentNullException(nameof(support));
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Support scope is invalid.");
        this.organizationId = organizationId;
        this.branchId = branchId;
        InitializeComponent();
        ScopeText.Text = $"Organization {organizationId:D} · Branch {branchId:D}";
        Opened += async (_, _) => await Refresh();
    }

    private async void RefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Refresh();
    private async void LoadMoreClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await LoadMore();
    private async void TicketSelectionChanged(object? sender, SelectionChangedEventArgs e) => await LoadSelected();
    private async void CreateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Create();
    private async void TransitionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await Transition();
    private async void DiagnosticClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await AddDiagnostic();

    private string? Filter() => (StatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() is { Length: > 0 } value ? value : null;

    private async Task Refresh()
    {
        await Execute(async () =>
        {
            var page = await support.ListAsync(organizationId, 50, null, Filter(), default);
            tickets.Clear(); tickets.AddRange(page.Items); nextCursor = page.NextCursor;
            RefreshTickets();
            MessageText.Text = $"{tickets.Count} support ticket(s) loaded.";
        });
    }

    private async Task LoadMore()
    {
        if (!nextCursor.HasValue) return;
        await Execute(async () =>
        {
            var page = await support.ListAsync(organizationId, 50, nextCursor, Filter(), default);
            tickets.AddRange(page.Items); nextCursor = page.NextCursor; RefreshTickets();
        });
    }

    private async Task LoadSelected()
    {
        var index = TicketList.SelectedIndex;
        if (index < 0 || index >= tickets.Count) return;
        await Execute(async () =>
        {
            detail = await support.ReadAsync(organizationId, tickets[index].Id, default);
            ShowDetail();
        });
    }

    private async Task Create()
    {
        var subject = (SubjectBox.Text ?? "").Trim();
        var description = (DescriptionBox.Text ?? "").Trim();
        var priority = (PriorityBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "normal";
        var fingerprint = string.Join("\u001f", branchId, subject, description, priority);
        var operationId = Operation(fingerprint, ref createFingerprint, ref createOperation);
        await Execute(async () =>
        {
            var created = await support.CreateAsync(organizationId,
                new(branchId, subject, description, priority), operationId, default);
            createFingerprint = ""; createOperation = Guid.Empty;
            SubjectBox.Clear(); DescriptionBox.Clear();
            await Refresh();
            var index = tickets.FindIndex(ticket => ticket.Id == created.Id);
            if (index >= 0) TicketList.SelectedIndex = index;
        });
    }

    private async Task Transition()
    {
        if (detail is null || TransitionStatusBox.SelectedItem is not string status) return;
        var note = (TransitionNoteBox.Text ?? "").Trim();
        var fingerprint = string.Join("\u001f", detail.Ticket.Id, detail.Ticket.Version, status, note);
        var operationId = Operation(fingerprint, ref transitionFingerprint, ref transitionOperation);
        await Execute(async () =>
        {
            var updated = await support.TransitionAsync(organizationId, detail.Ticket.Id,
                new(status, detail.Ticket.Version, note), operationId, default);
            transitionFingerprint = ""; transitionOperation = Guid.Empty; TransitionNoteBox.Clear();
            detail = await support.ReadAsync(organizationId, updated.Id, default);
            ShowDetail(); await Refresh();
        });
    }

    private async Task AddDiagnostic()
    {
        if (detail is null) return;
        var kind = (DiagnosticKindBox.Text ?? "").Trim();
        var reference = (DiagnosticReferenceBox.Text ?? "").Trim();
        var sha = (DiagnosticShaBox.Text ?? "").Trim().ToLowerInvariant();
        var fingerprint = string.Join("\u001f", detail.Ticket.Id, kind, reference, sha);
        var operationId = Operation(fingerprint, ref diagnosticFingerprint, ref diagnosticOperation);
        await Execute(async () =>
        {
            await support.AddDiagnosticAsync(organizationId, detail.Ticket.Id,
                new(kind, reference, sha), operationId, default);
            diagnosticFingerprint = ""; diagnosticOperation = Guid.Empty;
            DiagnosticReferenceBox.Clear(); DiagnosticShaBox.Clear();
            detail = await support.ReadAsync(organizationId, detail.Ticket.Id, default);
            ShowDetail();
        });
    }

    private void RefreshTickets()
    {
        TicketList.ItemsSource = tickets.Select(ticket =>
            $"{ticket.Priority.ToUpperInvariant()} · {ticket.Status.Replace('_', ' ')} · v{ticket.Version} · {ticket.Subject} · {ticket.Id:D}").ToArray();
        LoadMoreButton.IsVisible = nextCursor.HasValue;
    }

    private void ShowDetail()
    {
        if (detail is null) return;
        DetailTitle.Text = detail.Ticket.Subject;
        DetailMeta.Text = $"{detail.Ticket.Priority} · {detail.Ticket.Status.Replace('_', ' ')} · version {detail.Ticket.Version} · {detail.Ticket.UpdatedAt:yyyy-MM-dd HH:mm} UTC";
        DetailDescription.Text = detail.Ticket.Description;
        DiagnosticList.ItemsSource = detail.Diagnostics.Select(value =>
            $"{value.Kind} · {value.Reference} · {value.Sha256}").ToArray();
        var transitions = NextStatuses(detail.Ticket.Status).ToArray();
        TransitionStatusBox.ItemsSource = transitions;
        TransitionStatusBox.SelectedIndex = transitions.Length > 0 ? 0 : -1;
        TransitionButton.IsEnabled = transitions.Length > 0;
        DiagnosticButton.IsEnabled = true;
    }

    private async Task Execute(Func<Task> action)
    {
        try { await action(); MessageText.Text = ""; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or HttpRequestException or FormatException)
        {
            MessageText.Text = exception is HttpRequestException
                ? "The support service request failed." : exception.Message;
        }
    }

    private static Guid Operation(string fingerprint, ref string previousFingerprint, ref Guid operationId)
    {
        if (fingerprint != previousFingerprint || operationId == Guid.Empty)
        {
            previousFingerprint = fingerprint;
            operationId = Guid.NewGuid();
        }
        return operationId;
    }

    private static IEnumerable<string> NextStatuses(string status) => status switch
    {
        "open" => ["in_progress", "closed"],
        "in_progress" => ["waiting_for_customer", "resolved", "closed"],
        "waiting_for_customer" => ["in_progress", "resolved", "closed"],
        "resolved" => ["in_progress", "closed"],
        _ => []
    };
}
