using Avalonia.Controls;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerGlobalConfigurationWindow : Window
{
    private readonly IGlobalConfiguration configuration;
    private readonly Guid organizationId;
    private readonly Guid branchId;
    private IReadOnlyList<TaxProfileSummary> profiles = [];

    public ManagerGlobalConfigurationWindow() =>
        throw new InvalidOperationException("Global configuration runtime is required.");

    public ManagerGlobalConfigurationWindow(IGlobalConfiguration configuration,
        Guid organizationId, Guid branchId)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Global configuration scope is invalid.");
        this.organizationId = organizationId;
        this.branchId = branchId;
        InitializeComponent();
        Opened += async (_, _) => await LoadAll();
    }
    private async Task LoadAll()
    {
        await LoadNotifications();
        await LoadProfiles();
        await LoadLocalization();
    }

    private async Task LoadNotifications()
    {
        try
        {
            var page = await configuration.ListNotificationsAsync(organizationId, 100, null,
                UnreadOnlyBox.IsChecked == true, default);
            NotificationsList.ItemsSource = page.Items.Select(x => new NotificationRow(x.Id,
                $"{x.Severity.ToUpperInvariant()} · {x.Title}", x.Body,
                $"{x.CreatedAt:yyyy-MM-dd HH:mm} UTC · {(x.IsRead ? "read" : "unread")}"));
            var preferences = await configuration.ReadNotificationPreferencesAsync(organizationId, default);
            InAppBox.IsChecked = preferences.InAppEnabled;
            EmailBox.IsChecked = preferences.EmailEnabled;
            PushBox.IsChecked = preferences.PushEnabled;
            NotificationStatusText.Text = $"Loaded {page.Items.Count} notification(s).";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            NotificationStatusText.Text = "Notifications could not be loaded.";
        }
    }

    private async void RefreshNotificationsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await LoadNotifications();
    private async void MarkReadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string idText } || !Guid.TryParse(idText, out var id)) return;
        try
        {
            await configuration.MarkNotificationReadAsync(organizationId, id, default);
            await LoadNotifications();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            NotificationStatusText.Text = "The notification could not be marked as read.";
        }
    }

    private async void SavePreferencesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await configuration.UpdateNotificationPreferencesAsync(organizationId,
                InAppBox.IsChecked == true, EmailBox.IsChecked == true, PushBox.IsChecked == true, default);
            NotificationStatusText.Text = "Notification preferences saved.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            NotificationStatusText.Text = "Notification preferences could not be saved.";
        }
    }
    private async Task LoadProfiles()
    {
        try
        {
            var page = await configuration.ListTaxProfilesAsync(organizationId, 100, null, default);
            profiles = page.Items;
            ProfilesBox.ItemsSource = profiles.Select(x => new ProfileRow(x.Id,
                $"{x.Code} · {x.Name} · {x.CountryCode} · {(x.PricesIncludeTax ? "inclusive" : "exclusive")}"));
            ProfilesBox.SelectedIndex = profiles.Count > 0 ? 0 : -1;
            TaxStatusText.Text = $"Loaded {profiles.Count} tax profile(s).";
            await LoadRates();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            TaxStatusText.Text = "Tax profiles could not be loaded.";
        }
    }

    private async void CreateProfileClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await configuration.CreateTaxProfileAsync(organizationId,
                new(ProfileCodeBox.Text ?? "", ProfileNameBox.Text ?? "",
                    (CountryCodeBox.Text ?? "").ToUpperInvariant(), PricesIncludeTaxBox.IsChecked == true),
                Guid.NewGuid(), default);
            ProfileCodeBox.Clear(); ProfileNameBox.Clear();
            await LoadProfiles();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            TaxStatusText.Text = "Tax profile could not be created.";
        }
    }

    private async void ProfileSelectionChanged(object? sender, SelectionChangedEventArgs e) => await LoadRates();

    private TaxProfileSummary? SelectedProfile()
    {
        if (ProfilesBox.SelectedItem is not ProfileRow selected) return null;
        return profiles.FirstOrDefault(x => x.Id == selected.Id);
    }

    private async Task LoadRates()
    {
        var profile = SelectedProfile();
        if (profile is null) { RatesList.ItemsSource = null; return; }
        try
        {
            var rates = await configuration.ListTaxRatesAsync(organizationId, profile.Id, default);
            RatesList.ItemsSource = rates.Select(x =>
                $"{x.CategoryCode} · {x.RatePercent:0.######}% · {(x.BranchId.HasValue ? "branch" : "organization")} · from {x.EffectiveFrom:yyyy-MM-dd}");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            TaxStatusText.Text = "Tax rates could not be loaded.";
        }
    }
    private async void CreateRateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var profile = SelectedProfile();
        if (profile is null || !decimal.TryParse(RatePercentBox.Text,
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var rate)) return;
        try
        {
            await configuration.CreateTaxRateAsync(organizationId, profile.Id,
                new(BranchSpecificBox.IsChecked == true ? branchId : null,
                    (CategoryCodeBox.Text ?? "").Trim().ToUpperInvariant(), rate,
                    DateTimeOffset.UtcNow, null), Guid.NewGuid(), default);
            await LoadRates();
            TaxStatusText.Text = "Tax rate created.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            TaxStatusText.Text = "Tax rate could not be created.";
        }
    }

    private async void CalculateTaxClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var profile = SelectedProfile();
        if (profile is null || !decimal.TryParse(AmountBox.Text,
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount)) return;
        try
        {
            var result = await configuration.CalculateTaxAsync(organizationId, profile.Id, branchId,
                (CalculateCategoryBox.Text ?? "").Trim().ToUpperInvariant(), amount,
                DateTimeOffset.UtcNow, default);
            TaxStatusText.Text = $"Net {result.NetAmount:0.######} · Tax {result.TaxAmount:0.######} · Gross {result.GrossAmount:0.######} · {result.RatePercent:0.######}%";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            TaxStatusText.Text = "Tax calculation could not be completed.";
        }
    }

    private async Task LoadLocalization()
    {
        try
        {
            var value = await configuration.ReadLocalizationAsync(organizationId, default);
            LocCountryBox.Text = value.CountryCode; LocCurrencyBox.Text = value.DefaultCurrency;
            LocDefaultLocaleBox.Text = value.DefaultLocale; LocTimeZoneBox.Text = value.TimeZone;
            LocSupportedBox.Text = string.Join(",", value.SupportedLocales); LocFirstDayBox.Text = value.FirstDayOfWeek.ToString();
            LocalizationStatusText.Text = $"Localization loaded · version {value.Version}.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        { LocalizationStatusText.Text = "Localization is not configured or could not be loaded."; }
    }

    private async void SaveLocalizationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!int.TryParse(LocFirstDayBox.Text, out var firstDay)) return;
        try
        {
            LocalizationSettingsSummary? current = null;
            try { current = await configuration.ReadLocalizationAsync(organizationId, default); } catch { }
            var locales = (LocSupportedBox.Text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var result = await configuration.UpdateLocalizationAsync(organizationId, new(
                (LocCountryBox.Text ?? "").ToUpperInvariant(), LocDefaultLocaleBox.Text ?? "",
                (LocCurrencyBox.Text ?? "").ToUpperInvariant(), LocTimeZoneBox.Text ?? "", locales, firstDay, current?.Version), Guid.NewGuid(), default);
            LocalizationStatusText.Text = $"Localization saved · version {result.Version}.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        { LocalizationStatusText.Text = "Localization could not be saved."; }
    }

    private sealed record NotificationRow(Guid Id, string Heading, string Body, string Meta);
    private sealed record ProfileRow(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
