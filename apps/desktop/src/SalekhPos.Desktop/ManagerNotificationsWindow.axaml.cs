using Avalonia.Controls;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerNotificationsWindow : Window
{
    private readonly INotificationDeliveryManager manager;
    private readonly Guid organizationId;

    public ManagerNotificationsWindow() =>
        throw new InvalidOperationException("Notification delivery runtime is required.");

    public ManagerNotificationsWindow(
        INotificationDeliveryManager manager,
        Guid organizationId)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        this.organizationId = organizationId;
        InitializeComponent();
        ScopeText.Text = $"Organization {organizationId:D}";
        Opened += async (_, _) => await Refresh();
    }

    private async void RefreshClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e) => await Refresh();

    private async void RetryClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DeliveryList.SelectedItem is not DesktopNotificationDelivery delivery)
        {
            MessageText.Text = "Select a delivery first.";
            return;
        }
        if (delivery.Status != "dead_lettered")
        {
            MessageText.Text = "Only dead-lettered deliveries can be retried.";
            return;
        }

        var reason = ReasonBox.Text ?? "";
        await Execute(async () =>
        {
            await manager.RetryAsync(
                organizationId,
                delivery.Id,
                reason,
                Guid.NewGuid(),
                default);
            ReasonBox.Clear();
            await Refresh();
        });
    }

    private async Task Refresh() => await Execute(async () =>
    {
        var page = await manager.DeliveriesAsync(
            organizationId,
            Selected(StatusBox),
            Selected(ChannelBox),
            default);
        DeliveryList.ItemsSource = page.Items;
    });

    private async Task Execute(Func<Task> action)
    {
        try
        {
            await action();
            MessageText.Text = "";
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            MessageText.Text = exception is HttpRequestException
                ? "Notification delivery service request failed."
                : exception.Message;
        }
    }

    private static string? Selected(ComboBox box)
    {
        var value = (box.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return value == "all" ? null : value;
    }
}
